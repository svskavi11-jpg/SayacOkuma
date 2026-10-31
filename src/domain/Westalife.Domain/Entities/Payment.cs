using Westalife.Domain.Common;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;
using Westalife.Domain.Services.Ledger;

namespace Westalife.Domain.Entities;

/// <summary>
/// Bir daireden tahsil edilen ödeme (nakit giriş). Soft-delete iptal mekanizması:
/// IsCancelled = true + CancelledOn + CancellationReason ile iptal edilir;
/// fiziksel silme YASAKTIR (denetim izi).
/// <para>
/// Allocation motoru: ödeme önce en eski devir bakiyesine, sonra en eski ApartmentInvoice'a
/// (DueDate sırasına göre) düşürülür (FIFO kronolojik). Faiz birikimi ayrı "virtual borç"
/// olarak Ledger sorgu katmanında hesaplanır — tabloya yazılmaz (basit faiz kural #2).
/// </para>
/// </summary>
public class Payment : BaseEntity
{
    public Guid ApartmentId { get; private set; }

    public Apartment Apartment { get; private set; } = null!;

    /// <summary>Tahsilat tarihi (kullanıcı girişi, yerel gün). Geleceğe atanamaz.</summary>
    public DateOnly PaymentDate { get; private set; }

    /// <summary>Tahsil edilen tutar (TL, kuruş hassasiyetiyle).</summary>
    public decimal Amount { get; private set; }

    /// <summary>Açıklama / dekont no gibi serbest metin. İsteğe bağlı.</summary>
    public string? Description { get; private set; }

    /// <summary>Tahsilatı giren görevli. Faz 7'de JWT claim'den okunacak.</summary>
    public string ReceivedBy { get; private set; } = string.Empty;

    // ── Faz 7.1: Hedefli tahsilat + Hibrit Model sahiplik ────────────────────
    /// <summary>Ödemeyi yapan sakin (SCD çözümü — kiracı varsa kiracı, yoksa ev sahibi). Opsiyonel.</summary>
    public Guid? ApartmentOwnerId { get; private set; }

    public ApartmentOwner? ApartmentOwner { get; private set; }

    /// <summary>Müteselsil sorumlu ev sahibi (KMK m.22 — yapan kiracıysa doldurulur). Opsiyonel.</summary>
    public Guid? LegalResponsibleOwnerId { get; private set; }

    public ApartmentOwner? LegalResponsibleOwner { get; private set; }

    /// <summary>
    /// Hedef borç türü (Aidat/Invoice/OpeningBalancePrincipal) — belirtilirse FIFO tahsilatı
    /// yalnızca bu türdeki borçlara düşer; null ise tüm borçlara normal FIFO uygulanır.
    /// </summary>
    public LedgerDebtType? TargetDebtType { get; private set; }

    // ── İptal alanları (soft-delete) ──────────────────────────────────────────
    public bool IsCancelled { get; private set; }

    /// <summary>İptal zamanı (UTC). Ledger motorunda bu kayıt dikkate alınmaz.</summary>
    public DateTimeOffset? CancelledOn { get; private set; }

    /// <summary>İptal gerekçesi (denetim izi için zorunlu).</summary>
    public string? CancellationReason { get; private set; }

    // ── Navigation ────────────────────────────────────────────────────────────
    private readonly List<PaymentAllocation> _allocations = [];
    public IReadOnlyCollection<PaymentAllocation> Allocations => _allocations.AsReadOnly();

    private Payment() { } // EF Core

    public Payment(
        Guid apartmentId,
        DateOnly paymentDate,
        decimal amount,
        string? description,
        string? receivedBy,
        Guid? apartmentOwnerId = null,
        Guid? legalResponsibleOwnerId = null,
        LedgerDebtType? targetDebtType = null)
    {
        if (apartmentId == Guid.Empty)
            throw new ArgumentException("Daire kimliği (apartmentId) zorunludur.", nameof(apartmentId));

        if (amount <= 0m)
            throw new ArgumentException(DomainConstants.Payment.AmountMustBePositive, nameof(amount));

        if (amount > DomainConstants.Payment.AmountMax)
            throw new ArgumentException(DomainConstants.Payment.AmountTooLarge, nameof(amount));

        if (paymentDate > DateOnly.FromDateTime(DateTime.Today))
            throw new ArgumentException(DomainConstants.Payment.PaymentDateFuture, nameof(paymentDate));

        ApartmentId = apartmentId;
        PaymentDate = paymentDate;
        Amount = decimal.Round(amount, 2);
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        ReceivedBy = string.IsNullOrWhiteSpace(receivedBy)
            ? DomainConstants.Payment.DefaultReceivedBy
            : receivedBy.Trim();
        ApartmentOwnerId = apartmentOwnerId;
        LegalResponsibleOwnerId = legalResponsibleOwnerId;
        TargetDebtType = targetDebtType;
    }

    /// <summary>
    /// Tahsilatı iptal eder (soft-delete). Zaten iptal edilmişse DomainException fırlatır.
    /// İptal ile birlikte tüm allocation satırları da otomatik pasifleştirilir.
    /// </summary>
    public void Cancel(string reason)
    {
        if (IsCancelled)
            throw new DomainException(DomainConstants.Payment.AlreadyCancelled);

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException(DomainConstants.Payment.CancellationReasonRequired, nameof(reason));

        if (reason.Length > DomainConstants.Payment.CancellationReasonMaxLength)
            throw new ArgumentException(
                $"İptal gerekçesi en fazla {DomainConstants.Payment.CancellationReasonMaxLength} karakter olabilir.",
                nameof(reason));

        IsCancelled = true;
        CancelledOn = DateTimeOffset.UtcNow;
        CancellationReason = reason.Trim();
        LastModifiedOn = DateTimeOffset.UtcNow;

        // Tüm allocation satırlarını pasifleştir — allocation'lar da denetim için fiziksel silinmez.
        foreach (var alloc in _allocations)
            alloc.Void();
    }

    /// <summary>Allocation ekler. Toplam allocation, Payment.Amount'u aşamaz.</summary>
    internal void AddAllocation(PaymentAllocation allocation)
    {
        var currentTotal = _allocations
            .Where(a => !a.IsVoided)
            .Sum(a => a.AllocatedAmount);

        if (currentTotal + allocation.AllocatedAmount > Amount + 0.005m) // kuruş toleransı
            throw new DomainException(DomainConstants.PaymentAllocation.AllocationExceedsPayment);

        _allocations.Add(allocation);
    }
}
