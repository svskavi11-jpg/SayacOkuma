using System.Globalization;
using Westalife.Domain.Entities;
using Westalife.Domain.Services.Ledger;

namespace Westalife.Application.Features.Notifications;

/// <summary>
/// Şablondaki yer tutucuları dairenin verisiyle değiştirir.
/// [Tutar] toplam cari bakiyedir. Aidat ve ısınma borçları devri içermez.
/// Dönem tahakkukları ödemeden düşülmeden, içinde bulunulan takvim ayından alınır.
/// </summary>
public static class NotificationPlaceholderRenderer
{
    public static IReadOnlyList<NotificationPlaceholderItem> Catalog { get; } =
    [
        new("[AdSoyad]", "Aktif kiracı; yoksa ev sahibi.", "Ayşe Yılmaz"),
        new("[DaireNo]", "Dairenin kapı numarası.", "12"),
        new("[Tutar]", "Güncel cari bakiye. Devir, aidat ve ısınma birlikte.", "4.636,73 TL"),
        new("[Donem]", "Aktif Yönetim Dönemi. Tanımlı değilse içinde bulunulan ay.", "01.06.2026 – 31.05.2027"),
        new("[MevcutAy]", "İçinde bulunulan takvim ayı.", "Kasım 2026"),
        new("[AidatBorc]", "Kalan aidat anapara ve faizi. Devir dahil değildir.", "3.000,00 TL"),
        new("[IsinmaBorc]", "Kalan ısınma anapara ve faizi. Devir dahil değildir.", "2.000,00 TL"),
        new("[BuDonemAidat]", "İçinde bulunulan aya tahakkuk eden aidat. Ödeme düşülmez.", "1.500,00 TL"),
        new("[BuDonemIsinma]", "İçinde bulunulan aya tahakkuk eden ısınma. Ödeme düşülmez.", "800,00 TL"),
        new("[PdfLink]", "SMS için güvenli PDF indirme linki. E-posta/WhatsApp'ta ek olarak gider.", "https://ornek/api/v1/reports/notice.pdf?token=…"),
    ];

    public static string Apply(string? template, NotificationPlaceholderValues values)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        return template
            .Replace("[AdSoyad]", values.FullName, StringComparison.Ordinal)
            .Replace("[DaireNo]", values.DoorNumber, StringComparison.Ordinal)
            .Replace("[Tutar]", FormatMoney(values.Balance), StringComparison.Ordinal)
            .Replace("[AidatBorc]", FormatMoney(values.AidatDebt), StringComparison.Ordinal)
            .Replace("[IsinmaBorc]", FormatMoney(values.HeatingDebt), StringComparison.Ordinal)
            .Replace("[BuDonemAidat]", FormatMoney(values.CurrentAidat), StringComparison.Ordinal)
            .Replace("[BuDonemIsinma]", FormatMoney(values.CurrentHeating), StringComparison.Ordinal)
            .Replace("[MevcutAy]", values.CurrentMonthLabel, StringComparison.Ordinal)
            .Replace("[PdfLink]", values.PdfLink ?? string.Empty, StringComparison.Ordinal)
            .Replace("[Donem]", values.PeriodLabel, StringComparison.Ordinal);
    }

    /// <summary>
    /// Kalan aidat ve ısınma borçlarını ledger özetinden, dönem tahakkuklarını ise
    /// ödemeye bakmadan içinde bulunulan aydan okur.
    /// </summary>
    public static NotificationMoneyParts ReadAmounts(
        LedgerResult ledger,
        IEnumerable<AidatCharge> charges,
        IEnumerable<ApartmentInvoice> invoices,
        DateOnly asOf)
    {
        var summary = ledger.Summary;
        var aidatDebt = (summary?.AidatPrincipalDue ?? 0m) + (summary?.AidatInterestAccrued ?? 0m);
        var heatingDebt = (summary?.InvoicePrincipalDue ?? 0m) + (summary?.InvoiceInterestAccrued ?? 0m);

        var aidatAccrual = charges
            .Where(c => c.AidatInvoice is { IsCancelled: false })
            .Select(c => new NotificationAccrual(c.AidatInvoice.PeriodStart, c.AidatInvoice.PeriodEnd, c.Amount));
        var heatingAccrual = invoices
            .Where(i => !i.IsSuperseded)
            .Select(ToHeatingAccrual);

        return new NotificationMoneyParts(
            aidatDebt,
            heatingDebt,
            SumAccrual(aidatAccrual, asOf),
            SumAccrual(heatingAccrual, asOf));
    }

    public static decimal SumAccrual(IEnumerable<NotificationAccrual> items, DateOnly asOf) =>
        items.Where(item => OverlapsMonth(item.Start, item.End, asOf)).Sum(item => item.Amount);

    public static bool OverlapsMonth(DateOnly start, DateOnly end, DateOnly asOf)
    {
        if (end < start)
        {
            return false;
        }

        var monthStart = new DateOnly(asOf.Year, asOf.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        return start <= monthEnd && end >= monthStart;
    }

    private static NotificationAccrual ToHeatingAccrual(ApartmentInvoice invoice)
    {
        if (invoice.GasInvoice is { } gas)
        {
            return new NotificationAccrual(DateOf(gas.ReadingDateFrom), DateOf(gas.ReadingDateTo), invoice.TotalAmount);
        }

        var day = DateOf(invoice.CalculatedOnUtc);
        return new NotificationAccrual(day, day, invoice.TotalAmount);
    }

    private static DateOnly DateOf(DateTimeOffset value) => new(value.Year, value.Month, value.Day);

    public static string FormatMoney(decimal amount)
    {
        var culture = CultureInfo.GetCultureInfo("tr-TR");
        return string.Create(culture, $"{amount:N2} TL");
    }

    public static string FormatPeriod(DateOnly? start, DateOnly? end, DateOnly today)
    {
        var culture = CultureInfo.GetCultureInfo("tr-TR");
        if (start is { } periodStart && end is { } periodEnd)
        {
            return $"{periodStart.ToString("dd.MM.yyyy", culture)} – {periodEnd.ToString("dd.MM.yyyy", culture)}";
        }

        return FormatCurrentMonth(today);
    }

    public static string FormatCurrentMonth(DateOnly today) =>
        today.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("tr-TR"));
}

public readonly record struct NotificationPlaceholderValues(
    string FullName,
    string DoorNumber,
    decimal Balance,
    string PeriodLabel,
    decimal AidatDebt = 0m,
    decimal HeatingDebt = 0m,
    decimal CurrentAidat = 0m,
    decimal CurrentHeating = 0m,
    string CurrentMonthLabel = "",
    string? PdfLink = null);

public readonly record struct NotificationMoneyParts(
    decimal AidatDebt,
    decimal HeatingDebt,
    decimal CurrentAidat,
    decimal CurrentHeating);

/// <summary>Bir tahakkuk aralığı. İptal edilmiş satırlar listeye konmaz.</summary>
public readonly record struct NotificationAccrual(DateOnly Start, DateOnly End, decimal Amount);

public sealed record NotificationPlaceholderItem(string Token, string Description, string Example);
