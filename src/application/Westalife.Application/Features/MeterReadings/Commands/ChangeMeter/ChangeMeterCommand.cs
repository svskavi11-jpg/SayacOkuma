using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.MeterReadings.Commands.ChangeMeter;

/// <summary>
/// Sayaç değişimi. Eski sayaca kapanış okuması yazar ve pasife alır; yeni sayacı
/// açılış endeksiyle (tüketim 0) açar. İkisi tek kayıtta yazılır.
/// </summary>
public sealed record ChangeMeterCommand(
    Guid ApartmentId,
    decimal ClosingIndex,
    string NewSerialNumber,
    decimal OpeningIndex) : IRequest<ChangeMeterResult>;

public sealed record ChangeMeterResult(
    Guid OldMeterId,
    Guid NewMeterId,
    string NewSerialNumber,
    decimal ClosingConsumptionKWh,
    decimal OpeningIndex);

public sealed class ChangeMeterCommandValidator : AbstractValidator<ChangeMeterCommand>
{
    public ChangeMeterCommandValidator()
    {
        RuleFor(c => c.ApartmentId).NotEmpty();

        RuleFor(c => c.ClosingIndex)
            .GreaterThanOrEqualTo(0m)
            .WithMessage(DomainConstants.ValidationMessages.MeterIndexValueMustBeNonNegative);

        RuleFor(c => c.OpeningIndex)
            .GreaterThanOrEqualTo(0m)
            .WithMessage(DomainConstants.ValidationMessages.MeterIndexValueMustBeNonNegative);

        RuleFor(c => c.NewSerialNumber)
            .NotEmpty()
            .WithMessage(DomainConstants.ValidationMessages.SerialNumberRequired)
            .MaximumLength(DomainConstants.Meter.SerialNumberMaxLength)
            .WithMessage(DomainConstants.ValidationMessages.SerialNumberRequired);
    }
}

public sealed class ChangeMeterCommandHandler : IRequestHandler<ChangeMeterCommand, ChangeMeterResult>
{
    private readonly IApplicationDbContext _db;

    public ChangeMeterCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ChangeMeterResult> Handle(ChangeMeterCommand request, CancellationToken cancellationToken)
    {
        var oldMeter = await _db.Meters
            .Where(m => m.ApartmentId == request.ApartmentId
                && m.IsActive
                && m.Type == MeterType.Calorimeter)
            .OrderByDescending(m => m.CreatedOn)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new DomainException(DomainConstants.ValidationMessages.ReadingImportNoActiveMeter);

        var newSerial = request.NewSerialNumber.Trim();
        if (string.Equals(newSerial, oldMeter.SerialNumber, StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException(DomainConstants.ValidationMessages.MeterReplacementSameSerial);
        }

        var serialTaken = await _db.Meters.AnyAsync(
            m => m.SerialNumber.ToLower() == newSerial.ToLower(),
            cancellationToken);
        if (serialTaken)
        {
            throw new DomainException(DomainConstants.ValidationMessages.MeterSerialAlreadyRegistered);
        }

        var lastReading = await _db.MeterReadings
            .Where(r => r.MeterId == oldMeter.Id)
            .OrderByDescending(r => r.ReadingDateUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastReading is not null && request.ClosingIndex < lastReading.IndexValue)
        {
            throw new DomainException(DomainConstants.ValidationMessages.ManualReadingIndexRegression);
        }

        var readingAt = DateTimeOffset.UtcNow;
        if (lastReading is not null && readingAt <= lastReading.ReadingDateUtc)
        {
            readingAt = lastReading.ReadingDateUtc.AddTicks(1);
        }

        var periodLocked = await _db.GasInvoices.AnyAsync(
            i => i.IsLocked
                && i.ReadingDateFrom <= readingAt
                && readingAt < i.ReadingDateTo,
            cancellationToken);
        if (periodLocked)
        {
            throw new DomainException(DomainConstants.GasInvoice.PeriodLocked);
        }

        var duplicate = await _db.MeterReadings.AnyAsync(
            r => r.MeterId == oldMeter.Id && r.ReadingDateUtc == readingAt,
            cancellationToken);
        if (duplicate)
        {
            throw new DomainException(DomainConstants.ValidationMessages.ManualReadingDuplicate);
        }

        var closingConsumption = lastReading is null ? 0m : request.ClosingIndex - lastReading.IndexValue;
        if (closingConsumption > DomainConstants.MeterReading.MaxPeriodConsumptionKWh)
        {
            throw new DomainException(DomainConstants.ValidationMessages.MeterConsumptionMustBeValid);
        }

        var closing = new MeterReading(
            oldMeter.Id,
            request.ClosingIndex,
            closingConsumption,
            readingAt,
            ReadingSource.ManualEntry,
            isInitialReading: lastReading is null);
        _db.MeterReadings.Add(closing);
        oldMeter.Deactivate();

        var newMeter = Meter.CreateCalorimeter(
            newSerial,
            request.ApartmentId,
            oldMeter.MbusPrimaryAddress,
            oldMeter.Manufacturer,
            readingAt);
        _db.Meters.Add(newMeter);

        var opening = new MeterReading(
            newMeter.Id,
            request.OpeningIndex,
            0m,
            readingAt,
            ReadingSource.ManualEntry,
            isInitialReading: true);
        _db.MeterReadings.Add(opening);

        await _db.SaveChangesAsync(cancellationToken);

        return new ChangeMeterResult(
            oldMeter.Id,
            newMeter.Id,
            newMeter.SerialNumber,
            closingConsumption,
            request.OpeningIndex);
    }
}
