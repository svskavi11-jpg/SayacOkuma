using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Common.Models;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;
using Westalife.Domain.Services.Ledger;

namespace Westalife.Application.Features.GasInvoices.Queries.GetNoticeReport;

/// <summary>
/// Danfoss "Bireysel Gider Bildirimi" formunun veri kaynağı (docs/DANFOSS_NOTICE_TEMPLATE.md).
/// Birim fiyatlar: sabit = Σ Fixed ÷ Σ m², tüketim = Σ Consumption ÷ Σ kWh (formdaki 6 ondalık gösterim).
/// </summary>
public sealed record GetNoticeReportQuery(
    Guid GasInvoiceId,
    bool? ShowDebtBreakdown = null,
    bool? ShowDebtTable = null) : IRequest<NoticeReportData>;

public sealed record NoticeReportData(
    string CompanyName,
    string BuildingName,
    string BuildingAddress,
    string PeriodName,
    DateTimeOffset PeriodFrom,
    DateTimeOffset PeriodTo,
    int DaysInPeriod,
    string SubscriberNumber,
    string MasterMeterSerial,
    DateTimeOffset IssueDate,
    DateTimeOffset DueDate,
    decimal TotalInvoiceAmount,
    decimal TotalSquareMeters,
    decimal TotalConsumptionKWh,
    decimal UnitPriceFixedPerM2,
    decimal UnitPriceConsumptionPerKwh,
    decimal ReadingServiceFeeAmount,
    string? InvoiceNote,
    IReadOnlyList<NoticeRow> Rows,
    string? LeftTitle = null,
    string? RightTitle = null,
    byte[]? LeftLogo = null,
    byte[]? RightLogo = null,
    int LeftLogoHeightMm = DomainConstants.AppSettings.LogoHeightMmDefault,
    int RightLogoHeightMm = DomainConstants.AppSettings.LogoHeightMmDefault,
    bool ShowDebtBreakdown = true,
    bool ShowDebtTable = true);

public sealed record NoticeRow(
    Guid ApartmentId,
    string DoorNumber,
    string ApartmentTitle,
    string MeterSerialNumber,
    decimal SquareMeters,
    decimal ConsumptionKWh,
    decimal? FirstIndexKWh,
    decimal? LastIndexKWh,
    DateTimeOffset? LastReadingDateUtc,
    decimal FixedAmount,
    decimal ConsumptionAmount,
    decimal TotalAmount,
    bool IsMeterReadFailed,

    /// <summary>Etap 6.8: Dairenin cari hesap (Ledger) özeti — künye + gecikmiş borç dökümü için.</summary>
    NoticeLedgerSummary? Ledger = null);

/// <summary>Etap 6.8: Dairenin cari hesap (Ledger) özeti — PDF künyesi ve gecikmiş borç dökümü besler.</summary>
public sealed record NoticeLedgerSummary(
    /// <summary>Devir bakiyesi ANAPARA kalanı (ödenmemiş).</summary>
    decimal OpeningBalancePrincipalRemaining,

    /// <summary>Devir ile gelen geçmiş faiz kalanı (kapalı borç — faiz işlemez).</summary>
    decimal OpeningBalanceInterestRemaining,

    /// <summary>Hesaplama günü itibarıyla toplam işlemiş gecikme faizi (anapara + fatura kalanlarından).</summary>
    decimal InterestAccrued,

    /// <summary>Güncel toplam bakiye (pozitif = borçlu, negatif = alacak).</summary>
    decimal TotalBalance,

    /// <summary>Uygulanan yıllık faiz oranı (%).</summary>
    decimal AnnualInterestRate,

    /// <summary>Gecikmiş borç/faiz dökümü satırları (kalanı veya işleyen faizi 0 olmayanlar).</summary>
    IReadOnlyList<NoticeDebtRow> Debts,

    /// <summary>Bu gaz faturasına ait ısınma satırının kalan anaparası. Tam tahsilde 0.</summary>
    decimal CurrentPeriodInvoiceRemaining = 0m,

    /// <summary>PDF'te gösterilen ödenecek tutar. Asla negatif değildir.</summary>
    decimal AmountDue = 0m,

    /// <summary>Dairenin alacağı. Bakiye borçsa 0.</summary>
    decimal CreditBalance = 0m)
{
    public static decimal DueAmount(decimal totalBalance) => totalBalance > 0m ? totalBalance : 0m;

    public static decimal CreditAmount(decimal totalBalance) => totalBalance < 0m ? -totalBalance : 0m;

    public static decimal RemainingForInvoice(IEnumerable<LedgerLine> lines, Guid apartmentInvoiceId)
    {
        return lines
            .Where(l => l.Type == LedgerDebtType.Invoice && SameInvoice(l.SourceId, apartmentInvoiceId))
            .Sum(l => l.RemainingPrincipal);
    }

    /// <summary>
    /// Bu dönem faturası, tahakkuk oluştuktan sonraki tahsilatlarla kapanır.
    /// Daha eski bir ödemenin FIFO artığı bu satırı sıfırlamaz; defter bakiyesinden düşülen
    /// kısım toplama geri eklenir.
    /// </summary>
    public static (decimal Remaining, decimal AddBack) ResolveCurrentPeriod(
        decimal invoicePrincipal,
        DateOnly chargeDate,
        IEnumerable<LedgerLine> lines,
        Guid apartmentInvoiceId)
    {
        var matches = lines
            .Where(l => l.Type == LedgerDebtType.Invoice && SameInvoice(l.SourceId, apartmentInvoiceId))
            .ToList();

        if (matches.Count == 0)
        {
            return (invoicePrincipal, invoicePrincipal);
        }

        var settledAfterCharge = matches
            .SelectMany(l => l.Allocations)
            .Where(a => a.PaymentDate >= chargeDate)
            .Sum(a => a.AllocatedAmount);
        var remaining = Math.Max(0m, invoicePrincipal - settledAfterCharge);
        var ledgerRemaining = matches.Sum(l => Math.Max(0m, l.RemainingPrincipal));
        return (remaining, Math.Max(0m, remaining - ledgerRemaining));
    }

    private static bool SameInvoice(string sourceId, Guid apartmentInvoiceId) =>
        Guid.TryParse(sourceId, out var parsed) && parsed == apartmentInvoiceId;
}

