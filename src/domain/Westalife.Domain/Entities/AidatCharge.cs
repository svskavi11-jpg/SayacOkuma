using Westalife.Domain.Common;
using Westalife.Domain.Constants;

namespace Westalife.Domain.Entities;

/// <summary>
/// Aidat tahakkukunun tek daire satırı (Faz 6.9). Cari hesap (Ledger) motorunda
/// <see cref="LedgerDebtType.Aidat"/> borcu olarak okunur; FIFO tahsilat ve
/// günlük faiz motoruna dahildir. Fiziksel silme yoktur — tahakkuk dönemi denetim izidir.
/// </summary>
public class AidatCharge : BaseEntity
{
    public Guid AidatInvoiceId { get; private set; }

    public AidatInvoice AidatInvoice { get; private set; } = null!;

    public Guid ApartmentId { get; private set; }

    public Apartment Apartment { get; private set; } = null!;

    /// <summary>Dairenin dönem aidat borcu (TL, kuruş hassasiyetiyle).</summary>
    public decimal Amount { get; private set; }

    private AidatCharge() { } // EF Core

    internal AidatCharge(Guid aidatInvoiceId, Guid apartmentId, decimal amount)
    {
        if (aidatInvoiceId == Guid.Empty)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.AidatInvoiceIdRequired, nameof(aidatInvoiceId));
        }

        if (apartmentId == Guid.Empty)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.AidatChargeApartmentRequired, nameof(apartmentId));
        }

        if (amount <= 0m)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.AidatChargeAmountMustBePositive, nameof(amount));
        }

        AidatInvoiceId = aidatInvoiceId;
        ApartmentId = apartmentId;
        Amount = decimal.Round(amount, 2);
    }
}
