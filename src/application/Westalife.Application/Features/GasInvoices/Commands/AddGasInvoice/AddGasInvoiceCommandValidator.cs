using FluentValidation;
using Westalife.Domain.Constants;

namespace Westalife.Application.Features.GasInvoices.Commands.AddGasInvoice;

public sealed class AddGasInvoiceCommandValidator : AbstractValidator<AddGasInvoiceCommand>
{
    public AddGasInvoiceCommandValidator()
    {
        RuleFor(c => c.InvoiceNumber)
            .NotEmpty()
            .MaximumLength(DomainConstants.GasInvoice.InvoiceNumberMaxLength);

        RuleFor(c => c.SubscriberNumber)
            .NotEmpty()
            .MaximumLength(DomainConstants.GasInvoice.SubscriberNumberMaxLength);

        RuleFor(c => c.MasterMeterSerial)
            .NotEmpty()
            .MaximumLength(DomainConstants.GasInvoice.MeterSerialMaxLength);

        RuleFor(c => c.ReadingDateFrom).NotEmpty();
        RuleFor(c => c.ReadingDateTo)
            .NotEmpty()
            .GreaterThan(c => c.ReadingDateFrom)
            .WithMessage(DomainConstants.ValidationMessages.InvoicePeriodInvalid);

        RuleFor(c => c.ConsumptionAmount).GreaterThanOrEqualTo(0);
        RuleFor(c => c.ConsumptionVat).GreaterThanOrEqualTo(0);
        RuleFor(c => c.DistributionAmount).GreaterThanOrEqualTo(0);
        RuleFor(c => c.DistributionVat).GreaterThanOrEqualTo(0);

        RuleFor(c => c.TotalAmount).GreaterThan(0)
            .WithMessage(DomainConstants.ValidationMessages.InvoiceTotalAmountMustBePositive);

        // Kalem tutarları yalnızca denetim amaçlı saklanır; Akmercan gösteriminde geçişme bedeli
        // KDV dahil olabildiği için kalem toplamı zorunlu olarak eşitlenmez.
    }
}
