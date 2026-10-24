using FluentValidation;
using Westalife.Domain.Constants;

namespace Westalife.Application.Features.Meters.Commands.RegisterMeter;

public sealed class RegisterMeterCommandValidator : AbstractValidator<RegisterMeterCommand>
{
    public RegisterMeterCommandValidator()
    {
        RuleFor(c => c.SerialNumber)
            .NotEmpty()
            .MaximumLength(DomainConstants.Meter.SerialNumberMaxLength);

        RuleFor(c => c.DoorNumber)
            .InclusiveBetween(DomainConstants.Apartment.MinDoorNumber, DomainConstants.SeedData.ApartmentCount);

        RuleFor(c => c.PrimaryAddress)
            .InclusiveBetween(1, 250);

        RuleFor(c => c.Manufacturer)
            .NotEmpty()
            .MaximumLength(DomainConstants.Meter.ManufacturerMaxLength);
    }
}
