using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.MeterReadings.Commands.UpdateMeterReading;

public sealed class UpdateMeterReadingCommandHandler
    : IRequestHandler<UpdateMeterReadingCommand, UpdateMeterReadingResult>
{
    private readonly IApplicationDbContext _db;

    public UpdateMeterReadingCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<UpdateMeterReadingResult> Handle(
        UpdateMeterReadingCommand request, CancellationToken cancellationToken)
    {
        // Kilitli dönem pencereleri (satır bazlı engelleme için önceden yüklenir).
        var lockedPeriods = await _db.GasInvoices
            .Where(i => i.IsLocked)
            .Select(i => new LockedPeriodWindow(i.ReadingDateFrom, i.ReadingDateTo))
            .ToListAsync(cancellationToken);

        var updated = 0;
        var rowErrors = new List<string>();

        foreach (var item in request.Items)
        {
            try
            {
                await CorrectSingleAsync(item, lockedPeriods, cancellationToken);
                updated++;
            }
            catch (DomainException ex)
            {
                rowErrors.Add(ex.Message);
            }
            catch (ArgumentException ex)
            {
                rowErrors.Add(ex.Message);
            }
        }

        // Tek SaveChanges = tek transaction; kısmi yazım olamaz.
        await _db.SaveChangesAsync(cancellationToken);

        return new UpdateMeterReadingResult(updated, rowErrors);
    }

    /// <summary>
    /// Tek okuma düzeltmesi: önceki/sonraki okuma ile zincir bütünlüğü, tüketim yeniden hesabı
    /// (bu okuma + SONRAKİ okuma) ve zorunlu nedenle denetim kaydı.
    /// </summary>
    private async Task CorrectSingleAsync(
        MeterReadingCorrectionItem item,
        IReadOnlyList<LockedPeriodWindow> lockedPeriods,
        CancellationToken cancellationToken)
    {
        var reading = await _db.MeterReadings
            .FirstOrDefaultAsync(r => r.Id == item.ReadingId, cancellationToken)
            ?? throw new DomainException(
                $"{DomainConstants.ValidationMessages.ReadingNotFound} (ID: {item.ReadingId})");

        var meterId = reading.MeterId;

        // Kilitli dönem koruması: okuma tarihi kilitli penceredeyse düzeltme reddedilir.
        if (lockedPeriods.Any(p => p.ReadingDateFrom <= reading.ReadingDateUtc
            && reading.ReadingDateUtc < p.ReadingDateTo))
        {
            throw new DomainException(
                $"{DomainConstants.GasInvoice.PeriodLocked} (Okuma tarihi: {reading.ReadingDateUtc:dd.MM.yyyy})");
        }

        if (reading.IndexValue == item.NewIndexValue)
        {
            throw new DomainException(DomainConstants.ValidationMessages.ReadingCorrectionNoChange);
        }

        // Zincir komşuları: aynı sayaçta tarih sırasına göre önceki ve sonraki okuma.
        var previous = await _db.MeterReadings
            .Where(r => r.MeterId == meterId && r.ReadingDateUtc < reading.ReadingDateUtc)
            .OrderByDescending(r => r.ReadingDateUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var next = await _db.MeterReadings
            .Where(r => r.MeterId == meterId && r.ReadingDateUtc > reading.ReadingDateUtc)
            .OrderBy(r => r.ReadingDateUtc)
            .FirstOrDefaultAsync(cancellationToken);

        // Zincir bütünlüğü: önceki okumadan küçük VEYA sonraki okumadan büyük olamaz.
        if ((previous is not null && item.NewIndexValue < previous.IndexValue)
            || (next is not null && item.NewIndexValue > next.IndexValue))
        {
            throw new DomainException(DomainConstants.ValidationMessages.ReadingCorrectionIndexOutsideChain);
        }

        // Yeni tüketimler: bu okuma (önceki fark) + SONRAKİ okumanın tüketimi (zincirleme).
        var isInitialReading = previous is null;
        var newConsumption = isInitialReading
            ? 0m
            : Math.Max(0m, item.NewIndexValue - previous.IndexValue);
        var nextNewConsumption = next is null
            ? 0m
            : Math.Max(0m, next.IndexValue - item.NewIndexValue);

        var oldIndex = reading.IndexValue;
        var oldConsumption = reading.ConsumptionKWh;

        reading.SetIndex(item.NewIndexValue, newConsumption, isInitialReading);

        if (next is not null && next.ConsumptionKWh != nextNewConsumption)
        {
            next.SetIndex(next.IndexValue, nextNewConsumption, next.IsInitialReading);
        }

        // Denetim izi: eski/yeni değer + zorunlu neden + kullanıcı (boşsa "Yönetici") + zaman.
        _db.MeterReadingAuditLogs.Add(new MeterReadingAuditLog(
            reading.Id,
            oldIndex,
            item.NewIndexValue,
            oldConsumption,
            newConsumption,
            item.Reason,
            item.ChangedBy ?? DomainConstants.MeterReadingAuditLog.DefaultChangedBy,
            DateTimeOffset.UtcNow));
    }
}