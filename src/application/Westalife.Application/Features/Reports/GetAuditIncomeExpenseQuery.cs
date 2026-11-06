using System.Globalization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Services.Budget;
using Westalife.Domain.Services.Ledger;
using Westalife.Domain.Services.Reports;

namespace Westalife.Application.Features.Reports;

public sealed record AuditExpenseRow(
    string Category,
    decimal Estimated,
    decimal ExpenseAmount,
    decimal WaiverAmount,
    decimal Actual,
    decimal Remaining);

public sealed record AuditIncomeRow(string Title, decimal Amount);

public sealed record AuditBankRow(
    Guid BankAccountId,
    string Label,
    decimal Opening,
    decimal MoneyIn,
    decimal MoneyOut,
    decimal Closing);

public sealed record AuditIncomeExpenseReport(
    int Year,
    string BuildingName,
    IReadOnlyList<AuditExpenseRow> Expenses,
    decimal TotalEstimated,
    decimal TotalActual,
    decimal TotalRemaining,
    IReadOnlyList<AuditIncomeRow> Income,
    decimal TotalIncome,
    decimal Net,
    IReadOnlyList<AuditBankRow> Banks,
    DateOnly? From = null,
    DateOnly? To = null);

public sealed record GetAuditIncomeExpenseQuery(
    int Year,
    DateOnly? From = null,
    DateOnly? To = null) : IRequest<AuditIncomeExpenseReport>;

public sealed class GetAuditIncomeExpenseQueryValidator : AbstractValidator<GetAuditIncomeExpenseQuery>
{
    public GetAuditIncomeExpenseQueryValidator()
    {
        When(q => q.From.HasValue && q.To.HasValue, () =>
        {
            RuleFor(q => q.To!.Value)
                .GreaterThanOrEqualTo(q => q.From!.Value)
                .WithMessage(DomainConstants.AuditReport.PeriodInvalid);
        }).Otherwise(() =>
        {
            RuleFor(q => q.Year)
                .InclusiveBetween(DomainConstants.Budget.YearMin, DomainConstants.Budget.YearMax)
                .WithMessage(DomainConstants.Budget.YearInvalid);
        });
    }
}

