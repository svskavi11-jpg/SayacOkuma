using System.Globalization;
using Westalife.Domain.Entities;

namespace Westalife.Domain.Services.Ledger;

/// <summary>
/// Cari hesap (Ledger) ve faiz hesaplama motoru.
/// <para>
/// Kurallar:
/// <list type="bullet">
///   <item>Basit faiz: Anapara × Oran/100 × Gün/365 (günlük)</item>
///   <item>Faiz üzerine faiz YOK (bileşik faiz yok)</item>
///   <item>Ödeme FIFO: önce en eski devir bakiyesine, sonra en eski faturaya</item>
///   <item>Kısmi ödeme: anapara önce düşer, kalan faize düşer</item>
///   <item>İptal edilmiş tahsilatlar (IsCancelled) hiç dikkate alınmaz</item>
/// </list>
/// </para>
/// </summary>
public static class LedgerService
{
    /// <summary>
    /// Dairenin <paramref name="asOfDate"/> tarihine kadar olan cari hesabını hesaplar.
    /// </summary>
    /// <param name="apartment">Daire (Navigation olmak zorunda değil — sadece bakiye alanları)</param>
    /// <param name="invoices">Dairenin aktif ApartmentInvoice satırları (IsSuperseded=false)</param>
    /// <param name="payments">Dairenin tüm ödeme kayıtları (IsCancelled dahil — filtre burada yapılır)</param>
    /// <param name="annualInterestRate">Yıllık faiz oranı (%, örn: 24.0 → %24)</param>
    /// <param name="asOfDate">Hesaplama günü (genellikle bugün)</param>
    public static LedgerResult Calculate(
        Apartment apartment,
        IEnumerable<ApartmentInvoice> invoices,
        IEnumerable<Payment> payments,
        decimal annualInterestRate,
        DateOnly asOfDate)
    {
        // ── 1. Borç satırlarını hazırla (kronolojik sıra) ─────────────────────
        var debts = BuildDebtList(apartment, invoices);

        // ── 2. Aktif ödemeleri FIFO sıralamasıyla işle ────────────────────────
        var activePayments = payments
            .Where(p => !p.IsCancelled)
            .OrderBy(p => p.PaymentDate)
            .ThenBy(p => p.CreatedOn)
            .ToList();

        // Her borç için kalan anapara (mutable)
        var remainingPrincipal = debts.ToDictionary(d => d.SourceId, d => d.PrincipalAmount);

        // Allocation detayları (borç ID → listesi)
        var allocationMap = debts.ToDictionary(d => d.SourceId, _ => new List<AllocationDetail>());

        foreach (var payment in activePayments)
        {
            var availableAmount = payment.Amount;

            // FIFO: en eski borçtan başla
            foreach (var debt in debts)
            {
                if (availableAmount <= 0m) break;

                var remaining = remainingPrincipal[debt.SourceId];
                if (remaining <= 0m) continue;

                var toAllocate = Math.Min(availableAmount, remaining);
                remainingPrincipal[debt.SourceId] -= toAllocate;
                availableAmount -= toAllocate;

                var target = debt.Type switch
                {
                    // Devir ANAPARA hedefi: AllocationTarget.OpeningBalance (int=1, geçmiş uyumlu).
                    LedgerDebtType.OpeningBalancePrincipal => AllocationTarget.OpeningBalance,
                    // Devir FAİZ hedefi: AllocationTarget.Interest (int=3).
                    LedgerDebtType.OpeningBalanceInterest => AllocationTarget.Interest,
                    // Aidat hedefi: AllocationTarget.Aidat (int=4) — Faz 6.9.
                    LedgerDebtType.Aidat => AllocationTarget.Aidat,
                    _ => AllocationTarget.Invoice,
                };

                allocationMap[debt.SourceId].Add(new AllocationDetail(
                    payment.Id,
                    payment.PaymentDate,
                    toAllocate,
                    target));
            }

            // Eğer hâlâ ödeme artığı varsa → alacak olarak son satıra negatif olarak eklenir
            // (Fazla ödeme durumu ledger'da TotalBalance negatifle gösterilir)
        }

        // ── 3. Faiz hesapla ───────────────────────────────────────────────────
        var lines = new List<LedgerLine>();

        foreach (var debt in debts)
        {
            var principal = debt.PrincipalAmount;
            var paid = debt.PrincipalAmount - remainingPrincipal[debt.SourceId];
            paid = Math.Max(0m, paid); // negatif olmasın
            var remaining = remainingPrincipal[debt.SourceId];

            var interest = AccruesInterest(debt.Type)
                ? CalculateSimpleInterest(
                    remainingPrincipal: Math.Max(0m, remaining),
                    annualRate: annualInterestRate,
                    dueDate: debt.DueDate,
                    asOfDate: asOfDate)
                : 0m; // Devreden geçmiş faiz gibi "kapalı" borçlara faiz İŞLEMEZ

            lines.Add(new LedgerLine(
                Type: debt.Type,
                SourceId: debt.SourceId,
                Label: debt.Label,
                DueDate: debt.DueDate,
                PrincipalAmount: principal,
                PaidAmount: paid,
                RemainingPrincipal: remaining,
                InterestAccrued: interest,
                LineBalance: remaining + interest,
                Allocations: allocationMap[debt.SourceId].AsReadOnly()));
        }

        var totalPrincipalDue = lines.Sum(l => Math.Max(0m, l.RemainingPrincipal));
        var totalInterest = lines.Sum(l => l.InterestAccrued);
        var totalBalance = lines.Sum(l => l.LineBalance);

        return new LedgerResult(
            ApartmentId: apartment.Id,
            ApartmentTitle: apartment.Title,
            DoorNumber: apartment.DoorNumber,
            Lines: lines.AsReadOnly(),
            TotalPrincipalDue: totalPrincipalDue,
            TotalInterestAccrued: totalInterest,
            TotalBalance: totalBalance,
            AsOfDate: asOfDate);
    }

