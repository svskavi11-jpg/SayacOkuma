using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.MeterReadings.Commands.AddManualMeterReading;

/// <summary>
/// Manuel sayaç okuma girişi (Etap 6.6 — saha arızalarına B planı).
/// Okuma Source=ManualEntry ile kaydedilir. Dönem dışı tarih ENGELLENMEZ, yalnız uyarılır
/// (yönetici sorumluluğu); kilitli döneme düşen tarih ise reddedilir.
/// </summary>
public sealed record AddManualMeterReadingCommand(
    Guid ApartmentId,
    Guid MeterId,
    DateTimeOffset ReadingDateUtc,
    decimal IndexValue) : IRequest<AddManualMeterReadingResult>;

public sealed record AddManualMeterReadingResult(
    Guid ReadingId,
    decimal IndexValue,
    decimal PeriodConsumptionKWh,
    bool IsInitialReading,
    IReadOnlyList<string> Warnings);

public sealed class AddManualMeterReadingCommandValidator : AbstractValidator<AddManualMeterReadingCommand>
{
    public AddManualMeterReadingCommandValidator()
    {
        RuleFor(c => c.ApartmentId).NotEmpty();
        RuleFor(c => c.MeterId).NotEmpty();

        RuleFor(c => c.IndexValue)
            .GreaterThanOrEqualTo(0m)
            .WithMessage(DomainConstants.ValidationMessages.MeterIndexValueMustBeNonNegative);

        // Tarih toleransı: sunucu saat farklarına karşı 5 dk pay bırakılır.
        RuleFor(c => c.ReadingDateUtc)
            .LessThanOrEqualTo(DateTimeOffset.UtcNow.AddMinutes(5))
            .WithMessage(DomainConstants.ValidationMessages.ManualReadingFutureDate);
    }
}

public sealed class AddManualMeterReadingCommandHandler
    : IRequestHandler<AddManualMeterReadingCommand, AddManualMeterReadingResult>
{
    private readonly IApplicationDbContext _db;

    public AddManualMeterReadingCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AddManualMeterReadingResult> Handle(
        AddManualMeterReadingCommand request, CancellationToken cancellationToken)
    {
        var meter = await _db.Meters
            .FirstOrDefaultAsync(m => m.Id == request.MeterId && m.IsActive, cancellationToken)
            ?? throw new DomainException(DomainConstants.ValidationMessages.ManualReadingMeterNotFound);

        if (meter.ApartmentId != request.ApartmentId)
        {
            throw new DomainException(DomainConstants.ValidationMessages.ManualReadingMeterNotInApartment);
        }

        var readingDate = request.ReadingDateUtc;

        // Kilitli dönem koruması (Özellik 3): kilitli fatura penceresine düşen tarih reddedilir.
        var isLockedPeriod = await _db.GasInvoices
            .AnyAsync(i => i.IsLocked
                && i.ReadingDateFrom <= readingDate
                && readingDate < i.ReadingDateTo, cancellationToken);

        if (isLockedPeriod)
        {
            throw new DomainException(DomainConstants.GasInvoice.PeriodLocked);
        }

        // Mükerrer koruma: aynı sayaç + aynı tarih (unique index'in önceden kontrolü).
        var isDuplicate = await _db.MeterReadings
            .AnyAsync(r => r.MeterId == meter.Id && r.ReadingDateUtc == readingDate, cancellationToken);

        if (isDuplicate)
        {
            throw new DomainException(DomainConstants.ValidationMessages.ManualReadingDuplicate);
        }

        // Endeks gerilemesi: muhasebe güvenliği için reddedilir (Kullanıcı kararı).
        var lastReading = await _db.MeterReadings
            .Where(r => r.MeterId == meter.Id && r.ReadingDateUtc < readingDate)
            .OrderByDescending(r => r.ReadingDateUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastReading is not null && request.IndexValue < lastReading.IndexValue)
        {
            throw new DomainException(DomainConstants.ValidationMessages.ManualReadingIndexRegression);
        }

        // Dönem uyarısı (Kullanıcı kararı: engelleme DEĞİL) — tarih hiçbir fatura penceresinde değilse.
        var warnings = new List<string>();
        var isInAnyInvoicePeriod = await _db.GasInvoices
            .AnyAsync(i => i.ReadingDateFrom <= readingDate
                && readingDate < i.ReadingDateTo, cancellationToken);

        if (!isInAnyInvoicePeriod)
        {
            warnings.Add(
                "Bu okuma tarihi hiçbir fatura döneminin dışında — " +
                "mevcut hesaplamalarda KULLANILMAZ (saha arızası B planı kaydı).");
        }

        // İlk kayıt = başlangıç endeksi (tüketim 0); sonraki kayıtlar = fark.
        var isInitialReading = lastReading is null;
        var periodConsumption = lastReading is null
            ? 0m
            : Math.Max(0m, request.IndexValue - lastReading.IndexValue);

        var reading = new MeterReading(
            meter.Id,
            request.IndexValue,
            periodConsumption,
            readingDate,
            ReadingSource.ManualEntry,
            isInitialReading);

        _db.MeterReadings.Add(reading);
        await _db.SaveChangesAsync(cancellationToken);

        return new AddManualMeterReadingResult(
            reading.Id, request.IndexValue, periodConsumption, isInitialReading, warnings);
    }
}