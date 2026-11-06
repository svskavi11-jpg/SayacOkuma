using System.Globalization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.BankAccounts;
using Westalife.Application.Features.Budget;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;
using Westalife.Domain.Services.Budget;

namespace Westalife.Application.Features.Budget.Commands.CreateExpense;

public sealed record CreateExpenseResult(
    Guid Id,
    bool BudgetWarning,
    decimal? UtilizationPercent,
    string? WarningMessage);

public sealed record CreateExpenseCommand(
    DateOnly ExpenseDate,
    decimal Amount,
    string? Description,
    string? ExpenseType,
    Guid? BudgetItemId,
    string? DocumentNumber = null,
    Guid? FundAidatInvoiceId = null,
    Guid? BankAccountId = null) : IRequest<CreateExpenseResult>;

public sealed class CreateExpenseCommandValidator : AbstractValidator<CreateExpenseCommand>
{
    public CreateExpenseCommandValidator()
    {
        RuleFor(c => c.ExpenseDate)
            .Must(d => d.Year >= DomainConstants.Budget.YearMin && d.Year <= DomainConstants.Budget.YearMax)
            .WithMessage(DomainConstants.Budget.YearInvalid)
            .Must(d => d <= DateOnly.FromDateTime(DateTime.Today))
            .WithMessage(DomainConstants.Budget.ExpenseDateFuture);

        RuleFor(c => c.Amount)
            .GreaterThan(0m)
            .LessThanOrEqualTo(DomainConstants.Budget.AmountMax)
            .WithMessage(DomainConstants.Budget.ExpenseAmountInvalid);

        RuleFor(c => c.Description)
            .MaximumLength(DomainConstants.Budget.DescriptionMaxLength)
            .WithMessage(DomainConstants.Budget.DescriptionTooLong);

        RuleFor(c => c.ExpenseType)
            .MaximumLength(DomainConstants.Budget.ExpenseTypeMaxLength)
            .WithMessage(DomainConstants.Budget.ExpenseTypeTooLong);

        RuleFor(c => c.DocumentNumber)
            .MaximumLength(DomainConstants.Budget.DocumentNumberMaxLength)
            .WithMessage(DomainConstants.Budget.DocumentNumberTooLong);
    }
}

public sealed class CreateExpenseCommandHandler : IRequestHandler<CreateExpenseCommand, CreateExpenseResult>
{
    private readonly IApplicationDbContext _db;

    public CreateExpenseCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CreateExpenseResult> Handle(CreateExpenseCommand request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sistem ayarları bulunamadı.");

        await BudgetPeriodGuard.EnsureOpen(_db, request.ExpenseDate.Year, cancellationToken);

        Guid? fundId = request.FundAidatInvoiceId is Guid raw && raw != Guid.Empty ? raw : null;
        if (fundId is Guid linkedFund)
        {
            var fund = await _db.AidatInvoices.AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == linkedFund && !i.IsCancelled, cancellationToken)
                ?? throw new NotFoundException(DomainConstants.Budget.FundNotFound);
            if (fund.ChargeType != AidatChargeType.OzelToplama)
            {
                throw new DomainException(DomainConstants.Budget.FundNotSpecial);
            }
        }

        var bankAccountId = await BankAccountLookup.ResolveAsync(
            _db, request.BankAccountId, null, cancellationToken);

        Expense expense;
        BudgetItem? item = null;

        if (settings.IsBudgetTrackingEnabled)
        {
            if (request.BudgetItemId is null || request.BudgetItemId == Guid.Empty)
            {
                throw new DomainException(DomainConstants.Budget.BudgetItemRequired);
            }

            item = await _db.BudgetItems.FirstOrDefaultAsync(b => b.Id == request.BudgetItemId, cancellationToken)
                ?? throw new NotFoundException(DomainConstants.Budget.BudgetItemNotFound);

            if (item.Year != request.ExpenseDate.Year)
            {
                throw new DomainException(DomainConstants.Budget.BudgetItemYearMismatch);
            }

            expense = new Expense(
                request.ExpenseDate, request.Amount, request.Description, null, item.Id, request.DocumentNumber, fundId, bankAccountId);
        }
        else
        {
            if (request.BudgetItemId is not null && request.BudgetItemId != Guid.Empty)
            {
                throw new DomainException(DomainConstants.Budget.BudgetItemNotAllowed);
            }

            expense = new Expense(
                request.ExpenseDate, request.Amount, request.Description, request.ExpenseType, null, request.DocumentNumber, fundId, bankAccountId);
        }

        bool warning = false;
        decimal? percent = null;
        string? warningMessage = null;

        if (item is not null)
        {
            var transferIn = await _db.BudgetTransfers.AsNoTracking()
                .Where(t => t.ToBudgetItemId == item.Id)
                .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;
            var transferOut = await _db.BudgetTransfers.AsNoTracking()
                .Where(t => t.FromBudgetItemId == item.Id)
                .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;
            var spent = await _db.Expenses.AsNoTracking()
                .Where(e => e.BudgetItemId == item.Id)
                .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;
            var estimate = BudgetMath.AdjustedEstimate(item.EstimatedAmount, transferIn, transferOut);

            var projected = decimal.Round(spent + expense.Amount, DomainConstants.MoneyScale);
            percent = BudgetMath.UtilizationPercent(estimate, projected);
            warning = BudgetMath.ReachesWarning(estimate, projected);
            if (warning)
            {
                warningMessage = string.Format(
                    CultureInfo.GetCultureInfo("tr-TR"),
                    DomainConstants.Budget.WarningReachedFormat,
                    item.Category,
                    percent);
            }
        }

        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync(cancellationToken);

        return new CreateExpenseResult(expense.Id, warning, percent, warningMessage);
    }
}
