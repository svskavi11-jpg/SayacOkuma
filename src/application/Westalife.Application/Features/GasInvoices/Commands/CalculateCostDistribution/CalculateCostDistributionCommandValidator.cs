using FluentValidation;

namespace Westalife.Application.Features.GasInvoices.Commands.CalculateCostDistribution;

public sealed class CalculateCostDistributionCommandValidator : AbstractValidator<CalculateCostDistributionCommand>
{
    public CalculateCostDistributionCommandValidator()
    {
        RuleFor(c => c.GasInvoiceId).NotEmpty();
    }
}
