using FluentValidation;
using Westalife.Domain.Constants;

namespace Westalife.Application.Features.MeterReadings.Commands.RecordMeterReadings;

public sealed class RecordMeterReadingsCommandValidator : AbstractValidator<RecordMeterReadingsCommand>
{
    public RecordMeterReadingsCommandValidator()
    {
        RuleFor(c => c.Readings)
            .NotEmpty().WithMessage("Kaydedilecek okuma listesi boş olamaz.");

        RuleForEach(c => c.Readings).ChildRules(snapshot =>
        {
            snapshot.RuleFor(s => s.SerialNumber).NotEmpty();
            snapshot.RuleFor(s => s.CumulativeEnergyKWh)
                .GreaterThanOrEqualTo(0)
                .WithMessage(DomainConstants.ValidationMessages.MeterIndexValueMustBeNonNegative);
            snapshot.RuleFor(s => s.ReadingTimestampUtc).NotEmpty();
        });
    }
}
