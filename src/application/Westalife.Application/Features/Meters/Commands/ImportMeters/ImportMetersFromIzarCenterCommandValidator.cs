using FluentValidation;
using Westalife.Domain.Constants;

namespace Westalife.Application.Features.Meters.Commands.ImportMeters;

public sealed class ImportMetersFromIzarCenterCommandValidator : AbstractValidator<ImportMetersFromIzarCenterCommand>
{
    public ImportMetersFromIzarCenterCommandValidator()
    {
        RuleFor(c => c.Items)
            .NotEmpty().WithMessage("İçe aktarılacak sayaç listesi boş olamaz.")
            .Must(items => items.Count <= DomainConstants.SeedData.ApartmentCount)
            .WithMessage($"Tek seferde en fazla {DomainConstants.SeedData.ApartmentCount} sayaç içe aktarılabilir.");

        RuleForEach(c => c.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.SerialNumber).NotEmpty();
            item.RuleFor(i => i.PrimaryAddress).InclusiveBetween(1, 250);
            item.RuleFor(i => i.DoorNumber).InclusiveBetween(
                DomainConstants.Apartment.MinDoorNumber, DomainConstants.SeedData.ApartmentCount);
        });
    }
}
