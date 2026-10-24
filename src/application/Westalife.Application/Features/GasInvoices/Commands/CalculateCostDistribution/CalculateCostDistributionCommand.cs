using MediatR;

namespace Westalife.Application.Features.GasInvoices.Commands.CalculateCostDistribution;

/// <summary>
/// Fatura dönemi bazlı 30/70 gider dağıtımını hesaplar ve ApartmentInvoice satırlarını yazar.
/// Yeniden hesaplamada eski satırlar Supersede edilir (audit trail — fiziksel silme yasak).
/// </summary>
public sealed record CalculateCostDistributionCommand(Guid GasInvoiceId)
    : IRequest<CalculateCostDistributionResult>;

public sealed record CalculateCostDistributionResult(
    int ApartmentCount,
    int FailedMeterCount,
    decimal TotalDistributedAmount,
    decimal InvoiceTotalAmount)
{
    public bool Reconciled => TotalDistributedAmount == InvoiceTotalAmount;
}
