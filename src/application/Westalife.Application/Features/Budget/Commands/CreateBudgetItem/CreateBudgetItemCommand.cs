using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Budget;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Budget.Commands.CreateBudgetItem;

public sealed record CreateBudgetItemCommand(int Year, string Category, decimal EstimatedAmount) : IRequest<Guid>;

public sealed class CreateBudgetItemCommandValidator : AbstractValidator<CreateBudgetItemCommand>
{
    public CreateBudgetItemCommandValidator()
    {
        RuleFor(c => c.Year)
            .InclusiveBetween(DomainConstants.Budget.YearMin, DomainConstants.Budget.YearMax)
            .WithMessage(DomainConstants.Budget.YearInvalid);

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

public sealed class CreateBudgetItemCommandHandler : IRequestHandler<CreateBudgetItemCommand, Guid>
{
    private readonly IApplicationDbContext _db;

    public CreateBudgetItemCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(CreateBudgetItemCommand request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sistem ayarları bulunamadı.");

        if (!settings.IsBudgetTrackingEnabled)
        {
            throw new DomainException(DomainConstants.Budget.BudgetTrackingDisabled);
        }

        await BudgetPeriodGuard.EnsureOpen(_db, request.Year, cancellationToken);

        var category = request.Category.Trim();
        var key = category.ToLower();
        var taken = await _db.BudgetItems.AnyAsync(
            b => b.Year == request.Year && b.Category.ToLower() == key,
            cancellationToken);

        if (taken)
        {
            throw new DomainException(DomainConstants.Budget.CategoryAlreadyExists);
        }

        var item = new BudgetItem(request.Year, category, request.EstimatedAmount);
        _db.BudgetItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken);
        return item.Id;
    }
}
