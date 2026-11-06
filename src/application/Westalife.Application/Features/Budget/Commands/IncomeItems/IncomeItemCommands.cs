using System.Globalization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Budget;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Budget.Commands.IncomeItems;

public sealed record IncomeItemListItem(Guid Id, int Year, string Category, decimal EstimatedAmount);

public sealed record GetIncomeItemsQuery(int Year) : IRequest<IReadOnlyList<IncomeItemListItem>>;

public sealed class GetIncomeItemsQueryValidator : AbstractValidator<GetIncomeItemsQuery>
{
    public GetIncomeItemsQueryValidator()
    {
        RuleFor(q => q.Year)
            .InclusiveBetween(DomainConstants.Budget.YearMin, DomainConstants.Budget.YearMax)
            .WithMessage(DomainConstants.Budget.YearInvalid);
    }
}

public sealed class GetIncomeItemsQueryHandler : IRequestHandler<GetIncomeItemsQuery, IReadOnlyList<IncomeItemListItem>>
{
    private readonly IApplicationDbContext _db;

    public GetIncomeItemsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<IncomeItemListItem>> Handle(GetIncomeItemsQuery request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sistem ayarlari bulunamadi.");

        if (!settings.IsBudgetTrackingEnabled)
        {
            throw new DomainException(DomainConstants.Budget.BudgetTrackingDisabled);
        }

        var comparer = StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: false);
        var items = await _db.IncomeItems.AsNoTracking()
            .Where(i => i.Year == request.Year)
            .ToListAsync(cancellationToken);

        return items
            .OrderBy(i => i.Category, comparer)
            .Select(i => new IncomeItemListItem(i.Id, i.Year, i.Category, i.EstimatedAmount))
            .ToList();
    }
}

public sealed record CreateIncomeItemCommand(int Year, string Category, decimal EstimatedAmount) : IRequest<Guid>;

public sealed class CreateIncomeItemCommandValidator : AbstractValidator<CreateIncomeItemCommand>
{
    public CreateIncomeItemCommandValidator()
    {
        RuleFor(c => c.Year)
            .InclusiveBetween(DomainConstants.Budget.YearMin, DomainConstants.Budget.YearMax)
            .WithMessage(DomainConstants.Budget.YearInvalid);
        RuleFor(c => c.Category)
            .NotEmpty().WithMessage(DomainConstants.Budget.CategoryRequired)
            .MaximumLength(DomainConstants.Budget.CategoryMaxLength).WithMessage(DomainConstants.Budget.CategoryTooLong);
        RuleFor(c => c.EstimatedAmount)
            .GreaterThan(0m)
            .LessThanOrEqualTo(DomainConstants.Budget.AmountMax)
            .WithMessage(DomainConstants.Budget.EstimatedAmountInvalid);
    }
}

public sealed class CreateIncomeItemCommandHandler : IRequestHandler<CreateIncomeItemCommand, Guid>
{
    private readonly IApplicationDbContext _db;

    public CreateIncomeItemCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(CreateIncomeItemCommand request, CancellationToken cancellationToken)
    {
        await RequireTracking(cancellationToken);
        await BudgetPeriodGuard.EnsureOpen(_db, request.Year, cancellationToken);

        var category = request.Category.Trim();
        var key = category.ToLower();
        var taken = await _db.IncomeItems.AnyAsync(
            i => i.Year == request.Year && i.Category.ToLower() == key, cancellationToken);
        if (taken)
        {
            throw new DomainException(DomainConstants.Budget.IncomeCategoryAlreadyExists);
        }

        var item = new IncomeItem(request.Year, category, request.EstimatedAmount);
        _db.IncomeItems.Add(item);
        await _db.SaveChangesAsync(cancellationToken);
        return item.Id;
    }

    private async Task RequireTracking(CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sistem ayarlari bulunamadi.");
        if (!settings.IsBudgetTrackingEnabled)
        {
            throw new DomainException(DomainConstants.Budget.BudgetTrackingDisabled);
        }
    }
}

public sealed record UpdateIncomeItemCommand(Guid Id, string Category, decimal EstimatedAmount) : IRequest<Guid>;

public sealed class UpdateIncomeItemCommandValidator : AbstractValidator<UpdateIncomeItemCommand>
{
    public UpdateIncomeItemCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Category)
            .NotEmpty().WithMessage(DomainConstants.Budget.CategoryRequired)
            .MaximumLength(DomainConstants.Budget.CategoryMaxLength).WithMessage(DomainConstants.Budget.CategoryTooLong);
        RuleFor(c => c.EstimatedAmount)
            .GreaterThan(0m)
            .LessThanOrEqualTo(DomainConstants.Budget.AmountMax)
            .WithMessage(DomainConstants.Budget.EstimatedAmountInvalid);
    }
}

public sealed class UpdateIncomeItemCommandHandler : IRequestHandler<UpdateIncomeItemCommand, Guid>
{
    private readonly IApplicationDbContext _db;

    public UpdateIncomeItemCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(UpdateIncomeItemCommand request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sistem ayarlari bulunamadi.");
        if (!settings.IsBudgetTrackingEnabled)
        {
            throw new DomainException(DomainConstants.Budget.BudgetTrackingDisabled);
        }

        var item = await _db.IncomeItems.FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(DomainConstants.Budget.IncomeItemNotFound);

        await BudgetPeriodGuard.EnsureOpen(_db, item.Year, cancellationToken);

        var category = request.Category.Trim();
        var key = category.ToLower();
        var taken = await _db.IncomeItems.AnyAsync(
            i => i.Id != item.Id && i.Year == item.Year && i.Category.ToLower() == key, cancellationToken);
        if (taken)
        {
            throw new DomainException(DomainConstants.Budget.IncomeCategoryAlreadyExists);
        }

        item.Update(category, request.EstimatedAmount);
        await _db.SaveChangesAsync(cancellationToken);
        return item.Id;
    }
}

public sealed record DeleteIncomeItemCommand(Guid Id) : IRequest;

public sealed class DeleteIncomeItemCommandValidator : AbstractValidator<DeleteIncomeItemCommand>
{
    public DeleteIncomeItemCommandValidator() => RuleFor(c => c.Id).NotEmpty();
}

public sealed class DeleteIncomeItemCommandHandler : IRequestHandler<DeleteIncomeItemCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteIncomeItemCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteIncomeItemCommand request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sistem ayarlari bulunamadi.");
        if (!settings.IsBudgetTrackingEnabled)
        {
            throw new DomainException(DomainConstants.Budget.BudgetTrackingDisabled);
        }

        var item = await _db.IncomeItems.FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(DomainConstants.Budget.IncomeItemNotFound);

        await BudgetPeriodGuard.EnsureOpen(_db, item.Year, cancellationToken);
        _db.IncomeItems.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
