using Westalife.Domain.Common;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Domain.Entities;

/// <summary>
/// Aidat dağıtım yöntemi (Faz 6.9).
/// </summary>
/// <summary>Aidat tahakkuk türü. Özel toplama bir fon hedefi taşır.</summary>
public enum AidatChargeType
{
    StandartAidat = 1,
    OzelToplama = 2
}

public enum AidatDistributionMethod
{
    /// <summary>TarifeBazlı: daireye atanmış <see cref="AidatTariff"/> tutarı kadar.</summary>
    TarifeBazli = 1,

    /// <summary>m²: dairenin aktif <see cref="ApartmentDimension"/> m²'sine oranla.</summary>
    Metrekare = 2,

    /// <summary>Eşit: tüm aktif dairelere eşit bölüşüm.</summary>
    Esit = 3
}

/// <summary>
/// Aidat tahakkuku (dönem başlığı) — Faz 6.9. Yönetici bir dönem için tahakkuk
/// oluşturduğunda her aktif daire için bir <see cref="AidatCharge"/> satırı açılır.
/// Toplam tutar, daire satırlarından türetilir (kuruş hassasiyeti, decimal).
/// </summary>
public class AidatInvoice : BaseEntity
{
    /// <summary>Dönem başlangıcı (dahil).</summary>
    public DateOnly PeriodStart { get; private set; }

    /// <summary>Dönem bitişi (dahil).</summary>
    public DateOnly PeriodEnd { get; private set; }

    /// <summary>Son ödeme tarihi (vade) — Ledger faiz motoru bu tarihten itibaren işler.</summary>
    public DateOnly DueDate { get; private set; }

    /// <summary>Borçların dairelere dağıtım yöntemi.</summary>
    public AidatDistributionMethod DistributionMethod { get; private set; }

    /// <summary>Dönemin toplam aidat borcu (daire satırlarının toplamı, TL).</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>Yönetici notu (isteğe bağlı).</summary>
    public string? Note { get; private set; }

    /// <summary>Standart aidat veya özel toplama (fon).</summary>
    public AidatChargeType ChargeType { get; private set; } = AidatChargeType.StandartAidat;

    /// <summary>Özel toplama adı. Örnek: Çatı Onarımı Fonu.</summary>
    public string? Title { get; private set; }

    /// <summary>Özel toplamanın hedef tutarı (TL). Tahsilat bu hedefe göre izlenir.</summary>
    public decimal? TargetAmount { get; private set; }

    /// <summary>Tahakkuk iptal edildi mi? (Soft delete — denetim izi için kayıt korunur).</summary>
    public bool IsCancelled { get; private set; }

    /// <summary>Tahakkukun iptal edildiği zaman (UTC).</summary>
    public DateTimeOffset? CancelledAt { get; private set; }

    private readonly List<AidatCharge> _charges = [];
    public IReadOnlyCollection<AidatCharge> Charges => _charges.AsReadOnly();

    private AidatInvoice() { } // EF Core

    public AidatInvoice(
        DateOnly periodStart,
        DateOnly periodEnd,
        DateOnly dueDate,
        AidatDistributionMethod distributionMethod,
        string? note = null)
    {
        if (periodEnd < periodStart)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.AidatPeriodInvalid, nameof(periodEnd));
        }

        if (note is { Length: > DomainConstants.AidatInvoice.NoteMaxLength })
        {
            throw new ArgumentException(
                $"Aidat notu en fazla {DomainConstants.AidatInvoice.NoteMaxLength} karakter olabilir.",
                nameof(note));
        }

        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        DueDate = dueDate;
        DistributionMethod = distributionMethod;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }

    /// <summary>Tahakkuku özel toplama fonuna çevirir. Hedef tutar daire borçlarından bağımsızdır.</summary>
    public void MarkAsSpecialCollection(string title, decimal targetAmount)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException(DomainConstants.AidatInvoice.FundTitleRequired, nameof(title));
        }

        var trimmed = title.Trim();
        if (trimmed.Length > DomainConstants.AidatInvoice.TitleMaxLength)
        {
            throw new ArgumentException(DomainConstants.AidatInvoice.FundTitleTooLong, nameof(title));
        }

        if (targetAmount <= 0m || targetAmount > DomainConstants.Budget.AmountMax)
        {
            throw new ArgumentException(DomainConstants.AidatInvoice.FundTargetRequired, nameof(targetAmount));
        }

        ChargeType = AidatChargeType.OzelToplama;
        Title = trimmed;
        TargetAmount = decimal.Round(targetAmount, DomainConstants.MoneyScale);
        LastModifiedOn = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Tek dairenin dönem aidat borcunu ekler ve toplam tutarı günceller.
    /// Kuruş hassasiyeti: tutar 2 haneye yuvarlanır, toplam kuruş kaybı olmadan birikir.
    /// </summary>
    public AidatCharge AddCharge(Guid apartmentId, decimal amount)
    {
        if (apartmentId == Guid.Empty)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.AidatChargeApartmentRequired, nameof(apartmentId));
        }

        if (amount <= 0m)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.AidatChargeAmountMustBePositive, nameof(amount));
        }

        var charge = new AidatCharge(Id, apartmentId, amount);
        charge.BindInvoice(this);
        _charges.Add(charge);
        TotalAmount = decimal.Round(TotalAmount + amount, 2);
        LastModifiedOn = DateTimeOffset.UtcNow;
        return charge;
    }

    /// <summary>
    /// Hatalı oluşturulan tahakkuku iptal eder (Soft Delete). Fiziksel silme yapılmaz;
    /// cari hesapta "İptal Edilmiş Aidat Tahakkuku" olarak 0 TL bakiye ile denetim izi kalır.
    /// </summary>
    public void Cancel(DateTimeOffset? cancelledAt = null)
    {
        if (IsCancelled)
        {
            throw new DomainException(DomainConstants.AidatInvoice.AlreadyCancelled);
        }

        IsCancelled = true;
        CancelledAt = cancelledAt ?? DateTimeOffset.UtcNow;
        LastModifiedOn = CancelledAt;
    }
}
