using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Entities;

namespace Westalife.Application.Features.MeterReadings.Commands.RecordMeterReadings;

public sealed class RecordMeterReadingsCommandHandler
    : IRequestHandler<RecordMeterReadingsCommand, RecordMeterReadingsResult>
{
    private readonly IApplicationDbContext _db;
    private readonly ILogger<RecordMeterReadingsCommandHandler> _logger;

    public RecordMeterReadingsCommandHandler(IApplicationDbContext db, ILogger<RecordMeterReadingsCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<RecordMeterReadingsResult> Handle(
        RecordMeterReadingsCommand request, CancellationToken cancellationToken)
    {
        var serialNumbers = request.Readings
            .Select(r => r.SerialNumber)
            .Distinct()
            .ToList();

        // Seri no → meter eşlemesi tek sorguda çözülür.
        var metersBySerial = await _db.Meters
            .Where(m => serialNumbers.Contains(m.SerialNumber))
            .ToDictionaryAsync(m => m.SerialNumber, m => m, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var inserted = 0;
        var skippedDuplicate = 0;
        var skippedDeviceError = 0;
        var unmatchedSerial = 0;

        foreach (var snapshot in request.Readings)
        {
            if (!metersBySerial.TryGetValue(snapshot.SerialNumber, out var meter))
            {
                unmatchedSerial++;
                _logger.LogWarning(
                    "Okuma eşleşmedi: seri no {Serial} sistemde kayıtlı değil (adres {Address}).",
                    snapshot.SerialNumber, snapshot.PrimaryAddress);
                continue;
            }

            if (snapshot.HasDataError)
            {
                skippedDeviceError++;
                _logger.LogWarning(
                    "Okuma atlandı: sayaç {Serial} cihaz veri hatası bildirdi (Operating Time → Data Error).",
                    snapshot.SerialNumber);
                continue;
            }

            // Idempotency: aynı sayaç + aynı zaman dilimi zaten kayıtlıysa atla (unique index'in önceden kontrolü).
            var isDuplicate = await _db.MeterReadings.AnyAsync(
                r => r.MeterId == meter.Id && r.ReadingDateUtc == snapshot.ReadingTimestampUtc,
                cancellationToken);

            if (isDuplicate)
            {
                skippedDuplicate++;
                continue;
            }

            // Kümülatif endeksten dönem tüketimi: son kayıttan fark (yoksa veya gerileme varsa 0).
            var lastReading = await _db.MeterReadings
                .Where(r => r.MeterId == meter.Id && r.ReadingDateUtc < snapshot.ReadingTimestampUtc)
                .OrderByDescending(r => r.ReadingDateUtc)
                .FirstOrDefaultAsync(cancellationToken);

            // İlk kurulum: sayacın hiç okuması yoksa bu kayıt "Başlangıç Endeksi"dir —
            // tüketim 0 kabul edilir ve IsInitialReading ile işaretlenir (Özellik 3).
            var isInitialReading = lastReading is null;
            var periodConsumption = lastReading is null
                ? 0m
                : Math.Max(0m, snapshot.CumulativeEnergyKWh - lastReading.IndexValue);

            _db.MeterReadings.Add(new MeterReading(
                meter.Id,
                snapshot.CumulativeEnergyKWh,
                periodConsumption,
                snapshot.ReadingTimestampUtc,
                request.Source,
                isInitialReading));

            inserted++;
        }

        // Tek SaveChanges = tek transaction; kısmi yazım olamaz.
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Okuma kaydı tamamlandı: {Inserted} eklendi, {Duplicate} duplicate, {DeviceError} cihaz hatası, {Unmatched} eşleşmedi.",
            inserted, skippedDuplicate, skippedDeviceError, unmatchedSerial);

        return new RecordMeterReadingsResult(inserted, skippedDuplicate, skippedDeviceError, unmatchedSerial);
    }
}
