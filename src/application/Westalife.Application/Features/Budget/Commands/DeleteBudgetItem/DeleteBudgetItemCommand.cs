using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Budget;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Budget.Commands.DeleteBudgetItem;

public sealed record DeleteBudgetItemCommand(Guid Id) : IRequest;

public sealed class DeleteBudgetItemCommandValidator : AbstractValidator<DeleteBudgetItemCommand>
{
    public DeleteBudgetItemCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}

public sealed class DeleteBudgetItemCommandHandler : IRequestHandler<DeleteBudgetItemCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteBudgetItemCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteBudgetItemCommand request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sistem ayarları bulunamadı.");

        if (!settings.IsBudgetTrackingEnabled)
        {
            throw new DomainException(DomainConstants.Budget.BudgetTrackingDisabled);
        }

        var item = await _db.BudgetItems.FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(DomainConstants.Budget.BudgetItemNotFound);

        await BudgetPeriodGuard.EnsureOpen(_db, item.Year, cancellationToken);

        var hasExpenses = await _db.Expenses.AnyAsync(e => e.BudgetItemId == item.Id, cancellationToken);
        if (hasExpenses)
        {
            throw new DomainException(DomainConstants.Budget.BudgetItemHasExpenses);
        }

        var hasTransfers = await _db.BudgetTransfers.AnyAsync(
            t => t.FromBudgetItemId == item.Id || t.ToBudgetItemId == item.Id, cancellationToken);
        if (hasTransfers)
        {
            throw new DomainException(DomainConstants.Budget.BudgetItemHasTransfers);
        }

        _db.BudgetItems.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
