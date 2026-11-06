using System.Globalization;
using Westalife.Domain.Entities;

namespace Westalife.Domain.Services.Ledger;

/// <summary>
/// Cari hesap (Ledger) ve faiz hesaplama motoru.
/// <para>
/// Kurallar:
/// <list type="bullet">
///   <item>Basit faiz: Anapara × Oran/100 × Gün/365 (günlük, parça parça)</item>
///   <item>Faiz üzerine faiz YOK (bileşik faiz yok)</item>
///   <item>Kronolojik faiz: Her ödeme kendi tarihinde anaparayı düşürür; faiz o tarihe
///         kadar hesaplanır ve sonraki dönemde kalan anapara üzerinden devam eder.</item>
///   <item>Aynı gün kuralı: Borç ve tahsilat aynı günse faiz 0'dır.</item>
///   <item>Ödeme FIFO: hedef borç türü yoksa önce en eski devir, sonra vade sırasıyla fatura ve aidat</item>
///   <item>Hedefli tahsilat: TargetDebtType doluysa yalnızca o türe mahsup (Devir hem anapara hem geçmiş faizi kapsar)</item>
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
        DateOnly asOfDate,
        IEnumerable<AidatCharge>? aidatCharges = null,
        IEnumerable<WaiverOffset>? waivers = null,
        bool cutOpeningAtAsOf = false)
    {
        // ── 1. Borç satırlarını hazırla (kronolojik sıra) ─────────────────────
        var debts = BuildDebtList(apartment, invoices, aidatCharges, cutOpeningAtAsOf ? asOfDate : null);

        // ── 2. Aktif ödemeleri FIFO sıralamasıyla işle ────────────────────────
        var activePayments = payments
            .Where(p => !p.IsCancelled)
            .OrderBy(p => p.PaymentDate)
            .ThenBy(p => p.CreatedOn)
            .ToList();

        // Her borç için kalan anapara (mutable). Görev mahsubu, nakit tahsilattan ve faizden önce düşer.
        var remainingPrincipal = debts.ToDictionary(d => d.SourceId, d => d.PrincipalAmount);
        var waiverApplied = ApplyWaivers(debts, remainingPrincipal, waivers);

        // Allocation detayları (borç ID → listesi)
        var allocationMap = debts.ToDictionary(d => d.SourceId, _ => new List<AllocationDetail>());
        var credits = new List<LedgerCredit>();

        // Negatif Devir Bakiyesi (Devir Alacağı): Yeni borçlar (fatura / aidat) geldiğinde
        // öncelikle bu devir alacağından FIFO sırasıyla mahsup edilir (faiz işlemesini de durdurur).
        var openingPrincipalDebt = debts.FirstOrDefault(
            d => d.Type == LedgerDebtType.OpeningBalancePrincipal && d.PrincipalAmount < 0m);
        if (openingPrincipalDebt is not null)
        {
            var availableCredit = -remainingPrincipal[openingPrincipalDebt.SourceId];
            var eligibleForOpeningCredit = debts
                .Where(d => d.Type is LedgerDebtType.Invoice or LedgerDebtType.Aidat)
                .Where(d => openingPrincipalDebt.OwnerId is null || d.OwnerId == openingPrincipalDebt.OwnerId)
                .ToList();

            foreach (var debt in eligibleForOpeningCredit)
            {
                if (availableCredit <= 0m) break;

                var remaining = remainingPrincipal[debt.SourceId];
                if (remaining <= 0m) continue;

                var toAllocate = Math.Min(availableCredit, remaining);
                remainingPrincipal[debt.SourceId] -= toAllocate;
                remainingPrincipal[openingPrincipalDebt.SourceId] += toAllocate; // -1000'den 0'a doğru yaklaşır
                availableCredit -= toAllocate;

                var target = debt.Type == LedgerDebtType.Aidat
                    ? AllocationTarget.Aidat
                    : AllocationTarget.Invoice;

                // Mahsup tarihi: borcun vadesinden önceyse vade tarihinde (0 gün faiz), sonraysa devir tarihinde
                var effectiveDate = openingPrincipalDebt.DueDate > debt.DueDate
                    ? openingPrincipalDebt.DueDate
                    : debt.DueDate;

                allocationMap[debt.SourceId].Add(new AllocationDetail(
                    Guid.Empty,
                    effectiveDate,
                    toAllocate,
                    target));
            }
        }

        foreach (var payment in activePayments)
        {
            var availableAmount = payment.Amount;

            // Hedefli tahsilat: tür veya sakin kısıtı varsa FIFO yalnızca o borçlara mahsup edilir.
            // Aidat doğalgaz faturasına, ısınma aidata dokunmaz. Devir hedefi anapara + geçmiş faizi kapsar.
            var eligibleDebts = debts
                .Where(d => payment.TargetDebtType is null || MatchesTarget(d.Type, payment.TargetDebtType.Value))
                .Where(d => payment.TargetOwnerId is null || d.OwnerId == payment.TargetOwnerId)
                .ToList();

            // FIFO: en eski borçtan başla
            foreach (var debt in eligibleDebts)
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

            // Mahsup edilemeyen tutar alacaktır. Aidat hedefi, tahakkuk silinince faturaya
            // yazılmaz; ödeme kaybolmaz, bakiyeyi eksiye çeker.
            if (availableAmount > 0m)
            {
                credits.Add(new LedgerCredit(
                    payment.Id,
                    payment.PaymentDate,
                    availableAmount,
                    payment.TargetDebtType));
            }
        }

        // ── 3. Faiz hesapla (kronolojik — piecewise) ─────────────────────────
        var lines = new List<LedgerLine>();

        foreach (var debt in debts)
        {
            var isOpeningCredit = debt.Type == LedgerDebtType.OpeningBalancePrincipal && debt.PrincipalAmount < 0m;
            var principal = debt.PrincipalAmount;
            var paid = allocationMap[debt.SourceId].Sum(a => a.AllocatedAmount);
            var remaining = remainingPrincipal[debt.SourceId];

            // Faiz: ödeme tarihlerine göre parçalara bölünmüş basit faiz (kronolojik).
            // Başlangıç anapara: PrincipalAmount'tan waiver'lar düşülmüş, allocation ödemeleri dahil
            // edilmiş (kalan + ödenen = waiver-sonrası anapara). Waiver bir tahsilat değildir;
            // allocation listesinde yer almaz — bu yüzden remaining + paid kullanılır.
            // Not: Negatif devir anapara (Devir Alacağı) borçlara dağıtıldıkça remaining 0'a yaklaşır;
            // hem borç satırlarında PaidAmount hem de devir satırında düşülmüş kalıntı iki kez sayılmasın
            // diye kullanılan kısım (remaining - principal > 0) PaidAmount olarak raporlanır, LineBalance
            // ise yalnızca kalan kullanılmamış alacağı (remaining <= 0) taşır.
            var waiverAdjustedPrincipal = isOpeningCredit ? 0m : remaining + paid; // = PrincipalAmount - waivers
            var interest = AccruesInterest(debt.Type) && debt.ChargesInterest && !isOpeningCredit
                ? CalculatePiecewiseInterest(
                    principalAmount: waiverAdjustedPrincipal,
                    annualRate: annualInterestRate,
                    dueDate: debt.DueDate,
                    asOfDate: asOfDate,
                    allocations: allocationMap[debt.SourceId])
                : 0m; // Kilitlenmemiş ısınma, devir alacağı ve devreden geçmiş faiz faiz işlemez

            var effectivePaid = isOpeningCredit ? (remaining - principal) : paid;

            lines.Add(new LedgerLine(
                Type: debt.Type,
                SourceId: debt.SourceId,
                Label: debt.Label,
                DueDate: debt.DueDate,
                PrincipalAmount: principal,
                PaidAmount: effectivePaid,
                RemainingPrincipal: remaining,
                InterestAccrued: interest,
                LineBalance: remaining + interest,
                Allocations: allocationMap[debt.SourceId].AsReadOnly(),
                OwnerId: debt.OwnerId,
                OwnerName: debt.OwnerName,
                PeriodStart: debt.PeriodStart,
                IsCancelled: debt.IsCancelled,
                OriginalPrincipalAmount: debt.OriginalPrincipalAmount));
        }

        AppendWaiverLines(lines, debts, waivers, waiverApplied);

        var totalPrincipalDue = lines.Sum(l => Math.Max(0m, l.RemainingPrincipal));
        var totalInterest = lines.Sum(l => l.InterestAccrued);
        var totalBalance = lines.Sum(l => l.LineBalance) - credits.Sum(c => c.Amount);

        // ── 4. Özet: borç türü bazında kalan anapara + faiz (Faz 7.1) ─────────
        // Devir anapara negatifse (kullanılmamış devir alacağı kaldıysa) negatif olarak özete yansır.
        var summary = new LedgerSummary(
            AidatPrincipalDue: lines.Where(l => l.Type == LedgerDebtType.Aidat).Sum(l => Math.Max(0m, l.RemainingPrincipal)),
            AidatInterestAccrued: lines.Where(l => l.Type == LedgerDebtType.Aidat).Sum(l => l.InterestAccrued),
            InvoicePrincipalDue: lines.Where(l => l.Type == LedgerDebtType.Invoice).Sum(l => Math.Max(0m, l.RemainingPrincipal)),
            InvoiceInterestAccrued: lines.Where(l => l.Type == LedgerDebtType.Invoice).Sum(l => l.InterestAccrued),
            OpeningBalancePrincipalDue: lines.Where(l => l.Type == LedgerDebtType.OpeningBalancePrincipal).Sum(l => l.RemainingPrincipal),
            OpeningBalanceInterestDue: lines.Where(l => l.Type == LedgerDebtType.OpeningBalanceInterest).Sum(l => Math.Max(0m, l.RemainingPrincipal)));

        return new LedgerResult(
            ApartmentId: apartment.Id,
            ApartmentTitle: apartment.Title,
            DoorNumber: apartment.DoorNumber,
            Lines: lines.AsReadOnly(),
            TotalPrincipalDue: totalPrincipalDue,
            TotalInterestAccrued: totalInterest,
            TotalBalance: totalBalance,
            AsOfDate: asOfDate,
            Summary: summary,
            Credits: credits);
    }

    // ── Yardımcı metodlar ─────────────────────────────────────────────────────

    private static List<LedgerDebt> BuildDebtList(
        Apartment apartment,
        IEnumerable<ApartmentInvoice> invoices,
        IEnumerable<AidatCharge>? aidatCharges,
        DateOnly? openingCutoff)
    {
        var debts = new List<LedgerDebt>();
        var showOpening = openingCutoff is null
            || (apartment.OpeningBalanceDate is { } openingDate && openingDate <= openingCutoff);

        // Devir bakiyesi (varsa) — faturalardan ÖNCE gelir. FIFO'da ÖNCE Devir Faiz,
        // SONRA Devir Anapara kapatılır; sonrasında faturalar (vade sırası).
        // Not: Faiz satırı "kapalı borçtur" — ona faiz işlenmez (AccruesInterest=false).
        var openingOwner = apartment.OpeningBalanceOwnerId is { } ownerId
            ? apartment.Owners.FirstOrDefault(o => o.Id == ownerId)
            : null;

        if (showOpening && apartment.OpeningBalanceInterest != 0m && apartment.OpeningBalanceDate.HasValue)
        {
            debts.Add(new LedgerDebt(
                Type: LedgerDebtType.OpeningBalanceInterest,
                SourceId: $"apt:{apartment.Id}:interest",
                DueDate: apartment.OpeningBalanceDate.Value,
                PrincipalAmount: apartment.OpeningBalanceInterest,
                Label: "Devreden Geçmiş Faiz",
                OwnerId: apartment.OpeningBalanceOwnerId,
                OwnerName: openingOwner?.OwnerName));
        }

        if (showOpening && apartment.OpeningBalancePrincipal != 0m && apartment.OpeningBalanceDate.HasValue)
        {
            var isCredit = apartment.OpeningBalancePrincipal < 0m;
            debts.Add(new LedgerDebt(
                Type: LedgerDebtType.OpeningBalancePrincipal,
                SourceId: $"apt:{apartment.Id}:principal",
                DueDate: apartment.OpeningBalanceDate.Value,
                PrincipalAmount: apartment.OpeningBalancePrincipal,
                Label: isCredit ? "Devir Alacağı" : "Devir Anapara",
                OwnerId: apartment.OpeningBalanceOwnerId,
                OwnerName: openingOwner?.OwnerName,
                ChargesInterest: !isCredit));
        }

        // Fatura ve aidat borçları tek kronolojik sırada FIFO kuyruğuna girer.
        // LINQ OrderBy stabil olduğundan aynı vadede önce fatura, sonra aidat korunur.
        var invoiceDebts = invoices
            .Where(i => !i.IsSuperseded)
            .OrderBy(i => i.GasInvoice?.DueDate ?? DateOnly.MinValue)
            .ThenBy(i => i.CreatedOn)
            .Select(i => new LedgerDebt(
                Type: LedgerDebtType.Invoice,
                SourceId: i.Id.ToString(),
                DueDate: i.GasInvoice?.DueDate ?? DateOnly.FromDateTime(i.CalculatedOnUtc.DateTime),
                PrincipalAmount: i.TotalAmount,
                Label: BuildInvoiceLabel(i),
                OwnerId: i.ApartmentOwnerId,
                OwnerName: i.ApartmentOwner?.OwnerName,
                ChargesInterest: i.GasInvoice is { IsLocked: true }))
            .ToList();

        var aidatDebts = (aidatCharges ?? Enumerable.Empty<AidatCharge>())
            .Where(c => c.Amount > 0m)
            .Select(c =>
            {
                var isCancelled = c.AidatInvoice is { IsCancelled: true };
                var baseLabel = BuildAidatLabel(c);
                return new LedgerDebt(
                    Type: LedgerDebtType.Aidat,
                    SourceId: c.Id.ToString(),
                    DueDate: c.AidatInvoice?.DueDate ?? DateOnly.FromDateTime(c.CreatedOn.UtcDateTime),
                    PrincipalAmount: isCancelled ? 0m : c.Amount,
                    Label: isCancelled ? $"İptal Edilmiş Aidat Tahakkuku ({baseLabel})" : baseLabel,
                    OwnerId: c.ApartmentOwnerId,
                    OwnerName: c.ApartmentOwner?.OwnerName,
                    ChargesInterest: !isCancelled,
                    PeriodStart: c.AidatInvoice?.PeriodStart,
                    IsCancelled: isCancelled,
                    OriginalPrincipalAmount: isCancelled ? c.Amount : null);
            })
            .ToList();

        debts.AddRange(invoiceDebts.Concat(aidatDebts).OrderBy(d => d.DueDate));
        return debts;
    }

    private static string BuildInvoiceLabel(ApartmentInvoice invoice)
    {
        if (invoice.GasInvoice is null) return "Fatura";

        // Tam Türkçe ay adları (Nisan 2026) — cari hesap ve PDF dökümünde tutarlı görünüm.
        var from = FormatMonthYear(invoice.GasInvoice.ReadingDateFrom);
        var to = FormatMonthYear(invoice.GasInvoice.ReadingDateTo);
        return from == to ? from : $"{from} – {to}";
    }

    /// <summary>
    /// Aidat satır etiketi — cari hesap ve PDF dökümünde fatura etiketleriyle tutarlı görünüm.
    /// Örnek: "Aidat Ocak 2026" / "Aidat Ocak 2026 – Şubat 2026".
    /// </summary>
    private static string BuildAidatLabel(AidatCharge charge)
    {
        var invoice = charge.AidatInvoice;
        if (invoice is null) return "Aidat";
        if (!string.IsNullOrWhiteSpace(invoice.Title)) return invoice.Title;

        // Tam Türkçe ay adları (Nisan 2026).
        var from = FormatMonthYear(invoice.PeriodStart);
        var to = FormatMonthYear(invoice.PeriodEnd);
        return from == to ? $"Aidat {from}" : $"Aidat {from} – {to}";
    }

    private static readonly string[] TurkishMonthNames =
    [
        "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
        "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık",
    ];

    /// <summary>
    /// "Eylül 2026" biçimi. Sunucudaki kültür/ICU verisine (ör. Docker imajı, InvariantGlobalization)
    /// bağlı olmaması için ay adları sabit tablodan alınır.
    /// </summary>
    private static string FormatMonthYear(DateOnly date) =>
        $"{TurkishMonthNames[date.Month - 1]} {date.Year}";

    private static string FormatMonthYear(DateTimeOffset date) =>
        $"{TurkishMonthNames[date.Month - 1]} {date.Year}";

    /// <summary>
    /// Kronolojik (piecewise) basit faiz hesabı.
    /// <para>
    /// Ödeme tarihleri, borcun ömrünü parçalara böler. Her parça için o andaki
    /// anapara kalıntısı üzerinden faiz işler. Bu sayede:
    /// <list type="bullet">
    ///   <item>Aynı gün borç+tahsilat → faiz = 0</item>
    ///   <item>Geçmişe dönük tahsilat → faiz yalnızca ödeme tarihine kadar işler</item>
    ///   <item>Kısmi ödeme → kalan anapara üzerinden devam eder</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="principalAmount">Borcun orijinal anapara tutarı (waiver sonrası değil).</param>
    /// <param name="annualRate">Yıllık faiz oranı (%, örn: 24.0 → %24)</param>
    /// <param name="dueDate">Vade tarihi — faiz bu tarihten itibaren işler.</param>
    /// <param name="asOfDate">Hesaplama günü (genellikle bugün).</param>
    /// <param name="allocations">Bu borca ait tahsilat mahsubları (kronolojik sırada).</param>
    public static decimal CalculatePiecewiseInterest(
        decimal principalAmount,
        decimal annualRate,
        DateOnly dueDate,
        DateOnly asOfDate,
        IReadOnlyList<AllocationDetail> allocations)
    {
        if (principalAmount <= 0m || annualRate <= 0m) return 0m;
        if (asOfDate <= dueDate) return 0m;

        // Borcun kronolojik parçalarını oluştur:
        // - Her ödeme kendi tarihinde anaparayı düşürür.
        // - Vadeden önce yapılan ödemeler dueDate'te sayılır.
        // Ödemeleri vade sonrasına göre filtrele + dueDate'te normalize et.
        var sortedPayments = allocations
            .OrderBy(a => a.PaymentDate)
            .Select(a => (Date: a.PaymentDate < dueDate ? dueDate : a.PaymentDate, a.AllocatedAmount))
            .ToList();

        var runningPrincipal = principalAmount;
        var prevDate = dueDate;
        var totalInterest = 0m;

        foreach (var (payDate, amount) in sortedPayments)
        {
            // Ödeme asOfDate'ten sonraysa, bu noktada döngüden çık (daha sonraki ödemeler de sonra gelir).
            if (payDate >= asOfDate) break;

            // Bu ödeme tarihi ile önceki tarih arasındaki dönemi hesapla.
            if (payDate > prevDate && runningPrincipal > 0m)
            {
                var days = payDate.DayNumber - prevDate.DayNumber;
                totalInterest += runningPrincipal * (annualRate / 100m) * (days / 365m);
            }

            // Ödemeyi anaparadan düş ve bir sonraki dönem başlangıcını güncelle.
            runningPrincipal = Math.Max(0m, runningPrincipal - amount);
            prevDate = payDate;
        }

        // Son dönem: son ödeme/vade tarihinden asOfDate'e kadar kalan anapara üzerinden.
        if (prevDate < asOfDate && runningPrincipal > 0m)
        {
            var days = asOfDate.DayNumber - prevDate.DayNumber;
            totalInterest += runningPrincipal * (annualRate / 100m) * (days / 365m);
        }

        return decimal.Round(totalInterest, 2);
    }

    /// <summary>
    /// Basit faiz: Anapara × Oran/100 × Gün/365.
    /// Vade tarihinden SONRA başlar. Vade geçmemişse 0 döner.
    /// Oran 0 ise veya anapara 0 ise 0 döner.
    /// <para>
    /// NOT: Bu metod ödeme tarihlerini dikkate ALMAZ. Yalnızca kalan anapara + asOfDate ile
    /// tek blok faiz hesaplar. Gerçek cari hesap için <see cref="CalculatePiecewiseInterest"/>
    /// kullanın; bu metod yalnızca birim testleri ve basit ön hesaplamalar içindir.
    /// </para>
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
        type is not LedgerDebtType.OpeningBalanceInterest and not LedgerDebtType.WaiverOffset;

    /// <summary>
    /// Tahsilat hedefi bu borç satırına mahsup edilebilir mi?
    /// Aidat ve ısınma (fatura) yalnızca kendi türüne düşer.
    /// Devir hedefi (anapara veya geçmiş faiz) her iki devir satırını kapsar; FIFO içinde önce geçmiş faiz kapanır.
    /// </summary>
    private static bool MatchesTarget(LedgerDebtType debtType, LedgerDebtType target)
    {
        if (debtType == LedgerDebtType.WaiverOffset || target == LedgerDebtType.WaiverOffset)
            return false;

        if (debtType == target)
            return true;

        return IsOpeningBalance(debtType) && IsOpeningBalance(target);
    }

    private static bool IsOpeningBalance(LedgerDebtType type) =>
        type is LedgerDebtType.OpeningBalancePrincipal or LedgerDebtType.OpeningBalanceInterest;

    private readonly record struct WaiverApplication(
        decimal TakenFromAidat,
        decimal TakenFromInvoice,
        decimal UnappliedCredit)
    {
        public decimal TotalTaken => TakenFromAidat + TakenFromInvoice;
    }

    /// <summary>
    /// Akıllı Görev Mahsubu Sıralaması:
    /// 1) Önce bağlı olduğu Aidat borcundan, hala artıyorsa diğer açık Aidat borçlarından (FIFO) düşer.
    /// 2) Aidat borçları yetmezse açık Isınma (Invoice) borçlarından (FIFO) düşer.
    /// 3) Hala artıyorsa kalan tutar Cari Hesaba "Alacak" (Negatif Bakiye / UnappliedCredit) olarak yazılır.
    /// </summary>
    private static Dictionary<Guid, WaiverApplication> ApplyWaivers(
        List<LedgerDebt> debts,
        Dictionary<string, decimal> remainingPrincipal,
        IEnumerable<WaiverOffset>? waivers)
    {
        var applied = new Dictionary<Guid, WaiverApplication>();
        if (waivers is null)
        {
            return applied;
        }

        foreach (var waiver in waivers.OrderBy(w => w.OffsetDate).ThenBy(w => w.CreatedOn))
        {
            if (waiver.IsCancelled || waiver.Amount <= 0m)
            {
                continue;
            }

            var primarySourceId = waiver.AidatChargeId.ToString();
            var primaryDebt = debts.FirstOrDefault(d => d.SourceId == primarySourceId);
            if (primaryDebt is null || primaryDebt.IsCancelled)
            {
                continue;
            }

            var leftover = waiver.Amount;
            var takenFromAidat = 0m;
            var takenFromInvoice = 0m;

            // 1a. Bağlı olduğu Aidat tahakkukundan düş
            if (remainingPrincipal.TryGetValue(primarySourceId, out var primaryRemaining) && primaryRemaining > 0m)
            {
                var take = Math.Min(leftover, primaryRemaining);
                remainingPrincipal[primarySourceId] = primaryRemaining - take;
                leftover -= take;
                takenFromAidat += take;
            }

            // 1b. Artan tutar varsa diğer açık Aidat borçlarından (vade sırasıyla) düş
            if (leftover > 0m)
            {
                foreach (var aidatDebt in debts.Where(d =>
                    d.Type == LedgerDebtType.Aidat && !d.IsCancelled && d.SourceId != primarySourceId))
                {
                    if (leftover <= 0m) break;
                    if (!remainingPrincipal.TryGetValue(aidatDebt.SourceId, out var rem) || rem <= 0m) continue;

                    var take = Math.Min(leftover, rem);
                    remainingPrincipal[aidatDebt.SourceId] = rem - take;
                    leftover -= take;
                    takenFromAidat += take;
                }
            }

            // 2. Aidat borcu yetmezse açık Isınma (Invoice) borçlarından (vade sırasıyla) düş
            if (leftover > 0m)
            {
                foreach (var invoiceDebt in debts.Where(d =>
                    d.Type == LedgerDebtType.Invoice && !d.IsCancelled))
                {
                    if (leftover <= 0m) break;
                    if (!remainingPrincipal.TryGetValue(invoiceDebt.SourceId, out var rem) || rem <= 0m) continue;

                    var take = Math.Min(leftover, rem);
                    remainingPrincipal[invoiceDebt.SourceId] = rem - take;
                    leftover -= take;
                    takenFromInvoice += take;
                }
            }

            // 3. Artan kısım (leftover > 0) Cari Hesapta "Alacak" (Negatif Bakiye) olarak taşınır.
            applied[waiver.Id] = new WaiverApplication(
                TakenFromAidat: takenFromAidat,
                TakenFromInvoice: takenFromInvoice,
                UnappliedCredit: leftover);
        }

        return applied;
    }

    private static void AppendWaiverLines(
        List<LedgerLine> lines,
        List<LedgerDebt> debts,
        IEnumerable<WaiverOffset>? waivers,
        Dictionary<Guid, WaiverApplication> applied)
    {
        if (waivers is null)
        {
            return;
        }

        foreach (var waiver in waivers)
        {
            var debt = debts.FirstOrDefault(d => d.SourceId == waiver.AidatChargeId.ToString());

            if (waiver.IsCancelled)
            {
                // Denetim izi: iptal edilmiş görev mahsubu Cari Hesap'ta 0 TL bakiye etkisiyle görünür.
                lines.Add(new LedgerLine(
                    Type: LedgerDebtType.WaiverOffset,
                    SourceId: $"waiver:{waiver.AidatChargeId}:{waiver.Id}",
                    Label: $"İptal Edilmiş Görev Mahsubu — Borç Geri Eklendi ({waiver.Description})",
                    DueDate: waiver.OffsetDate,
                    PrincipalAmount: 0m,
                    PaidAmount: 0m,
                    RemainingPrincipal: 0m,
                    InterestAccrued: 0m,
                    LineBalance: 0m,
                    Allocations: [],
                    OwnerId: debt?.OwnerId,
                    OwnerName: debt?.OwnerName,
                    PeriodStart: waiver.OffsetDate,
                    IsCancelled: true,
                    OriginalPrincipalAmount: waiver.Amount));
                continue;
            }

            if (!applied.TryGetValue(waiver.Id, out var app) || waiver.Amount <= 0m)
            {
                continue;
            }

            lines.Add(new LedgerLine(
                Type: LedgerDebtType.WaiverOffset,
                SourceId: $"waiver:{waiver.AidatChargeId}",
                Label: waiver.Description,
                DueDate: waiver.OffsetDate,
                PrincipalAmount: waiver.Amount,
                PaidAmount: app.TotalTaken,
                RemainingPrincipal: -app.UnappliedCredit,
                InterestAccrued: 0m,
                LineBalance: -app.UnappliedCredit,
                Allocations: [],
                OwnerId: debt?.OwnerId,
                OwnerName: debt?.OwnerName,
                PeriodStart: waiver.OffsetDate));
        }
    }
}
