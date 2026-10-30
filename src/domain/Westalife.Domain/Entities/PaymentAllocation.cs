using Westalife.Domain.Common;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Domain.Entities;

/// <summary>
/// Bir ödeme ile bir borç satırı arasındaki eşleştirme (allocation).
/// Allocation tipi:
/// <list type="bullet">
///   <item><term>OpeningBalance</term><description>Devir (açılış) bakiyesine düşürüldü</description></item>
///   <item><term>Invoice</term><description>Belirli bir ApartmentInvoice satırına düşürüldü</description></item>
///   <item><term>Interest</term><description>Hesaplanan gecikme faizine düşürüldü (Ledger motoru üretir)</description></item>
/// </list>
/// Fiziksel silme yasaktır — Payment iptal edilince IsVoided = true ile pasifleşir.
/// </summary>
public class PaymentAllocation : BaseEntity
{
    public Guid PaymentId { get; private set; }

    public Payment Payment { get; private set; } = null!;

    /// <summary>Hangi tür borca düşürüldüğü.</summary>
    public AllocationTarget Target { get; private set; }

    /// <summary>
    /// Invoice tipinde eşleştirmede hedef ApartmentInvoice. OpeningBalance veya Interest tipinde null.
    /// </summary>
    public Guid? ApartmentInvoiceId { get; private set; }

    public ApartmentInvoice? ApartmentInvoice { get; private set; }

    /// <summary>
    /// Aidat tipinde eşleştirmede hedef AidatCharge (Faz 6.9). Diğer tiplerde null.
    /// </summary>
    public Guid? AidatChargeId { get; private set; }

    public AidatCharge? AidatCharge { get; private set; }

    /// <summary>Bu allocation satırıyla düşürülen tutar (TL).</summary>
    public decimal AllocatedAmount { get; private set; }

    /// <summary>Payment iptal edilince true yapılır — fiziksel silme yerine soft void.</summary>
    public bool IsVoided { get; private set; }

    public DateTimeOffset? VoidedOn { get; private set; }

    private PaymentAllocation() { } // EF Core

    public PaymentAllocation(
        Guid paymentId,
        AllocationTarget target,
        decimal allocatedAmount,
        Guid? apartmentInvoiceId = null,
        Guid? aidatChargeId = null)
    {
        if (allocatedAmount <= 0m)
            throw new ArgumentException(DomainConstants.PaymentAllocation.AmountMustBePositive, nameof(allocatedAmount));

        if (target == AllocationTarget.Invoice && apartmentInvoiceId is null)
            throw new ArgumentException("Invoice allocation için ApartmentInvoiceId zorunludur.", nameof(apartmentInvoiceId));

        if (target == AllocationTarget.Aidat && aidatChargeId is null)
            throw new ArgumentException(DomainConstants.ValidationMessages.AidatAllocationChargeIdRequired, nameof(aidatChargeId));

        PaymentId = paymentId;
        Target = target;
        AllocatedAmount = decimal.Round(allocatedAmount, 2);
        ApartmentInvoiceId = apartmentInvoiceId;
        AidatChargeId = aidatChargeId;
    }

    /// <summary>Payment iptal edilince tüm allocation'ları pasifleştirmek için çağrılır.</summary>
    internal void Void()
    {
        IsVoided = true;
        VoidedOn = DateTimeOffset.UtcNow;
        LastModifiedOn = DateTimeOffset.UtcNow;
    }
}

/// <summary>Eşleştirme hedef tipi.</summary>
public enum AllocationTarget
{
    /// <summary>Devir (açılış) bakiyesine düşürüldü.</summary>
    OpeningBalance = 1,

    /// <summary>Belirli bir fatura dönemine düşürüldü (ApartmentInvoice).</summary>
    Invoice = 2,

    /// <summary>Hesaplanan gecikme faizine düşürüldü.</summary>
    Interest = 3,

    /// <summary>Aidat (ortak gider) dönem borcuna düşürüldü (AidatCharge) — Faz 6.9.</summary>
    Aidat = 4
}
