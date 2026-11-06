using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.BankAccounts;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Budget.Commands.UpdateExpense;

public sealed record UpdateExpenseCommand(
    Guid Id,
    DateOnly ExpenseDate,
    decimal Amount,
    string? Description,
    string? ExpenseType,
    Guid? BudgetItemId,
    string? DocumentNumber,
    Guid? FundAidatInvoiceId,
    Guid? BankAccountId) : IRequest<Guid>;

public sealed class UpdateExpenseCommandValidator : AbstractValidator<UpdateExpenseCommand>
{
    public UpdateExpenseCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
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

public sealed class UpdateExpenseCommandHandler : IRequestHandler<UpdateExpenseCommand, Guid>
{
    private readonly IApplicationDbContext _db;

    public UpdateExpenseCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(UpdateExpenseCommand request, CancellationToken cancellationToken)
    {
        var expense = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(DomainConstants.Budget.ExpenseNotFound);

        var settings = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sistem ayarlari bulunamadi.");

        await BudgetPeriodGuard.EnsureOpen(_db, expense.ExpenseDate.Year, cancellationToken);
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
            _db, request.BankAccountId, expense.BankAccountId, cancellationToken);

        if (settings.IsBudgetTrackingEnabled)
        {
            if (request.BudgetItemId is null || request.BudgetItemId == Guid.Empty)
            {
                throw new DomainException(DomainConstants.Budget.BudgetItemRequired);
            }

            var item = await _db.BudgetItems.AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == request.BudgetItemId, cancellationToken)
                ?? throw new NotFoundException(DomainConstants.Budget.BudgetItemNotFound);

            if (item.Year != request.ExpenseDate.Year)
            {
                throw new DomainException(DomainConstants.Budget.BudgetItemYearMismatch);
            }

            expense.Update(
                request.ExpenseDate, request.Amount, request.Description, null, item.Id,
                request.DocumentNumber, fundId, bankAccountId);
        }
        else
        {
            if (request.BudgetItemId is not null && request.BudgetItemId != Guid.Empty)
            {
                throw new DomainException(DomainConstants.Budget.BudgetItemNotAllowed);
            }

            expense.Update(
                request.ExpenseDate, request.Amount, request.Description, request.ExpenseType, null,
                request.DocumentNumber, fundId, bankAccountId);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return expense.Id;
    }
}