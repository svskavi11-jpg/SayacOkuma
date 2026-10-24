using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Meters.Commands.ImportMeters;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Apartments.Commands.ImportMetersFromExcel;

/// <summary>
/// Excel içeriğinden (parser çıktısı) toplu sayaç içe aktarma.
/// ImportMetersFromIzarCenterCommand ile aynı çekirdek mantık kullanılır; opsiyonel
/// "Başlangıç Endeksi" doluysa IsInitialReading=true okuma satırı da oluşturulur (Özellik 3).
/// </summary>
public sealed record ImportMetersFromExcelCommand(byte[] ExcelContent) : IRequest<ImportMetersFromExcelResult>;

public sealed record ImportMetersFromExcelResult(
    int ImportedCount,
    int SkippedExistingCount,
    int UnmatchedApartmentCount,
    int InitialReadingsCreated,
    int OwnersSetCount,
    int DimensionsUpdatedCount,
    IReadOnlyList<string> RowErrors);

public sealed class ImportMetersFromExcelCommandValidator : AbstractValidator<ImportMetersFromExcelCommand>
{
    public ImportMetersFromExcelCommandValidator()
    {
        RuleFor(c => c.ExcelContent)
            .NotEmpty()
            .Must(content => content.Length <= 5 * 1024 * 1024)
            .WithMessage("Excel dosyası en fazla 5 MB olabilir.");
    }
}

