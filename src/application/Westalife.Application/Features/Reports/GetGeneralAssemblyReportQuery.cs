using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Enums;

namespace Westalife.Application.Features.Reports;

public sealed record GeneralAssemblyMonthColumn(
    string Key,
    int Year,
    int Month,
    string Label);

public sealed record GeneralAssemblyMonthlySummaryRow(
    string RowTitle,
    IReadOnlyDictionary<string, decimal> MonthValues,
    decimal Total);

public sealed record GeneralAssemblyApartmentMatrixRow(
    Guid ApartmentId,
    string DoorNumber,
    string Title,
    decimal OpeningBalance,
    IReadOnlyDictionary<string, decimal> MonthlyAccruals,
    IReadOnlyDictionary<string, decimal> MonthlyCollections,
    decimal TotalAccrual,
    decimal TotalCollected,
    decimal TotalWaiver,
    decimal TotalInterest,
    decimal ClosingBalance);

public sealed record GeneralAssemblyBoardMember(
    string RoleTitle,
    string PersonName,
    string DoorNumber,
    DateOnly StartDate,
    DateOnly? EndDate);

public sealed record GeneralAssemblyReport(
    DateOnly From,
    DateOnly To,
    string BuildingName,
    string CompanyName,
    decimal TotalIncome,
    decimal TotalCashExpense,
    decimal TotalWaiverExpense,
    decimal TotalActualExpense,
    decimal TotalEstimatedExpense,
    decimal TotalRemainingBudget,
    decimal NetIncomeExpense,
    decimal TotalReceivables,
    decimal TotalResidentCredits,
    decimal TotalBankOpeningBalance,
    decimal TotalBankClosingBalance,
    bool Reconciled,
    IReadOnlyList<AuditIncomeRow> IncomeRows,
    IReadOnlyList<AuditExpenseRow> ExpenseRows,
    IReadOnlyList<AuditBankRow> Banks,
    IReadOnlyList<ApartmentTrackingRow> ApartmentRows,
    ApartmentTrackingRow ApartmentTotals,
    IReadOnlyList<GeneralAssemblyMonthColumn> MonthColumns,
    IReadOnlyList<GeneralAssemblyMonthlySummaryRow> MonthlySummaryRows,
    IReadOnlyList<GeneralAssemblyApartmentMatrixRow> ApartmentMatrixRows,
    IReadOnlyList<GeneralAssemblyBoardMember> BoardMembers);

public sealed record GetGeneralAssemblyReportQuery(DateOnly From, DateOnly To)
    : IRequest<GeneralAssemblyReport>;

public sealed class GetGeneralAssemblyReportQueryValidator
    : AbstractValidator<GetGeneralAssemblyReportQuery>
{
    public GetGeneralAssemblyReportQueryValidator()
    {
        RuleFor(q => q.From).NotEmpty();
        RuleFor(q => q.To)
            .NotEmpty()
            .GreaterThanOrEqualTo(q => q.From)
            .WithMessage(DomainConstants.AuditReport.PeriodInvalid);
    }
}

