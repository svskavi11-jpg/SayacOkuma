using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Budget;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Budget.Commands.DeleteExpense;

public sealed record DeleteExpenseCommand(Guid Id) : IRequest;

public sealed class DeleteExpenseCommandValidator : AbstractValidator<DeleteExpenseCommand>
{
    public DeleteExpenseCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

public sealed class DeleteExpenseCommandHandler : IRequestHandler<DeleteExpenseCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteExpenseCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteExpenseCommand request, CancellationToken cancellationToken)
    {
        var expense = await _db.Expenses.FirstOrDefaultAsync(e => e.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(DomainConstants.Budget.ExpenseNotFound);

        await BudgetPeriodGuard.EnsureOpen(_db, expense.ExpenseDate.Year, cancellationToken);
        _db.Expenses.Remove(expense);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
