using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.MeterReadings.Commands.ImportReadingsFromExcel;

/// <summary>
/// Excel'den toplu okuma içe aktarma (Etap 6.6 — dış kaynak / Izar Center dışa aktarımı).
/// Kayıtlar Source=Import ile yazılır; kısmi başarı modeli: hatalı satırlar raporlanır,
/// geçerli satırlar tek transaction'da kaydedilir.
/// </summary>
public sealed record ImportReadingsFromExcelCommand(byte[] ExcelContent)
    : IRequest<ImportReadingsFromExcelResult>;

public sealed record ImportReadingsFromExcelResult(
    int ImportedCount,
    int SkippedDuplicateCount,
    int UnmatchedApartmentCount,
    IReadOnlyList<string> RowErrors);

public sealed class ImportReadingsFromExcelCommandValidator
    : AbstractValidator<ImportReadingsFromExcelCommand>
{
    public ImportReadingsFromExcelCommandValidator()
    {
        RuleFor(c => c.ExcelContent)
            .NotEmpty()
            .Must(content => content.Length <= 5 * 1024 * 1024)
            .WithMessage("Excel dosyası en fazla 5 MB olabilir.");
    }
}

public sealed class ImportReadingsFromExcelCommandHandler
    : IRequestHandler<ImportReadingsFromExcelCommand, ImportReadingsFromExcelResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IExcelReadingImportParser _parser;

    public ImportReadingsFromExcelCommandHandler(IApplicationDbContext db, IExcelReadingImportParser parser)
    {
        _db = db;
        _parser = parser;
    }

    public async Task<ImportReadingsFromExcelResult> Handle(
        ImportReadingsFromExcelCommand request, CancellationToken cancellationToken)
    {
        // 1) Excel'i satırlara ayrıştır (dinamik başlık eşlemesi + satır bazlı hatalar).
        using var stream = new MemoryStream(request.ExcelContent);
        var parsed = _parser.Parse(stream);

        // 2) Kapı No → daire çözümleme (ImportMetersFromIzarCenter ile aynı eşleme).
        //    Sayısal olmayan kapı numaraları (varsa) sessizce eşleşmez olarak işaretlenir.
        var apartmentsByDoor = (await _db.Apartments.ToListAsync(cancellationToken))
            .Where(a => int.TryParse(a.DoorNumber, out _))
            .GroupBy(a => int.Parse(a.DoorNumber))
            .ToDictionary(g => g.Key, g => g.First());

        // 3) Daire → aktif kalorimetre eşlemesi. Bir dairede birden fazla aktif kalorimetre varsa
        //    en son eklenen (CreatedOn en yeni) seçilir — deterministik; yeni kayıt eskiyi devralır.
        var metersByApartment = (await _db.Meters
            .Where(m => m.IsActive && m.Type == MeterType.Calorimeter && m.ApartmentId != null)
            .ToListAsync(cancellationToken))
            .GroupBy(m => m.ApartmentId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.CreatedOn).First());

        // 3b) Seri no ile eşleme (bağlantısız okuyucu programın dosyası): aynı seri no'da en yeni aktif sayaç.
        var metersBySerial = (await _db.Meters
            .Where(m => m.IsActive && m.Type == MeterType.Calorimeter)
            .ToListAsync(cancellationToken))
            .GroupBy(m => m.SerialNumber.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.CreatedOn).First(), StringComparer.OrdinalIgnoreCase);

        // 4) Kilitli dönem pencereleri (satır bazlı engelleme için önceden yüklenir).
        var lockedPeriods = await _db.GasInvoices
            .Where(i => i.IsLocked)
            .Select(i => new { i.ReadingDateFrom, i.ReadingDateTo })
            .ToListAsync(cancellationToken);

        var imported = 0;
        var skippedDuplicate = 0;
        var unmatched = 0;
        var rowErrors = new List<string>(parsed.Errors);

        // Dosya içi zincirleme okuma takibi: aynı sayaca ait birden çok satır tek dosyada gelebilir.
        // DB SaveChanges dosyanın sonunda yapıldığından, "son okuma" beklemedeki satırlardan da alınır;
        // yalnızca sayacın İLK satırı IsInitialReading=true ve tüketim 0 olarak kaydedilir.
        var pendingLastByMeter = new Dictionary<Guid, decimal>();
        var pendingKeys = new HashSet<(Guid MeterId, DateTimeOffset ReadingDateUtc)>();

        foreach (var row in parsed.Rows)
        {
            // Etiket (hata mesajları için): seri no varsa "Sayaç X", yoksa "Kapı N".
            var label = !string.IsNullOrWhiteSpace(row.SerialNumber)
                ? $"Sayaç {row.SerialNumber}"
                : $"Kapı {row.DoorNumber}";

            // Eşleşme: seri no doluysa seri no ile; değilse kapı no → daire → aktif kalorimetre.
            Meter? meter = null;
            if (!string.IsNullOrWhiteSpace(row.SerialNumber))
            {
                metersBySerial.TryGetValue(row.SerialNumber.Trim(), out meter);
            }
            else if (row.DoorNumber is { } door
                && apartmentsByDoor.TryGetValue(door, out var apartment))
            {
                metersByApartment.TryGetValue(apartment.Id, out meter);
            }

            if (meter is null)
            {
                unmatched++;
                rowErrors.Add(
                    $"{label}: {DomainConstants.ValidationMessages.ReadingImportNoActiveMeter}");
                continue;
            }

            // Kilitli dönem koruması (Özellik 3): satır bazlı hata, diğer satırlar işlenir.
            if (lockedPeriods.Any(p => p.ReadingDateFrom <= row.ReadingDateUtc
                && row.ReadingDateUtc < p.ReadingDateTo))
            {
                rowErrors.Add($"{label}: {DomainConstants.GasInvoice.PeriodLocked}");
                continue;
            }

            // Mükerrer koruma: aynı sayaç + aynı tarih atlanır (idempotent içe aktarma).
            // DB kontrolünün yanı sıra aynı dosyadaki beklemedeki satırlar da dikkate alınır.
            if (pendingKeys.Contains((meter.Id, row.ReadingDateUtc))
                || await _db.MeterReadings.AnyAsync(
                    r => r.MeterId == meter.Id && r.ReadingDateUtc == row.ReadingDateUtc, cancellationToken))
            {
                skippedDuplicate++;
                continue;
            }

            // Efektif son okuma: önce dosya içi beklemedeki satır, yoksa DB'deki son okuma.
            decimal? lastIndex;
            if (pendingLastByMeter.TryGetValue(meter.Id, out var pendingIndex))
            {
                lastIndex = pendingIndex;
            }
            else
            {
                var lastReading = await _db.MeterReadings
                    .Where(r => r.MeterId == meter.Id && r.ReadingDateUtc < row.ReadingDateUtc)
                    .OrderByDescending(r => r.ReadingDateUtc)
                    .FirstOrDefaultAsync(cancellationToken);
                lastIndex = lastReading?.IndexValue;
            }

            // Endeks gerilemesi: satır hatası olarak raporlanır, diğer satırlar işlenir.
            if (lastIndex.HasValue && row.IndexValue < lastIndex.Value)
            {
                rowErrors.Add(
                    $"{label}: {DomainConstants.ValidationMessages.ManualReadingIndexRegression}");
                continue;
            }

            // İlk okuma kuralı (canlı hata düzeltmesi): DB'de (ve dosyada) hiç okuma yoksa Excel'den
            // gelen endeks başlangıç endeksidir → tüketim 0, IsInitialReading=true.
            var isInitialReading = !lastIndex.HasValue;
            var lastIdx = isInitialReading ? 0m : lastIndex.Value;
            var periodConsumption = isInitialReading
                ? 0m
                : Math.Max(0m, row.IndexValue - lastIdx);

            // Tüketim üst sınırı: domain exception fırlatmak yerine satır hatası raporlanır
            // (kısmi başarı — tek satır tüm içe aktarmayı çökertmez).
            if (periodConsumption > DomainConstants.MeterReading.MaxPeriodConsumptionKWh)
            {
                rowErrors.Add(
                    $"{label}: {DomainConstants.ValidationMessages.MeterConsumptionMustBeValid} " +
                    $"(son endeks {lastIdx} → girilen endeks {row.IndexValue}; fark " +
                    $"{periodConsumption} kWh, üst sınır {DomainConstants.MeterReading.MaxPeriodConsumptionKWh} kWh).");
                continue;
            }

            try
            {
                _db.MeterReadings.Add(new MeterReading(
                    meter.Id,
                    row.IndexValue,
                    periodConsumption,
                    row.ReadingDateUtc,
                    ReadingSource.Import,
                    isInitialReading));
            }
            catch (ArgumentException ex)
            {
                // Beklenmeyen alan hatası: satır bazlı raporlanır, içe aktarma devam eder.
                rowErrors.Add($"{label}: {ex.Message}");
                continue;
            }

            pendingLastByMeter[meter.Id] = row.IndexValue;
            pendingKeys.Add((meter.Id, row.ReadingDateUtc));
            imported++;
        }

        // Tek SaveChanges = tek transaction; kısmi yazım olamaz.
        await _db.SaveChangesAsync(cancellationToken);

        return new ImportReadingsFromExcelResult(imported, skippedDuplicate, unmatched, rowErrors);
    }
}