public sealed class GetGeneralAssemblyReportQueryHandler
    : IRequestHandler<GetGeneralAssemblyReportQuery, GeneralAssemblyReport>
{
    private static readonly string[] TurkishMonthNames =
    [
        "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
        "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık",
    ];

    private readonly IApplicationDbContext _db;
    private readonly IMediator _mediator;

    public GetGeneralAssemblyReportQueryHandler(IApplicationDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<GeneralAssemblyReport> Handle(
        GetGeneralAssemblyReportQuery request, CancellationToken cancellationToken)
    {
        var from = request.From;
        var to = request.To;

        var settings = await _db.AppSettings.AsNoTracking()
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var incomeExpense = await _mediator.Send(
            new GetAuditIncomeExpenseQuery(from.Year, from, to), cancellationToken);
        var tracking = await _mediator.Send(
            new GetApartmentTrackingQuery(from, to), cancellationToken);

        // 1. Seçilen yönetim döneminin başlangıç ve bitiş aylarına göre dinamik sütunlar
        var monthColumns = BuildDynamicMonthColumns(from, to);

        // 2. Aylık Nakit/Tahakkuk Matrisi için dönem içi hareketler
        var payments = await _db.Payments.AsNoTracking()
            .Where(p => !p.IsCancelled && p.PaymentDate >= from && p.PaymentDate <= to)
            .Select(p => new { p.ApartmentId, p.PaymentDate, p.Amount })
            .ToListAsync(cancellationToken);

        var expenses = await _db.Expenses.AsNoTracking()
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to)
            .Select(e => new { e.ExpenseDate, e.Amount })
            .ToListAsync(cancellationToken);

        var waivers = await _db.WaiverOffsets.AsNoTracking()
            .Where(w => !w.IsCancelled && w.OffsetDate >= from && w.OffsetDate <= to)
            .Select(w => new { w.ApartmentId, w.OffsetDate, w.Amount })
            .ToListAsync(cancellationToken);

        var aidatCharges = await _db.AidatCharges.AsNoTracking()
            .Include(c => c.AidatInvoice)
            .Where(c => !c.AidatInvoice.IsCancelled
                && c.AidatInvoice.PeriodStart >= from
                && c.AidatInvoice.PeriodStart <= to)
            .Select(c => new { c.ApartmentId, Date = c.AidatInvoice.PeriodStart, c.Amount })
            .ToListAsync(cancellationToken);

        var fuelInvoices = await _db.ApartmentInvoices.AsNoTracking()
            .Include(i => i.GasInvoice)
            .Where(i => !i.IsSuperseded)
            .ToListAsync(cancellationToken);

        var fuelInPeriod = fuelInvoices
            .Select(i => new
            {
                i.ApartmentId,
                Date = AuditBooks.FuelPeriodStart(i),
                i.TotalAmount,
            })
            .Where(x => x.Date >= from && x.Date <= to)
            .ToList();

        // Aylık Gelir / Gider / Mahsup Özet Matrisi
        var incomeByMonth = new Dictionary<string, decimal>();
        var cashExpenseByMonth = new Dictionary<string, decimal>();
        var waiverByMonth = new Dictionary<string, decimal>();
        var netByMonth = new Dictionary<string, decimal>();

        foreach (var col in monthColumns)
        {
            var mIncome = payments
                .Where(p => p.PaymentDate.Year == col.Year && p.PaymentDate.Month == col.Month)
                .Sum(p => p.Amount);
            var mExpense = expenses
                .Where(e => e.ExpenseDate.Year == col.Year && e.ExpenseDate.Month == col.Month)
                .Sum(e => e.Amount);
            var mWaiver = waivers
                .Where(w => w.OffsetDate.Year == col.Year && w.OffsetDate.Month == col.Month)
                .Sum(w => w.Amount);

            incomeByMonth[col.Key] = mIncome;
            cashExpenseByMonth[col.Key] = mExpense;
            waiverByMonth[col.Key] = mWaiver;
            netByMonth[col.Key] = mIncome - (mExpense + mWaiver);
        }

        var monthlySummaryRows = new List<GeneralAssemblyMonthlySummaryRow>
        {
            new("Tahsilat (Gelir)", incomeByMonth, incomeByMonth.Values.Sum()),
            new("Nakit / Banka Gideri", cashExpenseByMonth, cashExpenseByMonth.Values.Sum()),
            new("Görev Mahsubu", waiverByMonth, waiverByMonth.Values.Sum()),
            new("Dönem Net Farkı", netByMonth, netByMonth.Values.Sum()),
        };

        // Daire Bazlı Dinamik Aylık Matris
        var apartmentMatrixRows = new List<GeneralAssemblyApartmentMatrixRow>(tracking.Rows.Count);
        foreach (var trackRow in tracking.Rows)
        {
            var monthlyAccruals = new Dictionary<string, decimal>();
            var monthlyCollections = new Dictionary<string, decimal>();

            foreach (var col in monthColumns)
            {
                var mAidat = aidatCharges
                    .Where(c => c.ApartmentId == trackRow.ApartmentId && c.Date.Year == col.Year && c.Date.Month == col.Month)
                    .Sum(c => c.Amount);
                var mFuel = fuelInPeriod
                    .Where(f => f.ApartmentId == trackRow.ApartmentId && f.Date.Year == col.Year && f.Date.Month == col.Month)
                    .Sum(f => f.TotalAmount);
                var mPaid = payments
                    .Where(p => p.ApartmentId == trackRow.ApartmentId && p.PaymentDate.Year == col.Year && p.PaymentDate.Month == col.Month)
                    .Sum(p => p.Amount);

                monthlyAccruals[col.Key] = mAidat + mFuel;
                monthlyCollections[col.Key] = mPaid;
            }

            apartmentMatrixRows.Add(new GeneralAssemblyApartmentMatrixRow(
                trackRow.ApartmentId,
                trackRow.DoorNumber,
                trackRow.Title,
                trackRow.Devir,
                monthlyAccruals,
                monthlyCollections,
                trackRow.ExtraAidat + trackRow.AidatAccrual + trackRow.FuelAccrual,
                trackRow.AidatCollected + trackRow.FuelCollected,
                trackRow.Waiver,
                trackRow.Interest,
                trackRow.Net));
        }

        // İmza Bloğu için Dönem İçi Yönetim ve Denetim Kurulu Üyeleri
        var roles = await _db.RoleAssignments.AsNoTracking()
            .Include(r => r.Apartment)
            .Where(r => !r.IsCancelled
                && r.StartDate <= to
                && (r.EndDate == null || r.EndDate >= from))
            .OrderBy(r => r.RoleType)
            .ThenBy(r => r.StartDate)
            .ToListAsync(cancellationToken);

        var boardMembers = roles
            .Select(r => new GeneralAssemblyBoardMember(
                r.RoleType == RoleType.Denetci ? "Denetçi" : "Yönetici",
                r.PersonName,
                r.Apartment.DoorNumber,
                r.StartDate,
                r.EndDate))
            .ToList();

        var totalCashExpense = incomeExpense.Expenses.Sum(e => e.ExpenseAmount);
        var totalWaiverExpense = incomeExpense.Expenses.Sum(e => e.WaiverAmount);
        var totalReceivables = tracking.Rows.Where(r => r.Net > 0m).Sum(r => r.Net);
        var totalResidentCredits = tracking.Rows.Where(r => r.Net < 0m).Sum(r => -r.Net);
        var totalBankOpening = incomeExpense.Banks.Sum(b => b.Opening);
        var totalBankClosing = incomeExpense.Banks.Sum(b => b.Closing);

        return new GeneralAssemblyReport(
            from,
            to,
            settings?.BuildingName ?? incomeExpense.BuildingName,
            settings?.CompanyName ?? string.Empty,
            incomeExpense.TotalIncome,
            totalCashExpense,
            totalWaiverExpense,
            incomeExpense.TotalActual,
            incomeExpense.TotalEstimated,
            incomeExpense.TotalRemaining,
            incomeExpense.Net,
            totalReceivables,
            totalResidentCredits,
            totalBankOpening,
            totalBankClosing,
            tracking.Reconciled,
            incomeExpense.Income,
            incomeExpense.Expenses,
            incomeExpense.Banks,
            tracking.Rows,
            tracking.Totals,
            monthColumns,
            monthlySummaryRows,
            apartmentMatrixRows,
            boardMembers);
    }

    public static IReadOnlyList<GeneralAssemblyMonthColumn> BuildDynamicMonthColumns(DateOnly from, DateOnly to)
    {
        var cols = new List<GeneralAssemblyMonthColumn>();
        var cursor = new DateOnly(from.Year, from.Month, 1);
        var endMonth = new DateOnly(to.Year, to.Month, 1);

        while (cursor <= endMonth)
        {
            var key = $"{cursor.Year:D4}-{cursor.Month:D2}";
            var label = $"{TurkishMonthNames[cursor.Month - 1]} {cursor.Year}";
            cols.Add(new GeneralAssemblyMonthColumn(key, cursor.Year, cursor.Month, label));
            cursor = cursor.AddMonths(1);
        }

        return cols;
    }
}

