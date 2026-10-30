namespace Westalife.Domain.Services.Ledger;

/// <summary>
/// Bir dairenin tek dönem borcunu temsil eder (Ledger satırı).
/// Faiz hesaplaması bu record üzerinden yapılır; veritabanına yazılmaz.
/// </summary>
public record LedgerDebt(
    /// <summary>Satır tipi (devir anapara / devir faiz / fatura / ileride Aidat vb.)</summary>
    LedgerDebtType Type,

    /// <summary>
    /// Kaynak kayıt kimliği (string — sentez ID destekler):
    /// fatura için "ApartmentInvoice.Id", devir için "apt:{ApartmentId}:principal" / "apt:{ApartmentId}:interest".
    /// Aynı dairenin iki devir satırı (anapara + faiz) ayrı kimlik taşımak zorundadır.
    /// </summary>
    string SourceId,

    /// <summary>Borcun vade tarihi (faiz bu tarihten işler)</summary>
    DateOnly DueDate,

    /// <summary>Orijinal anapara</summary>
    decimal PrincipalAmount,

    /// <summary>Dönem açıklaması (örn: "Ocak 2026 - Şubat 2026")</summary>
    string? Label = null
);

/// <summary>
/// Borç tipi. GELECEK İÇİN ESNEK: yeni borç tipleri (örn. Aidat = 4) buraya eklenip
/// BuildDebtList'te satır olarak üretildiğinde FIFO + faiz motoru otomatik işler.
/// </summary>
public enum LedgerDebtType
{
    /// <summary>Devir bakiyesi — ANAPARA kısmı (faiz işler).</summary>
    OpeningBalancePrincipal = 1,

    /// <summary>Fatura dönem borcu (faiz işler).</summary>
    Invoice = 2,

    /// <summary>Devir ile gelen geçmiş dönem faizi (kapalı borç — faiz İŞLEMEZ).</summary>
    OpeningBalanceInterest = 3,

    /// <summary>Aidat (ortak gider) dönem borcu — Faz 6.9 (faiz işler).</summary>
    Aidat = 4
}