public readonly record struct NoticeIndexPoint(Guid ApartmentId, DateTimeOffset ReadingDateUtc, decimal IndexValue);

/// <summary>
/// İlk okuma yalnızca dönem başlamadan önceki son gerçek endekstir.
/// Geçmiş kayıt yoksa uydurma değer üretilmez.
/// </summary>
public static class NoticeReadingIndex
{
    public static (decimal? FirstIndex, decimal? LastIndex) Resolve(decimal? previousIndex, decimal? lastIndex) =>
        (previousIndex, lastIndex);
}

public sealed record NoticeDebtRow(
    string Period,
    DateOnly DueDate,
    decimal RemainingPrincipal,
    int OverdueDays,
    decimal AnnualInterestRate,
    decimal InterestAccrued);

public sealed class GetNoticeReportQueryHandler : IRequestHandler<GetNoticeReportQuery, NoticeReportData>
{
    private readonly IApplicationDbContext _db;
    private readonly ReportingOptions _reportingOptions;

    public GetNoticeReportQueryHandler(IApplicationDbContext db, Microsoft.Extensions.Options.IOptions<ReportingOptions> reportingOptions)
    {
        _db = db;
        _reportingOptions = reportingOptions.Value;
    }

    private static DateOnly ChargeDate(DateTimeOffset calculatedOnUtc)
    {
        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        }
        catch (TimeZoneNotFoundException)
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
        }

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(calculatedOnUtc, tz).Date);
    }

    public async Task<NoticeReportData> Handle(GetNoticeReportQuery request, CancellationToken cancellationToken)
    {
        var invoice = await _db.GasInvoices
            .FirstOrDefaultAsync(i => i.Id == request.GasInvoiceId, cancellationToken)
            ?? throw new Westalife.Domain.Exceptions.NotFoundException("Fatura bulunamadı.");

        var building = await _db.Buildings
            .FirstOrDefaultAsync(b => b.IsActive, cancellationToken)
            ?? throw new Westalife.Domain.Exceptions.NotFoundException("Aktif bina bulunamadı.");

        var settings = await _db.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var rawRows = await
            (from row in _db.ApartmentInvoices
             join apartment in _db.Apartments on row.ApartmentId equals apartment.Id
             where row.GasInvoiceId == invoice.Id && !row.IsSuperseded
             // Sıralama bellekte sayısal yapılır (aşağıda).
             select new
             {
                 Row = row,
                 apartment.DoorNumber,
                 apartment.Title,
                 SerialNumber = apartment.Meters.Select(m => m.SerialNumber).FirstOrDefault()
             })
            .ToListAsync(cancellationToken);

        var rows = rawRows.OrderByDoorNumber(r => r.DoorNumber).ToList();

        if (rows.Count == 0)
        {
            throw new DomainException("Bu fatura için hesaplama sonucu bulunamadı. Önce dağıtımı hesaplayın.");
        }

        // Son okuma: fatura penceresindeki en güncel endeks.
        // İlk okuma: dönem başlangıcından hemen önceki gerçek endeks. Geçmiş yoksa boş kalır.
        var periodReadings = await
            (from reading in _db.MeterReadings
             join meter in _db.Meters on reading.MeterId equals meter.Id
             where meter.ApartmentId != null
                && reading.ReadingDateUtc >= invoice.ReadingDateFrom
                && reading.ReadingDateUtc < invoice.ReadingDateTo
             select new NoticeIndexPoint(meter.ApartmentId!.Value, reading.ReadingDateUtc, reading.IndexValue))
            .ToListAsync(cancellationToken);

        var priorReadings = await
            (from reading in _db.MeterReadings
             join meter in _db.Meters on reading.MeterId equals meter.Id
             where meter.ApartmentId != null
                && reading.ReadingDateUtc < invoice.ReadingDateFrom
             select new NoticeIndexPoint(meter.ApartmentId!.Value, reading.ReadingDateUtc, reading.IndexValue))
            .ToListAsync(cancellationToken);

        var lastByApartment = periodReadings
            .GroupBy(r => r.ApartmentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.ReadingDateUtc).ThenByDescending(r => r.IndexValue).First());
        var previousByApartment = priorReadings
            .GroupBy(r => r.ApartmentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.ReadingDateUtc).ThenByDescending(r => r.IndexValue).First().IndexValue);

        // ── Etap 6.8: Cari hesap (Ledger) özetleri — künye + gecikmiş borç/faiz dökümü ──
        // Her daire için LedgerService çalıştırılır (72 daire için tek batch yüklemesi — N+1 yok).
        var annualRate = settings?.InterestRate ?? 0m;
        var asOfToday = DateOnly.FromDateTime(DateTime.Today);
        var apartmentIds = rows.Select(r => r.Row.ApartmentId).Distinct().ToList();

        var apartmentsById = await _db.Apartments
            .AsNoTracking()
            .Where(a => apartmentIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var allInvoices = await _db.ApartmentInvoices
            .AsNoTracking()
            .Include(i => i.GasInvoice)
            .Where(i => apartmentIds.Contains(i.ApartmentId) && !i.IsSuperseded)
            .ToListAsync(cancellationToken);

        var allPayments = await _db.Payments
            .AsNoTracking()
            .Include(p => p.Allocations)
            .Where(p => apartmentIds.Contains(p.ApartmentId))
            .ToListAsync(cancellationToken);

        var allCharges = await _db.AidatCharges
            .AsNoTracking()
            .Include(c => c.AidatInvoice)
            .Where(c => apartmentIds.Contains(c.ApartmentId))
            .ToListAsync(cancellationToken);

        var allWaivers = await _db.WaiverOffsets
            .AsNoTracking()
            .Where(w => apartmentIds.Contains(w.ApartmentId))
            .ToListAsync(cancellationToken);

        var ledgerMap = new Dictionary<Guid, NoticeLedgerSummary>();
        foreach (var apartmentId in apartmentIds)
        {
            var apartment = apartmentsById[apartmentId];
            var apartmentInvoices = allInvoices.Where(i => i.ApartmentId == apartmentId).ToList();
            var apartmentPayments = allPayments.Where(p => p.ApartmentId == apartmentId).ToList();

            var ledger = LedgerService.Calculate(
                apartment,
                apartmentInvoices,
                apartmentPayments,
                annualRate,
                asOfToday,
                allCharges.Where(c => c.ApartmentId == apartmentId),
                allWaivers.Where(w => w.ApartmentId == apartmentId));

            // Döküm satırları: kalanı veya işleyen faizi 0 olmayan borçlar.
            var debts = ledger.Lines
                .Where(l => l.RemainingPrincipal != 0m || l.InterestAccrued != 0m)
                .Select(l => new NoticeDebtRow(
                    Period: l.Label ?? "Borç",
                    DueDate: l.DueDate,
                    RemainingPrincipal: l.RemainingPrincipal,
                    OverdueDays: Math.Max(0, asOfToday.DayNumber - l.DueDate.DayNumber),
                    AnnualInterestRate: annualRate,
                    InterestAccrued: l.InterestAccrued))
                .ToList();

            var currentInvoice = rows.First(r => r.Row.ApartmentId == apartmentId).Row;
            var (periodRemaining, addBack) = NoticeLedgerSummary.ResolveCurrentPeriod(
                currentInvoice.TotalAmount,
                ChargeDate(currentInvoice.CalculatedOnUtc),
                ledger.Lines,
                currentInvoice.Id);
            var adjustedBalance = ledger.TotalBalance + addBack;
            ledgerMap[apartmentId] = new NoticeLedgerSummary(
                OpeningBalancePrincipalRemaining: ledger.Lines
                    .Where(l => l.Type == LedgerDebtType.OpeningBalancePrincipal)
                    .Sum(l => l.RemainingPrincipal),
                OpeningBalanceInterestRemaining: ledger.Lines
                    .Where(l => l.Type == LedgerDebtType.OpeningBalanceInterest)
                    .Sum(l => l.RemainingPrincipal),
                InterestAccrued: ledger.TotalInterestAccrued,
                TotalBalance: ledger.TotalBalance,
                AnnualInterestRate: annualRate,
                Debts: debts,
                CurrentPeriodInvoiceRemaining: periodRemaining,
                AmountDue: NoticeLedgerSummary.DueAmount(adjustedBalance),
                CreditBalance: NoticeLedgerSummary.CreditAmount(adjustedBalance));
        }

        var noticeRows = rows.Select(r =>
        {
            NoticeIndexPoint? last = lastByApartment.TryGetValue(r.Row.ApartmentId, out var lastPoint)
                ? lastPoint
                : null;
            var previous = previousByApartment.TryGetValue(r.Row.ApartmentId, out var priorIndex)
                ? priorIndex
                : (decimal?)null;
            var indexes = NoticeReadingIndex.Resolve(previous, last?.IndexValue);
            return new NoticeRow(
                r.Row.ApartmentId,
                r.DoorNumber,
                r.Title,
                r.SerialNumber,
                r.Row.SquareMetersUsed,
                r.Row.ConsumptionKWhUsed,
                indexes.FirstIndex,
                indexes.LastIndex,
                last?.ReadingDateUtc,
                r.Row.FixedAmount,
                r.Row.ConsumptionAmount,
                r.Row.TotalAmount,
                r.Row.IsMeterReadFailed,
                ledgerMap.GetValueOrDefault(r.Row.ApartmentId));
        }).ToList();

        // Kural #1 invariant: Σ satır tutarı fatura toplamına eşit değilse rapor ÜRETİLMEZ.
        var allocatedTotal = noticeRows.Sum(r => r.TotalAmount);
        if (decimal.Round(allocatedTotal, 2) != decimal.Round(invoice.TotalAmount, 2))
        {
            throw new DomainException(
                $"{DomainConstants.ApartmentInvoice.TotalMustEqualParts} (Dağıtılan {allocatedTotal}, Fatura {invoice.TotalAmount})");
        }

        var totalSquareMeters = noticeRows.Sum(r => r.SquareMeters);
        var totalConsumption = noticeRows.Sum(r => r.ConsumptionKWh);
        var fixedPool = noticeRows.Sum(r => r.FixedAmount);
        var consumptionPool = noticeRows.Sum(r => r.ConsumptionAmount);

        var periodName = invoice.ReadingDateFrom.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("tr-TR"));

        // Ayarlar'dan (DB) gelen form değerleri; fatura notu boşsa varsayılan açıklama kullanılır.
        var subscriber = string.IsNullOrWhiteSpace(invoice.SubscriberNumber)
            ? settings?.DefaultSubscriberNumber ?? string.Empty
            : invoice.SubscriberNumber;
        var note = string.IsNullOrWhiteSpace(invoice.Note) ? settings?.DefaultNoticeNote : invoice.Note;
        var dueDays = settings?.DueDateDays ?? _reportingOptions.DueDateDays;
        var readingFee = settings?.ReadingServiceFee ?? _reportingOptions.ReadingServiceFeeAmount;

        return new NoticeReportData(
            settings?.CompanyName ?? string.Empty,
            building.Name,
            building.Address,
            periodName,
            invoice.ReadingDateFrom,
            invoice.ReadingDateTo,
            (int)(invoice.ReadingDateTo - invoice.ReadingDateFrom).TotalDays,
            subscriber,
            invoice.MasterMeterSerial,
            invoice.IssuedOn,
            invoice.IsLocked
                ? new DateTimeOffset(invoice.DueDate.ToDateTime(TimeOnly.MinValue), invoice.IssuedOn.Offset)
                : invoice.IssuedOn.AddDays(dueDays),
            invoice.TotalAmount,
            totalSquareMeters,
            totalConsumption,
            totalSquareMeters == 0m ? 0m : Math.Round(fixedPool / totalSquareMeters, 6),
            totalConsumption == 0m ? 0m : Math.Round(consumptionPool / totalConsumption, 6),
            readingFee,
            note,
            noticeRows,
            settings?.NoticeLeftTitle,
            settings?.NoticeRightTitle,
            settings?.LogoLeft,
            settings?.LogoRight,
            settings?.LogoLeftHeightMm ?? DomainConstants.AppSettings.LogoHeightMmDefault,
            settings?.LogoRightHeightMm ?? DomainConstants.AppSettings.LogoHeightMmDefault,
            request.ShowDebtBreakdown ?? settings?.ShowDebtBreakdownOnPdf ?? true,
            request.ShowDebtTable ?? settings?.ShowDebtTableOnPdf ?? true);
    }
}
