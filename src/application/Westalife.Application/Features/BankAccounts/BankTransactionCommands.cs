using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.BankAccounts;

public sealed record BankTransactionListItem(
    Guid Id,
    Guid BankAccountId,
    string BankLabel,
    BankTransactionType Type,
    decimal Amount,
    DateOnly TransactionDate,
    string? Description,
    Guid? TargetBankAccountId,
    string? TargetLabel,
    Guid? TransferGroupId);

public sealed record GetBankTransactionsQuery : IRequest<IReadOnlyList<BankTransactionListItem>>;

public sealed class GetBankTransactionsQueryHandler
    : IRequestHandler<GetBankTransactionsQuery, IReadOnlyList<BankTransactionListItem>>
{
    private readonly IApplicationDbContext _db;

    public GetBankTransactionsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<BankTransactionListItem>> Handle(
        GetBankTransactionsQuery request,
        CancellationToken cancellationToken)
    {
        return await (
            from transaction in _db.BankTransactions.AsNoTracking()
            join account in _db.BankAccounts.AsNoTracking() on transaction.BankAccountId equals account.Id
            join target in _db.BankAccounts.AsNoTracking() on transaction.TargetBankAccountId equals target.Id into targets
            from target in targets.DefaultIfEmpty()
            orderby transaction.TransactionDate descending, transaction.CreatedOn descending
            select new BankTransactionListItem(
                transaction.Id,
                transaction.BankAccountId,
                account.BankName + " — " + account.AccountName,
                transaction.Type,
                transaction.Amount,
                transaction.TransactionDate,
                transaction.Description,
                transaction.TargetBankAccountId,
                target == null ? null : target.BankName + " — " + target.AccountName,
                transaction.TransferGroupId))
            .ToListAsync(cancellationToken);
    }
}

public sealed record AddBankTransactionCommand(
    Guid BankAccountId,
    BankTransactionType Type,
    decimal Amount,
    DateOnly TransactionDate,
    string? Description) : IRequest<Guid>;

public sealed class AddBankTransactionCommandValidator : AbstractValidator<AddBankTransactionCommand>
{
    public AddBankTransactionCommandValidator()
    {
        RuleFor(c => c.BankAccountId).NotEmpty().WithMessage(DomainConstants.BankAccount.NotFound);
        RuleFor(c => c.Type)
            .Must(type => type is BankTransactionType.BankInterest or BankTransactionType.BankFee)
            .WithMessage(DomainConstants.BankTransaction.TypeInvalid);
        RuleFor(c => c.Amount)
            .GreaterThan(0m)
            .LessThanOrEqualTo(DomainConstants.BankAccount.AmountMax)
            .WithMessage(DomainConstants.BankTransaction.AmountInvalid);
        RuleFor(c => c.TransactionDate)
            .LessThanOrEqualTo(_ => BankTransaction.CalendarToday())
            .WithMessage(DomainConstants.BankTransaction.DateFuture);
        RuleFor(c => c.Description)
            .MaximumLength(DomainConstants.BankTransaction.DescriptionMaxLength)
            .WithMessage(DomainConstants.BankTransaction.DescriptionTooLong);
    }
}

public sealed class AddBankTransactionCommandHandler : IRequestHandler<AddBankTransactionCommand, Guid>
{
    private readonly IApplicationDbContext _db;

    public AddBankTransactionCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(AddBankTransactionCommand request, CancellationToken cancellationToken)
    {
        var account = await ActiveAccount(_db, request.BankAccountId, cancellationToken);
        var transaction = request.Type == BankTransactionType.BankInterest
            ? BankTransaction.CreateInterest(account.Id, account.OpeningDate, request.Amount, request.TransactionDate, request.Description)
            : BankTransaction.CreateFee(account.Id, account.OpeningDate, request.Amount, request.TransactionDate, request.Description);

        _db.BankTransactions.Add(transaction);
        await _db.SaveChangesAsync(cancellationToken);
        return transaction.Id;
    }

    internal static async Task<BankAccount> ActiveAccount(
        IApplicationDbContext db,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var account = await db.BankAccounts.FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken)
            ?? throw new NotFoundException(DomainConstants.BankAccount.NotFound);
        if (!account.IsActive)
        {
            throw new DomainException(DomainConstants.BankAccount.Inactive);
        }

        return account;
    }
}

public sealed record CreateBankTransferCommand(
    Guid SourceBankAccountId,
    Guid TargetBankAccountId,
    decimal Amount,
    DateOnly TransactionDate,
    string? Description) : IRequest<Guid>;

public sealed class CreateBankTransferCommandValidator : AbstractValidator<CreateBankTransferCommand>
{
    public CreateBankTransferCommandValidator()
    {
        RuleFor(c => c.SourceBankAccountId).NotEmpty().WithMessage(DomainConstants.BankAccount.NotFound);
        RuleFor(c => c.TargetBankAccountId).NotEmpty().WithMessage(DomainConstants.BankAccount.NotFound);
        RuleFor(c => c.TargetBankAccountId)
            .NotEqual(c => c.SourceBankAccountId)
            .WithMessage(DomainConstants.BankTransaction.SameAccount);
        RuleFor(c => c.Amount)
            .GreaterThan(0m)
            .LessThanOrEqualTo(DomainConstants.BankAccount.AmountMax)
            .WithMessage(DomainConstants.BankTransaction.AmountInvalid);
        RuleFor(c => c.TransactionDate)
            .LessThanOrEqualTo(_ => BankTransaction.CalendarToday())
            .WithMessage(DomainConstants.BankTransaction.DateFuture);
        RuleFor(c => c.Description)
            .MaximumLength(DomainConstants.BankTransaction.DescriptionMaxLength)
            .WithMessage(DomainConstants.BankTransaction.DescriptionTooLong);
    }
}

public sealed class CreateBankTransferCommandHandler : IRequestHandler<CreateBankTransferCommand, Guid>
{
    private readonly IApplicationDbContext _db;

    public CreateBankTransferCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(CreateBankTransferCommand request, CancellationToken cancellationToken)
    {
        var source = await AddBankTransactionCommandHandler.ActiveAccount(_db, request.SourceBankAccountId, cancellationToken);
        var target = await AddBankTransactionCommandHandler.ActiveAccount(_db, request.TargetBankAccountId, cancellationToken);
        var (outgoing, incoming) = BankTransaction.CreateTransfer(
            source.Id,
            source.OpeningDate,
            target.Id,
            target.OpeningDate,
            request.Amount,
            request.TransactionDate,
            request.Description);

        _db.BankTransactions.Add(outgoing);
        _db.BankTransactions.Add(incoming);
        await _db.SaveChangesAsync(cancellationToken);
        return outgoing.TransferGroupId!.Value;
    }
}

public sealed record DeleteBankTransactionCommand(Guid Id) : IRequest;

public sealed class DeleteBankTransactionCommandHandler : IRequestHandler<DeleteBankTransactionCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteBankTransactionCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteBankTransactionCommand request, CancellationToken cancellationToken)
    {
        var transaction = await _db.BankTransactions.FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(DomainConstants.BankTransaction.NotFound);

        if (transaction.TransferGroupId is Guid groupId)
        {
            var legs = await _db.BankTransactions.Where(t => t.TransferGroupId == groupId).ToListAsync(cancellationToken);
            _db.BankTransactions.RemoveRange(legs);
        }
        else
        {
            _db.BankTransactions.Remove(transaction);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