public sealed class ImportMetersFromExcelCommandHandler
    : IRequestHandler<ImportMetersFromExcelCommand, ImportMetersFromExcelResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IExcelMeterImportParser _parser;
    private readonly IMediator _mediator;

    public ImportMetersFromExcelCommandHandler(
        IApplicationDbContext db, IExcelMeterImportParser parser, IMediator mediator)
    {
        _db = db;
        _parser = parser;
        _mediator = mediator;
    }

    public async Task<ImportMetersFromExcelResult> Handle(
        ImportMetersFromExcelCommand request, CancellationToken cancellationToken)
    {
        // 1) Excel'i satırlara ayrıştır (dinamik başlık eşlemesi + satır bazlı hatalar).
        using var stream = new MemoryStream(request.ExcelContent);
        var parsed = _parser.Parse(stream);

        // Geçerli satır yoksa GERÇEK nedeni bildir (başlık eşleşmedi, dosya boş, tüm satırlar hatalı...).
        // Aksi halde ImportMetersFromIzarCenterCommand'ın "Items boş olamaz" validasyonu
        // kullanıcıyı yanıltan genel bir hata üretir.
        if (parsed.Rows.Count == 0)
        {
            throw new DomainException(
                "Excel'de içe aktarılabilir satır bulunamadı. " + string.Join(" | ", parsed.Errors));
        }

        // 2) Sayaçları içe aktar (mevcut çekirdek komut: duplicate skip + unmatched raporu).
        var importResult = await _mediator.Send(
            new ImportMetersFromIzarCenterCommand(
                parsed.Rows
                    .Select(r => new ImportedMeterItem(r.DoorNumber, r.SerialNumber, r.PrimaryAddress))
                    .ToList()),
            cancellationToken);

        // 3) Başlangıç endeksi dolu satırlar için IsInitialReading=true okuma oluştur (Özellik 3).
        var serials = parsed.Rows.Select(r => r.SerialNumber).ToList();
        var metersBySerial = await _db.Meters
            .Where(m => serials.Contains(m.SerialNumber))
            .ToDictionaryAsync(m => m.SerialNumber, m => m, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var initialReadingsCreated = 0;
        foreach (var row in parsed.Rows)
        {
            if (row.InitialIndexValue is null)
            {
                continue;
            }

            if (!metersBySerial.TryGetValue(row.SerialNumber, out var meter))
            {
                continue;
            }

            var readingDate = row.InitialReadingDate ?? DateTimeOffset.UtcNow;

            // Aynı tarih + sayaç için okuma zaten varsa duplicate koruması (unique index öncesi kontrol).
            var isDuplicate = await _db.MeterReadings.AnyAsync(
                r => r.MeterId == meter.Id && r.ReadingDateUtc == readingDate,
                cancellationToken);

            if (isDuplicate)
            {
                continue;
            }

            _db.MeterReadings.Add(new MeterReading(
                meter.Id,
                row.InitialIndexValue.Value,
                0m, // başlangıç endeksi tüketim DEĞİLDİR (Özellik 3)
                readingDate,
                ReadingSource.Import,
                isInitialReading: true));

            initialReadingsCreated++;
        }

        // Tek SaveChanges = tek transaction (sayaçlar + başlangıç okumaları birlikte).
        await _db.SaveChangesAsync(cancellationToken);

        // 4) Etap 6.7: Kat Maliki Adı Soyadı ve m² sütunları dolu satırlar için SCD Type 2
        //    kayıtları uygula (owner + dimension). Aynı değere dokunulmaz (no-op), geçmiş korunur.
        var doorsInFile = parsed.Rows
            .Where(r => r.OwnerName is not null || r.SquareMeters is not null)
            .Select(r => r.DoorNumber)
            .Distinct()
            .ToList();

        var ownersSet = 0;
        var dimensionsUpdated = 0;

        if (doorsInFile.Count > 0)
        {
            // Kapı No string saklandığı için int.Parse'i bellekte uygula (EF çevirisi mümkün değil).
            var doorSet = doorsInFile.ToHashSet();
            var apartmentsForScd = (await _db.Apartments
                .Include(a => a.Owners)
                .Include(a => a.Dimensions)
                .ToListAsync(cancellationToken))
                .Where(a => int.TryParse(a.DoorNumber, out var d) && doorSet.Contains(d))
                .ToDictionary(a => int.Parse(a.DoorNumber), a => a);

            // Faturaya girmis (hesaplanmis) daireler: m² degisikligi SCD Type 2 ile islenir, gecmis fatura bozulmaz.
            var billedApartmentIds = (await _db.ApartmentInvoices
                .Select(i => i.ApartmentId)
                .Distinct()
                .ToListAsync(cancellationToken))
                .ToHashSet();

            foreach (var row in parsed.Rows)
            {
                if (row.OwnerName is null && row.SquareMeters is null)
                {
                    continue;
                }

                if (!apartmentsForScd.TryGetValue(row.DoorNumber, out var apartment))
                {
                    continue; // eşleşmeyen kapı zaten UnmatchedApartmentCount'ta raporlandı
                }

                if (row.OwnerName is not null)
                {
                    var activeOwner = apartment.Owners.FirstOrDefault(o => o.ValidTo is null);
                    var unchanged = activeOwner is not null
                        && activeOwner.OwnerName.Equals(row.OwnerName.Trim(), StringComparison.OrdinalIgnoreCase);

                    if (!unchanged)
                    {
                        var newOwner = apartment.UpdateOwner(row.OwnerName, DateTimeOffset.UtcNow);
                        _db.ApartmentOwners.Add(newOwner);
                        ownersSet++;
                    }
                }

                if (row.SquareMeters is not null)
                {
                    var activeDimension = apartment.Dimensions.FirstOrDefault(d => d.ValidTo is null);
                    var unchanged = activeDimension is not null
                        && activeDimension.SquareMeters == row.SquareMeters.Value;

                    if (!unchanged)
                    {
                        // Yalnizca henuz hic faturalanmamis (ilk kurulum / yer tutucu) daire icin "uzerine yaz";
                        // faturalanmis dairede gercek degisiklik -> eski kayit ValidTo ile kapanir, yeni kayit acilir.
                        var isUnbilled = !billedApartmentIds.Contains(apartment.Id);
                        if (activeDimension is not null && isUnbilled)
                        {
                            activeDimension.CorrectSquareMeters(row.SquareMeters.Value);
                        }
                        else
                        {
                            var newDimension = apartment.AddDimension(
                                row.SquareMeters.Value,
                                activeDimension?.CommonAreaRatio ?? DomainConstants.SeedData.DefaultCommonAreaRatio,
                                DateTimeOffset.UtcNow);
                            _db.ApartmentDimensions.Add(newDimension);
                        }

                        dimensionsUpdated++;
                    }
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        return new ImportMetersFromExcelResult(
            importResult.ImportedCount,
            importResult.SkippedExistingCount,
            importResult.UnmatchedApartmentCount,
            initialReadingsCreated,
            ownersSet,
            dimensionsUpdated,
            parsed.Errors);
    }
}
