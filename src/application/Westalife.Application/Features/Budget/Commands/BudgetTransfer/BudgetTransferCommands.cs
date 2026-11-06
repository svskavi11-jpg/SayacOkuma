using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Budget;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Budget.Commands.BudgetTransfer;

public sealed record BudgetTransferListItem(
    Guid Id,
    DateOnly TransferDate,
    decimal Amount,
    string? Note,
    Guid FromBudgetItemId,
    Guid ToBudgetItemId,
    string FromCategory,
    string ToCategory);

public sealed record GetBudgetTransfersQuery(int Year) : IRequest<IReadOnlyList<BudgetTransferListItem>>;

public sealed class GetBudgetTransfersQueryValidator : AbstractValidator<GetBudgetTransfersQuery>
{
    public GetBudgetTransfersQueryValidator()
    {
        RuleFor(q => q.Year)
            .InclusiveBetween(DomainConstants.Budget.YearMin, DomainConstants.Budget.YearMax)
            .WithMessage(DomainConstants.Budget.YearInvalid);
    }
}

public sealed class GetBudgetTransfersQueryHandler : IRequestHandler<GetBudgetTransfersQuery, IReadOnlyList<BudgetTransferListItem>>
{
    private readonly IApplicationDbContext _db;

    public GetBudgetTransfersQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<BudgetTransferListItem>> Handle(
        GetBudgetTransfersQuery request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sistem ayarlari bulunamadi.");
        if (!settings.IsBudgetTrackingEnabled)
        {
            throw new DomainException(DomainConstants.Budget.BudgetTrackingDisabled);
        }

        return await (
            from t in _db.BudgetTransfers.AsNoTracking()
            join fromItem in _db.BudgetItems.AsNoTracking() on t.FromBudgetItemId equals fromItem.Id
            join toItem in _db.BudgetItems.AsNoTracking() on t.ToBudgetItemId equals toItem.Id
            where fromItem.Year == request.Year
            orderby t.TransferDate descending, t.CreatedOn descending
            select new BudgetTransferListItem(
                t.Id,
                t.TransferDate,
                t.Amount,
                t.Note,
                t.FromBudgetItemId,
                t.ToBudgetItemId,
                fromItem.Category,
                toItem.Category)
        ).ToListAsync(cancellationToken);
    }
}

public sealed record CreateBudgetTransferCommand(
    DateOnly TransferDate,
    decimal Amount,
    Guid FromBudgetItemId,
    Guid ToBudgetItemId,
    string? Note) : IRequest<Guid>;

public sealed class CreateBudgetTransferCommandValidator : AbstractValidator<CreateBudgetTransferCommand>
{
    public CreateBudgetTransferCommandValidator()
    {
        RuleFor(c => c.Amount)
            .GreaterThan(0m)
            .LessThanOrEqualTo(DomainConstants.Budget.AmountMax)
            .WithMessage(DomainConstants.Budget.ExpenseAmountInvalid);
        RuleFor(c => c.FromBudgetItemId).NotEmpty();
        RuleFor(c => c.ToBudgetItemId).NotEmpty();
        RuleFor(c => c.Note)
            .MaximumLength(DomainConstants.Budget.TransferNoteMaxLength)
            .WithMessage(DomainConstants.Budget.DescriptionTooLong);
    }
}

public sealed class CreateBudgetTransferCommandHandler : IRequestHandler<CreateBudgetTransferCommand, Guid>
{
    private readonly IApplicationDbContext _db;

    public CreateBudgetTransferCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(CreateBudgetTransferCommand request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sistem ayarlari bulunamadi.");
        if (!settings.IsBudgetTrackingEnabled)
        {
            throw new DomainException(DomainConstants.Budget.BudgetTrackingDisabled);
        }

        if (request.FromBudgetItemId == request.ToBudgetItemId)
        {
            throw new DomainException(DomainConstants.Budget.TransferSameItem);
        }

        var from = await _db.BudgetItems.FirstOrDefaultAsync(b => b.Id == request.FromBudgetItemId, cancellationToken)
            ?? throw new NotFoundException(DomainConstants.Budget.BudgetItemNotFound);
        var to = await _db.BudgetItems.FirstOrDefaultAsync(b => b.Id == request.ToBudgetItemId, cancellationToken)
            ?? throw new NotFoundException(DomainConstants.Budget.BudgetItemNotFound);

        if (from.Year != to.Year)
        {
            throw new DomainException(DomainConstants.Budget.TransferYearMismatch);
        }

        if (request.TransferDate.Year != from.Year)
        {
            throw new DomainException(DomainConstants.Budget.TransferDateYearMismatch);
        }

        await BudgetPeriodGuard.EnsureOpen(_db, from.Year, cancellationToken);

        var available = await BudgetAvailability.Available(_db, from.Id, from.EstimatedAmount, cancellationToken);
        if (request.Amount > available)
        {
            throw new DomainException(DomainConstants.Budget.TransferExceedsAvailable);
        }

        var transfer = new Westalife.Domain.Entities.BudgetTransfer(
            request.TransferDate, request.Amount, from.Id, to.Id, request.Note);
        _db.BudgetTransfers.Add(transfer);
        await _db.SaveChangesAsync(cancellationToken);
        return transfer.Id;
    }
}

public sealed record DeleteBudgetTransferCommand(Guid Id) : IRequest;

public sealed class DeleteBudgetTransferCommandValidator : AbstractValidator<DeleteBudgetTransferCommand>
{
    public DeleteBudgetTransferCommandValidator() => RuleFor(c => c.Id).NotEmpty();
}

public sealed class DeleteBudgetTransferCommandHandler : IRequestHandler<DeleteBudgetTransferCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteBudgetTransferCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteBudgetTransferCommand request, CancellationToken cancellationToken)
    {
        var transfer = await _db.BudgetTransfers.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(DomainConstants.Budget.TransferNotFound);

        var from = await _db.BudgetItems.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == transfer.FromBudgetItemId, cancellationToken)
            ?? throw new NotFoundException(DomainConstants.Budget.BudgetItemNotFound);

        await BudgetPeriodGuard.EnsureOpen(_db, from.Year, cancellationToken);
        _db.BudgetTransfers.Remove(transfer);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
