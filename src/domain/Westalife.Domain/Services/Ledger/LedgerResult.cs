using Westalife.Domain.Entities;

namespace Westalife.Domain.Services.Ledger;

/// <summary>
/// Cari hesap (Ledger) sonucu — tek dairenin tüm borç/ödeme/faiz özeti.
/// Veritabanına yazılmaz; sorgu anında hesaplanır.
/// </summary>
public record LedgerResult(
    Guid ApartmentId,
    string ApartmentTitle,
    string DoorNumber,

    /// <summary>Tüm borç satırları (açılış + faturalar). Kronolojik sırada.</summary>
    IReadOnlyList<LedgerLine> Lines,

    /// <summary>Toplam anapara borcu (ödenmemiş).</summary>
    decimal TotalPrincipalDue,

    /// <summary>Hesaplama tarihine kadar birikmiş toplam gecikme faizi.</summary>
    decimal TotalInterestAccrued,

    /// <summary>Toplam bakiye = Anapara + Faiz. Pozitif = borçlu, negatif = alacaklı.</summary>
    decimal TotalBalance,

    /// <summary>Hesaplamanın yapıldığı tarih.</summary>
    DateOnly AsOfDate,

    /// <summary>Borç türü bazlı özet (Faz 7.1) — cari hesap ekranındaki özet kartlarının kaynağı.</summary>
    LedgerSummary? Summary = null,

    /// <summary>
    /// Borca mahsup edilemeyen tahsilat artığı. Aidat tahakkuku silinince hedefi aidat olan
    /// ödeme burada kalır ve bakiyeyi alacağa çeker.
    /// </summary>
    IReadOnlyList<LedgerCredit>? Credits = null
);

/// <summary>Borca bağlanamayan tahsilat artığı.</summary>
public record LedgerCredit(
    Guid PaymentId,
    DateOnly PaymentDate,
    decimal Amount,
    LedgerDebtType? TargetDebtType,
    string? PerformedBy = null);

/// <summary>Cari hesap tek satırı.</summary>
public record LedgerLine(
    LedgerDebtType Type,

    /// <summary>Kaynak kayıt kimliği (string — fatura ID veya sentez: "apt:{id}:principal" / "apt:{id}:interest")</summary>
    string SourceId,

    string? Label,

    DateOnly DueDate,

    /// <summary>Orijinal anapara.</summary>
    decimal PrincipalAmount,

    /// <summary>Bu borca düşürülen toplam ödeme.</summary>
    decimal PaidAmount,

    /// <summary>Kalan anapara = Principal - Paid. Negatif olursa fazla ödeme alacağa geçer.</summary>
    decimal RemainingPrincipal,

    /// <summary>
    /// AsOfDate itibarıyla biriken faiz (yalnızca kalan anapara üzerinden, basit faiz).
    /// Faiz üzerine faiz hesaplanmaz (kural #2).
    /// </summary>
    decimal InterestAccrued,

    /// <summary>Satır toplamı = RemainingPrincipal + InterestAccrued.</summary>
    decimal LineBalance,

    /// <summary>Bu borca ait allocation detayları.</summary>
    IReadOnlyList<AllocationDetail> Allocations,

    /// <summary>Borç sahibi sakin (Faz 7.1 — Hibrit Model). Devir bakiyesinde NULL.</summary>
    Guid? OwnerId = null,

    /// <summary>Borç sahibi sakinin adı (UI Sakin Seçici/etiketleri için).</summary>
    string? OwnerName = null,

    /// <summary>Aidat dönem başlangıcı. Cari hesap tarih sütunu bunu kullanır; faiz vadesi DueDate'tir.</summary>
    DateOnly? PeriodStart = null,

    /// <summary>İptal edilmiş tahakkuk satırı mı? (Denetim izi: tutar 0'a düşürülmüştür).</summary>
    bool IsCancelled = false,

    /// <summary>İptal edilmiş satırın orijinal tahakkuk tutarı (denetim izi için).</summary>
    decimal? OriginalPrincipalAmount = null
);

/// <summary>
/// Cari hesap özeti — borç türü bazında anapara + faiz ayrımı (Faz 7.1).
/// UI özet kartları ve Geçmiş Sakinler ekranı bu yapıyı kullanır.
/// </summary>
public record LedgerSummary(
    /// <summary>Aidat kalan anapara.</summary>
    decimal AidatPrincipalDue,

    /// <summary>Aidat birikmiş gecikme faizi.</summary>
    decimal AidatInterestAccrued,

    /// <summary>Isınma (doğalgaz faturası) kalan anapara.</summary>
    decimal InvoicePrincipalDue,

    /// <summary>Isınma birikmiş gecikme faizi.</summary>
    decimal InvoiceInterestAccrued,

    /// <summary>Devir anapara kalanı.</summary>
    decimal OpeningBalancePrincipalDue,

    /// <summary>Devir ile gelen geçmiş faiz kalanı (faiz işlemez).</summary>
    decimal OpeningBalanceInterestDue);

public record AllocationDetail(
    Guid PaymentId,
    DateOnly PaymentDate,
    decimal AllocatedAmount,
    AllocationTarget Target,
    string? PerformedBy = null
);