    // ── Yardımcı metodlar ─────────────────────────────────────────────────────

    private static List<LedgerDebt> BuildDebtList(
        Apartment apartment,
        IEnumerable<ApartmentInvoice> invoices)
    {
        var debts = new List<LedgerDebt>();

        // Devir bakiyesi (varsa) — faturalardan ÖNCE gelir. FIFO'da ÖNCE Devir Faiz,
        // SONRA Devir Anapara kapatılır; sonrasında faturalar (vade sırası).
        // Not: Faiz satırı "kapalı borçtur" — ona faiz işlenmez (AccruesInterest=false).
        if (apartment.OpeningBalanceInterest != 0m && apartment.OpeningBalanceDate.HasValue)
        {
            debts.Add(new LedgerDebt(
                Type: LedgerDebtType.OpeningBalanceInterest,
                SourceId: $"apt:{apartment.Id}:interest",
                DueDate: apartment.OpeningBalanceDate.Value,
                PrincipalAmount: apartment.OpeningBalanceInterest,
                Label: "Devreden Geçmiş Faiz"));
        }

        if (apartment.OpeningBalancePrincipal != 0m && apartment.OpeningBalanceDate.HasValue)
        {
            debts.Add(new LedgerDebt(
                Type: LedgerDebtType.OpeningBalancePrincipal,
                SourceId: $"apt:{apartment.Id}:principal",
                DueDate: apartment.OpeningBalanceDate.Value,
                PrincipalAmount: apartment.OpeningBalancePrincipal,
                Label: "Devir Anapara"));
        }

        // Fatura satırları — vade tarihine göre kronolojik
        var invoiceDebts = invoices
            .Where(i => !i.IsSuperseded)
            .OrderBy(i => i.GasInvoice?.DueDate ?? DateOnly.MinValue)
            .ThenBy(i => i.CreatedOn)
            .Select(i => new LedgerDebt(
                Type: LedgerDebtType.Invoice,
                SourceId: i.Id.ToString(),
                DueDate: i.GasInvoice?.DueDate ?? DateOnly.FromDateTime(i.CalculatedOnUtc.DateTime),
                PrincipalAmount: i.TotalAmount,
                Label: BuildInvoiceLabel(i)));

        debts.AddRange(invoiceDebts);
        return debts;
    }

    private static string BuildInvoiceLabel(ApartmentInvoice invoice)
    {
        if (invoice.GasInvoice is null) return "Fatura";

        // Türkçe ay kısaltmaları (Nis 2026) — cari hesap ve PDF dökümünde tutarlı görünüm.
        var culture = CultureInfo.GetCultureInfo("tr-TR");
        var from = invoice.GasInvoice.ReadingDateFrom.ToString("MMM yyyy", culture);
        var to = invoice.GasInvoice.ReadingDateTo.ToString("MMM yyyy", culture);
        return from == to ? from : $"{from} – {to}";
    }

    /// <summary>
    /// Basit faiz: Anapara × Oran/100 × Gün/365.
    /// Vade tarihinden SONRA başlar. Vade geçmemişse 0 döner.
    /// Oran 0 ise veya anapara 0 ise 0 döner.
    /// </summary>
    public static decimal CalculateSimpleInterest(
        decimal remainingPrincipal,
        decimal annualRate,
        DateOnly dueDate,
        DateOnly asOfDate)
    {
        if (remainingPrincipal <= 0m || annualRate <= 0m) return 0m;
        if (asOfDate <= dueDate) return 0m;

        var days = asOfDate.DayNumber - dueDate.DayNumber;
        var interest = remainingPrincipal * (annualRate / 100m) * (days / 365m);
        return decimal.Round(interest, 2);
    }

    /// <summary>
    /// Bu borç tipine faiz işler mi? Kural: faiz üzerine faiz YASAK —
    /// devir ile gelen geçmiş faiz (OpeningBalanceInterest) faiz işlemez.
    /// Gelecekte eklenecek yeni borç tipleri (Aidat vb.) varsayılan olarak faiz işler.
    /// </summary>
    public static bool AccruesInterest(LedgerDebtType type) =>
        type != LedgerDebtType.OpeningBalanceInterest;
}
