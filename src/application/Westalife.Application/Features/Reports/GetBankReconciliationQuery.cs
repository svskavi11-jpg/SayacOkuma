using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Reports;

public sealed record GetBankReconciliationQuery(Guid BankAccountId, DateOnly From, DateOnly To)
    : IRequest<BankReconciliationReport>;

public sealed class GetBankReconciliationQueryValidator : AbstractValidator<GetBankReconciliationQuery>
{
    public GetBankReconciliationQueryValidator()
    {
        RuleFor(q => q.BankAccountId).NotEmpty();
        RuleFor(q => q.To)
            .GreaterThanOrEqualTo(q => q.From)
            .WithMessage(DomainConstants.AuditReport.PeriodInvalid);
    }
}

public sealed class GetBankReconciliationQueryHandler
    : IRequestHandler<GetBankReconciliationQuery, BankReconciliationReport>
{
    private readonly IApplicationDbContext _db;

    public GetBankReconciliationQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<BankReconciliationReport> Handle(
        GetBankReconciliationQuery request, CancellationToken cancellationToken)
    {
        var account = await _db.BankAccounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.BankAccountId, cancellationToken)
            ?? throw new NotFoundException(DomainConstants.AuditReport.BankNotFound);

        var payments = await _db.Payments.AsNoTracking().ToListAsync(cancellationToken);
        var expenses = await _db.Expenses.AsNoTracking().ToListAsync(cancellationToken);
        var transactions = await _db.BankTransactions.AsNoTracking().ToListAsync(cancellationToken);
        var doors = await _db.Apartments.AsNoTracking()
            .Select(a => new { a.Id, a.DoorNumber })
            .ToDictionaryAsync(a => a.Id, a => a.DoorNumber, cancellationToken);
        var categories = await _db.BudgetItems.AsNoTracking()
            .Select(b => new { b.Id, b.Category })
            .ToDictionaryAsync(b => b.Id, b => b.Category, cancellationToken);

        return BankStatement.Build(
            account, request.From, request.To, payments, doors, expenses, categories, transactions);
    }
}
