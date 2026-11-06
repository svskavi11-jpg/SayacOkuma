using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Budget;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Budget.Commands.UpdateBudgetItem;

public sealed record UpdateBudgetItemCommand(Guid Id, string Category, decimal EstimatedAmount) : IRequest<Guid>;

public sealed class UpdateBudgetItemCommandValidator : AbstractValidator<UpdateBudgetItemCommand>
{
    public UpdateBudgetItemCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();

        RuleFor(c => c.Category)
            .NotEmpty()
            .WithMessage(DomainConstants.Budget.CategoryRequired)
            .MaximumLength(DomainConstants.Budget.CategoryMaxLength)
            .WithMessage(DomainConstants.Budget.CategoryTooLong);

        RuleFor(c => c.EstimatedAmount)
            .GreaterThan(0m)
            .LessThanOrEqualTo(DomainConstants.Budget.AmountMax)
            .WithMessage(DomainConstants.Budget.EstimatedAmountInvalid);
    }
}

public sealed class UpdateBudgetItemCommandHandler : IRequestHandler<UpdateBudgetItemCommand, Guid>
{
    private readonly IApplicationDbContext _db;

    public UpdateBudgetItemCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(UpdateBudgetItemCommand request, CancellationToken cancellationToken)
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

        var category = request.Category.Trim();
        var key = category.ToLower();
        var taken = await _db.BudgetItems.AnyAsync(
            b => b.Id != item.Id && b.Year == item.Year && b.Category.ToLower() == key,
            cancellationToken);

        if (taken)
        {
            throw new DomainException(DomainConstants.Budget.CategoryAlreadyExists);
        }

        item.Update(category, request.EstimatedAmount);
        await _db.SaveChangesAsync(cancellationToken);
        return item.Id;
    }
}
