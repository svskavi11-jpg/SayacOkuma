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
    DateOnly AsOfDate
);

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
    IReadOnlyList<AllocationDetail> Allocations
);

public record AllocationDetail(
    Guid PaymentId,
    DateOnly PaymentDate,
    decimal AllocatedAmount,
    AllocationTarget Target
);
