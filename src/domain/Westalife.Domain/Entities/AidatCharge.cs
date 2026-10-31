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

    // ── Faz 7.1: Hibrit Model borç sahipliği (KMK m.22) ──────────────────────
    /// <summary>Borç sahibi sakin (kiracı varsa kiracı, yoksa ev sahibi — tahakkuk anındaki SCD çözümü).</summary>
    public Guid? ApartmentOwnerId { get; private set; }

    public ApartmentOwner? ApartmentOwner { get; private set; }

    /// <summary>Müteselsil sorumlu ev sahibi (KMK m.22 — borç kiracıya yazıldıysa doldurulur).</summary>
    public Guid? LegalResponsibleOwnerId { get; private set; }

    public ApartmentOwner? LegalResponsibleOwner { get; private set; }

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

    /// <summary>
    /// Faz 7.1: Borç sahipliğini atar (Hibrit Model). Primary = kiracı (varsa) / ev sahibi;
    /// LegalResponsible = müteselsil sorumlu ev sahibi (KMK m.22).
    /// </summary>
    internal void AssignOwner(Guid? apartmentOwnerId, Guid? legalResponsibleOwnerId)
    {
        ApartmentOwnerId = apartmentOwnerId;
        LegalResponsibleOwnerId = legalResponsibleOwnerId;
        LastModifiedOn = DateTimeOffset.UtcNow;
    }
}