public sealed class GetAuditIncomeExpenseQueryHandler
    : IRequestHandler<GetAuditIncomeExpenseQuery, AuditIncomeExpenseReport>
{
    private readonly IApplicationDbContext _db;

    public GetAuditIncomeExpenseQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<AuditIncomeExpenseReport> Handle(
        GetAuditIncomeExpenseQuery request, CancellationToken cancellationToken)
    {
        var books = await AuditBooks.LoadAsync(_db, cancellationToken);
        var from = request.From ?? new DateOnly(request.Year, 1, 1);
        var to = request.To ?? new DateOnly(request.Year, 12, 31);
        var primaryYear = request.From?.Year ?? request.Year;
        var yearsInRange = Enumerable.Range(from.Year, to.Year - from.Year + 1).ToList();

        var aidat = 0m;
        var fuel = 0m;
        var openingCollected = 0m;
        var unallocated = 0m;

        foreach (var apartment in books.Apartments)
        {
            var ledger = AuditBooks.Full(books, apartment, to);
            aidat += AuditBooks.Collected(ledger, from, to, AllocationTarget.Aidat);
            fuel += AuditBooks.Collected(ledger, from, to, AllocationTarget.Invoice);
            openingCollected += AuditBooks.Collected(
                ledger, from, to, AllocationTarget.OpeningBalance, AllocationTarget.Interest);
            unallocated += AuditBooks.Unallocated(ledger, from, to);
        }

        var expenses = await _db.Expenses.AsNoTracking()
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to)
            .ToListAsync(cancellationToken);
        var waivers = books.Waivers
            .Where(w => !w.IsCancelled && AuditTrackingMath.InRange(w.OffsetDate, from, to))
            .ToList();
        var referencedIds = expenses
            .Where(e => e.BudgetItemId.HasValue)
            .Select(e => e.BudgetItemId!.Value)
            .Concat(waivers.Select(w => w.BudgetItemId))
            .Distinct()
            .ToList();
        var items = await _db.BudgetItems.AsNoTracking()
            .Where(b => yearsInRange.Contains(b.Year) || referencedIds.Contains(b.Id))
            .ToListAsync(cancellationToken);
        var itemIds = items.Select(i => i.Id).ToList();
        var transfers = itemIds.Count == 0
            ? []
            : await _db.BudgetTransfers.AsNoTracking()
                .Where(t => itemIds.Contains(t.FromBudgetItemId) || itemIds.Contains(t.ToBudgetItemId))
                .ToListAsync(cancellationToken);

        var spent = new Dictionary<Guid, decimal>();
        var waived = new Dictionary<Guid, decimal>();
        var unbudgeted = 0m;
        foreach (var expense in expenses)
        {
            if (expense.BudgetItemId is { } itemId && items.Any(i => i.Id == itemId))
            {
                spent[itemId] = spent.GetValueOrDefault(itemId) + expense.Amount;
            }
            else
            {
                unbudgeted += expense.Amount;
            }
        }

        foreach (var waiver in waivers)
        {
            waived[waiver.BudgetItemId] = waived.GetValueOrDefault(waiver.BudgetItemId) + waiver.Amount;
        }

        var transferIn = transfers.GroupBy(t => t.ToBudgetItemId).ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
        var transferOut = transfers.GroupBy(t => t.FromBudgetItemId).ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
        var comparer = StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: false);
        var rows = new List<AuditExpenseRow>();

        // Çok yıllı yönetim dönemlerinde aynı kategori adlarını birleştirerek tek satırda göster.
        var groupedByCategory = items
            .GroupBy(i => i.Category, comparer)
            .OrderBy(g => g.Any(i => yearsInRange.Contains(i.Year)) ? 0 : 1)
            .ThenBy(g => g.Key, comparer);

        foreach (var group in groupedByCategory)
        {
            var expenseAmount = group.Sum(i => spent.GetValueOrDefault(i.Id));
            var waiverAmount = group.Sum(i => waived.GetValueOrDefault(i.Id));
            var actual = expenseAmount + waiverAmount;
            var inPeriodItems = group.Where(i => yearsInRange.Contains(i.Year)).ToList();

            if (inPeriodItems.Count == 0 && actual == 0m)
            {
                continue;
            }

            var estimated = inPeriodItems.Sum(i => i.EstimatedAmount);
            var adjusted = inPeriodItems.Sum(i =>
                BudgetMath.AdjustedEstimate(
                    i.EstimatedAmount,
                    transferIn.GetValueOrDefault(i.Id),
                    transferOut.GetValueOrDefault(i.Id)));

            rows.Add(new AuditExpenseRow(
                group.Key,
                estimated,
                expenseAmount,
                waiverAmount,
                actual,
                BudgetMath.Remaining(adjusted, actual)));
        }

        if (unbudgeted != 0m)
        {
            rows.Add(new AuditExpenseRow(
                DomainConstants.AuditReport.UnbudgetedExpense,
                0m,
                unbudgeted,
                0m,
                unbudgeted,
                BudgetMath.Remaining(0m, unbudgeted)));
        }

        var income = new List<AuditIncomeRow>
        {
            new(DomainConstants.AuditReport.AidatCollection, aidat),
            new(DomainConstants.AuditReport.FuelCollection, fuel),
            new(DomainConstants.AuditReport.OpeningCollection, openingCollected),
            new(DomainConstants.AuditReport.UnallocatedCollection, unallocated),
        };

        var accounts = await _db.BankAccounts.AsNoTracking()
            .OrderBy(a => a.BankName)
            .ThenBy(a => a.AccountName)
            .ToListAsync(cancellationToken);
        var allExpenses = await _db.Expenses.AsNoTracking().ToListAsync(cancellationToken);
        var transactions = await _db.BankTransactions.AsNoTracking().ToListAsync(cancellationToken);
        var categories = items.ToDictionary(i => i.Id, i => i.Category);
        var doors = books.Apartments.ToDictionary(a => a.Id, a => a.DoorNumber);
        var banks = accounts
            .Select(account =>
            {
                var statement = BankStatement.Build(
                    account, from, to, books.Payments, doors, allExpenses, categories, transactions);
                return new AuditBankRow(
                    account.Id, statement.Label, statement.Opening, statement.MoneyIn, statement.MoneyOut, statement.Closing);
            })
            .ToList();

        var totalIncome = income.Sum(i => i.Amount);
        var totalActual = rows.Sum(r => r.Actual);
        return new AuditIncomeExpenseReport(
            primaryYear,
            books.BuildingName,
            rows,
            rows.Sum(r => r.Estimated),
            totalActual,
            rows.Sum(r => r.Remaining),
            income,
            totalIncome,
            BudgetMath.Remaining(totalIncome, totalActual),
            banks,
            from,
            to);
    }
}
