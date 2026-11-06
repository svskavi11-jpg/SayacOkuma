using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;
using Westalife.Domain.Services.Budget;

namespace Westalife.Application.Features.Budget.Queries.GetIncomeExpenseReport;

public sealed record GetIncomeExpenseReportQuery(int Year) : IRequest<IncomeExpenseReport>;

public sealed record IncomeExpenseMonthRow(int Month, decimal Income, decimal Expense, decimal Net);

public sealed record IncomeExpenseLine(
    DateOnly Date,
    string Kind,
    string Title,
    string? Description,
    decimal Amount);

public sealed record IncomeExpenseReport(
    int Year,
    string BuildingName,
    decimal TotalIncome,
    decimal TotalExpense,
    decimal Net,
    IReadOnlyList<IncomeExpenseMonthRow> Months,
    IReadOnlyList<IncomeExpenseLine> Lines,
    decimal EstimatedIncome,
    decimal EstimatedExpense,
    decimal EstimatedBalance);

public sealed class GetIncomeExpenseReportQueryValidator : AbstractValidator<GetIncomeExpenseReportQuery>
{
    public GetIncomeExpenseReportQueryValidator()
    {
        RuleFor(q => q.Year)
            .InclusiveBetween(DomainConstants.Budget.YearMin, DomainConstants.Budget.YearMax)
            .WithMessage(DomainConstants.Budget.YearInvalid);
    }
}

public sealed class GetIncomeExpenseReportQueryHandler
    : IRequestHandler<GetIncomeExpenseReportQuery, IncomeExpenseReport>
{
    private readonly IApplicationDbContext _db;

    public GetIncomeExpenseReportQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IncomeExpenseReport> Handle(
        GetIncomeExpenseReportQuery request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sistem ayarları bulunamadı.");

        var start = new DateOnly(request.Year, 1, 1);
        var end = new DateOnly(request.Year, 12, 31);

        var payments = await (
            from p in _db.Payments.AsNoTracking()
            join a in _db.Apartments.AsNoTracking() on p.ApartmentId equals a.Id
            where !p.IsCancelled && p.PaymentDate >= start && p.PaymentDate <= end
            select new { p.PaymentDate, p.Amount, p.Description, a.DoorNumber }
        ).ToListAsync(cancellationToken);

        var expenses = await (
            from e in _db.Expenses.AsNoTracking()
            where e.ExpenseDate >= start && e.ExpenseDate <= end
            join b in _db.BudgetItems.AsNoTracking() on e.BudgetItemId equals (Guid?)b.Id into bj
            from b in bj.DefaultIfEmpty()
            select new
            {
                e.ExpenseDate,
                e.Amount,
                e.Description,
                e.ExpenseType,
                Category = b != null ? b.Category : null,
            }
        ).ToListAsync(cancellationToken);

        var waivers = await (
            from w in _db.WaiverOffsets.AsNoTracking()
            join b in _db.BudgetItems.AsNoTracking() on w.BudgetItemId equals b.Id
            where !w.IsCancelled && w.OffsetDate >= start && w.OffsetDate <= end
            select new { w.OffsetDate, w.Amount, w.Description, b.Category }
        ).ToListAsync(cancellationToken);

        var lines = new List<IncomeExpenseLine>(payments.Count + expenses.Count + waivers.Count);
        foreach (var p in payments)
        {
            var title = string.IsNullOrWhiteSpace(p.DoorNumber)
                ? DomainConstants.Budget.CollectionTitle
                : $"{DomainConstants.Budget.CollectionTitle} — Daire {p.DoorNumber}";
            lines.Add(new IncomeExpenseLine(
                p.PaymentDate,
                DomainConstants.Budget.IncomeLabel,
                title,
                p.Description,
                p.Amount));
        }

        foreach (var e in expenses)
        {
            lines.Add(new IncomeExpenseLine(
                e.ExpenseDate,
                DomainConstants.Budget.ExpenseLabel,
                e.Category ?? e.ExpenseType ?? DomainConstants.Budget.ExpenseLabel,
                e.Description,
                e.Amount));
        }

        foreach (var w in waivers)
        {
            lines.Add(new IncomeExpenseLine(
                w.OffsetDate,
                DomainConstants.Budget.ExpenseLabel,
                w.Category,
                w.Description,
                w.Amount));
        }

        lines.Sort(static (a, b) =>
        {
            var byDate = a.Date.CompareTo(b.Date);
            return byDate != 0 ? byDate : string.Compare(a.Title, b.Title, StringComparison.Ordinal);
        });

        var months = Enumerable.Range(1, 12)
            .Select(month =>
            {
                var income = payments.Where(p => p.PaymentDate.Month == month).Sum(p => p.Amount);
                var expense = expenses.Where(e => e.ExpenseDate.Month == month).Sum(e => e.Amount)
                    + waivers.Where(w => w.OffsetDate.Month == month).Sum(w => w.Amount);
                return new IncomeExpenseMonthRow(month, income, expense, BudgetMath.Remaining(income, expense));
            })
            .ToList();

        var totalIncome = payments.Sum(p => p.Amount);
        var totalExpense = expenses.Sum(e => e.Amount) + waivers.Sum(w => w.Amount);
        var estimatedIncome = await _db.IncomeItems.AsNoTracking()
            .Where(i => i.Year == request.Year)
            .SumAsync(i => (decimal?)i.EstimatedAmount, cancellationToken) ?? 0m;
        var estimatedExpense = await _db.BudgetItems.AsNoTracking()
            .Where(b => b.Year == request.Year)
            .SumAsync(b => (decimal?)b.EstimatedAmount, cancellationToken) ?? 0m;

        return new IncomeExpenseReport(
            request.Year,
            settings.BuildingName,
            totalIncome,
            totalExpense,
            BudgetMath.Remaining(totalIncome, totalExpense),
            months,
            lines,
            estimatedIncome,
            estimatedExpense,
            BudgetMath.Balance(estimatedIncome, estimatedExpense));
    }
}
