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
public sealed record GetNoticeReportQuery(Guid GasInvoiceId) : IRequest<NoticeReportData>;

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
    int RightLogoHeightMm = DomainConstants.AppSettings.LogoHeightMmDefault);

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

    /// <summary>Güncel toplam bakiye (pozitif = borçlu).</summary>
    decimal TotalBalance,

    /// <summary>Uygulanan yıllık faiz oranı (%).</summary>
    decimal AnnualInterestRate,

    /// <summary>Gecikmiş borç/faiz dökümü satırları (kalanı veya işleyen faizi 0 olmayanlar).</summary>
    IReadOnlyList<NoticeDebtRow> Debts);

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

        // Daire başına dönem okuma değerleri: ilk/son kümülatif endeks (kWh).
        var readingStats = await
            (from reading in _db.MeterReadings
             join meter in _db.Meters on reading.MeterId equals meter.Id
             where meter.ApartmentId != null
                && reading.ReadingDateUtc >= invoice.ReadingDateFrom
                && reading.ReadingDateUtc < invoice.ReadingDateTo
             group reading by meter.ApartmentId!.Value into g
             select new
             {
                 ApartmentId = g.Key,
                 FirstIndex = g.Min(r => r.IndexValue),
                 LastIndex = g.Max(r => r.IndexValue),
                 LastReadingDate = g.Max(r => r.ReadingDateUtc)
             })
            .ToDictionaryAsync(x => x.ApartmentId, cancellationToken);

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
            .Where(p => apartmentIds.Contains(p.ApartmentId))
            .ToListAsync(cancellationToken);

        var ledgerMap = new Dictionary<Guid, NoticeLedgerSummary>();
        foreach (var apartmentId in apartmentIds)
        {
            var apartment = apartmentsById[apartmentId];
            var apartmentInvoices = allInvoices.Where(i => i.ApartmentId == apartmentId).ToList();
            var apartmentPayments = allPayments.Where(p => p.ApartmentId == apartmentId).ToList();

            var ledger = LedgerService.Calculate(apartment, apartmentInvoices, apartmentPayments, annualRate, asOfToday);

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
                Debts: debts);
        }

        var noticeRows = rows.Select(r =>
        {
            readingStats.TryGetValue(r.Row.ApartmentId, out var stats);
            return new NoticeRow(
                r.Row.ApartmentId,
                r.DoorNumber,
                r.Title,
                r.SerialNumber,
                r.Row.SquareMetersUsed,
                r.Row.ConsumptionKWhUsed,
                stats?.FirstIndex,
                stats?.LastIndex,
                stats?.LastReadingDate,
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
            invoice.IssuedOn.AddDays(dueDays),
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
            settings?.LogoRightHeightMm ?? DomainConstants.AppSettings.LogoHeightMmDefault);
    }
}
