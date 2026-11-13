using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;

namespace Westalife.Application.Features.Budget.Queries.GetExpenses;

public sealed record GetExpensesQuery(int Year) : IRequest<IReadOnlyList<ExpenseListItem>>;

public sealed record ExpenseListItem(
    Guid Id,
    DateOnly ExpenseDate,
    decimal Amount,
    string? Description,
    string DisplayType,
    Guid? BudgetItemId,
    string? DocumentNumber,
    Guid? FundAidatInvoiceId,
    string? FundTitle,
    Guid? BankAccountId,
    string? BankAccountLabel,
    string? CreatedBy = null);

public sealed class GetExpensesQueryValidator : AbstractValidator<GetExpensesQuery>
{
    public GetExpensesQueryValidator()
    {
        RuleFor(q => q.Year)
            .InclusiveBetween(DomainConstants.Budget.YearMin, DomainConstants.Budget.YearMax)
            .WithMessage(DomainConstants.Budget.YearInvalid);
    }
}

public sealed class GetExpensesQueryHandler : IRequestHandler<GetExpensesQuery, IReadOnlyList<ExpenseListItem>>
{
    private readonly IApplicationDbContext _db;

    public GetExpensesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ExpenseListItem>> Handle(GetExpensesQuery request, CancellationToken cancellationToken)
    {
        var start = new DateOnly(request.Year, 1, 1);
        var end = new DateOnly(request.Year, 12, 31);

        var rows = await (
            from e in _db.Expenses.AsNoTracking()
            where e.ExpenseDate >= start && e.ExpenseDate <= end
            join b in _db.BudgetItems.AsNoTracking() on e.BudgetItemId equals (Guid?)b.Id into bj
            from b in bj.DefaultIfEmpty()
            join f in _db.AidatInvoices.AsNoTracking() on e.FundAidatInvoiceId equals (Guid?)f.Id into fj
            from f in fj.DefaultIfEmpty()
            join bank in _db.BankAccounts.AsNoTracking() on e.BankAccountId equals (Guid?)bank.Id into bankJoin
            from bank in bankJoin.DefaultIfEmpty()
            orderby e.ExpenseDate descending, e.CreatedOn descending
            select new
            {
                e.Id,
                e.ExpenseDate,
                e.Amount,
                e.Description,
                e.ExpenseType,
                e.DocumentNumber,
                Category = b != null ? b.Category : null,
                e.BudgetItemId,
                e.FundAidatInvoiceId,
                FundTitle = f != null ? f.Title : null,
                e.BankAccountId,
                BankName = bank != null ? bank.BankName : null,
                AccountName = bank != null ? bank.AccountName : null,
                e.CreatedBy,
            }).ToListAsync(cancellationToken);

        return rows
            .Select(r => new ExpenseListItem(
                r.Id,
                r.ExpenseDate,
                r.Amount,
                r.Description,
                r.Category ?? r.ExpenseType ?? string.Empty,
                r.BudgetItemId,
                r.DocumentNumber,
                r.FundAidatInvoiceId,
                r.FundTitle,
                r.BankAccountId,
                r.BankName == null ? null : r.BankName + " — " + r.AccountName,
                r.CreatedBy))
            .ToList();
    }
}
