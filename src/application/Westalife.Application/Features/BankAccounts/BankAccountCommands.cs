using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.BankAccounts;

public sealed record BankAccountListItem(
    Guid Id,
    string BankName,
    string AccountName,
    string AccountNumber,
    string? Iban,
    decimal OpeningBalance,
    DateOnly OpeningDate,
    decimal MoneyIn,
    decimal MoneyOut,
    decimal CurrentBalance,
    bool IsActive);

public sealed record GetBankAccountsQuery : IRequest<IReadOnlyList<BankAccountListItem>>;

public sealed class GetBankAccountsQueryHandler : IRequestHandler<GetBankAccountsQuery, IReadOnlyList<BankAccountListItem>>
{
    private readonly IApplicationDbContext _db;

    public GetBankAccountsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<BankAccountListItem>> Handle(GetBankAccountsQuery request, CancellationToken cancellationToken)
    {
        var accounts = await _db.BankAccounts.AsNoTracking()
            .OrderBy(a => a.BankName)
            .ThenBy(a => a.AccountName)
            .ToListAsync(cancellationToken);

        var ids = accounts.Select(a => a.Id).ToList();
        var flows = await BankMovementTotals.ByAccountAsync(_db, ids, cancellationToken);

        return accounts.Select(a =>
        {
            var flow = flows.GetValueOrDefault(a.Id);
            var incoming = flow.Incoming;
            var outgoing = flow.Outgoing;
            return new BankAccountListItem(
                a.Id,
                a.BankName,
                a.AccountName,
                a.AccountNumber,
                a.Iban,
                a.OpeningBalance,
                a.OpeningDate,
                incoming,
                outgoing,
                a.OpeningBalance + incoming - outgoing,
                a.IsActive);
        }).ToList();
    }
}

public sealed record CreateBankAccountCommand(
    string BankName,
    string AccountName,
    string AccountNumber,
    string? Iban,
    decimal OpeningBalance,
    DateOnly OpeningDate) : IRequest<Guid>;

public sealed class CreateBankAccountCommandValidator : AbstractValidator<CreateBankAccountCommand>
{
    public CreateBankAccountCommandValidator()
    {
        RuleFor(c => c.BankName).NotEmpty().WithMessage(DomainConstants.BankAccount.BankNameRequired)
            .MaximumLength(DomainConstants.BankAccount.BankNameMaxLength).WithMessage(DomainConstants.BankAccount.BankNameTooLong);
        RuleFor(c => c.AccountName).NotEmpty().WithMessage(DomainConstants.BankAccount.AccountNameRequired)
            .MaximumLength(DomainConstants.BankAccount.AccountNameMaxLength).WithMessage(DomainConstants.BankAccount.AccountNameTooLong);
        RuleFor(c => c.AccountNumber).NotEmpty().WithMessage(DomainConstants.BankAccount.AccountNumberRequired)
            .MaximumLength(DomainConstants.BankAccount.AccountNumberMaxLength).WithMessage(DomainConstants.BankAccount.AccountNumberTooLong);
        RuleFor(c => c.Iban).Must(BankAccount.IsValidIban).WithMessage(DomainConstants.BankAccount.IbanInvalid);
        RuleFor(c => c.OpeningBalance).GreaterThanOrEqualTo(0m).LessThanOrEqualTo(DomainConstants.BankAccount.AmountMax)
            .WithMessage(DomainConstants.BankAccount.OpeningBalanceInvalid);
        RuleFor(c => c.OpeningDate).LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.Today))
            .WithMessage(DomainConstants.BankAccount.OpeningDateFuture);
    }
}

public sealed class CreateBankAccountCommandHandler : IRequestHandler<CreateBankAccountCommand, Guid>
{
    private readonly IApplicationDbContext _db;

    public CreateBankAccountCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(CreateBankAccountCommand request, CancellationToken cancellationToken)
    {
        var account = new BankAccount(
            request.BankName,
            request.AccountName,
            request.AccountNumber,
            request.Iban,
            request.OpeningBalance,
            request.OpeningDate);

        await BankAccountUniqueness.EnsureAvailable(_db, account.AccountNumber, account.Iban, null, cancellationToken);
        _db.BankAccounts.Add(account);
        await _db.SaveChangesAsync(cancellationToken);
        return account.Id;
    }
}

public sealed record UpdateBankAccountCommand(
    Guid Id,
    string BankName,
    string AccountName,
    string AccountNumber,
    string? Iban,
    decimal OpeningBalance,
    DateOnly OpeningDate) : IRequest<Guid>;

public sealed class UpdateBankAccountCommandValidator : AbstractValidator<UpdateBankAccountCommand>
{
    public UpdateBankAccountCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.BankName).NotEmpty().WithMessage(DomainConstants.BankAccount.BankNameRequired)
            .MaximumLength(DomainConstants.BankAccount.BankNameMaxLength).WithMessage(DomainConstants.BankAccount.BankNameTooLong);
        RuleFor(c => c.AccountName).NotEmpty().WithMessage(DomainConstants.BankAccount.AccountNameRequired)
            .MaximumLength(DomainConstants.BankAccount.AccountNameMaxLength).WithMessage(DomainConstants.BankAccount.AccountNameTooLong);
        RuleFor(c => c.AccountNumber).NotEmpty().WithMessage(DomainConstants.BankAccount.AccountNumberRequired)
            .MaximumLength(DomainConstants.BankAccount.AccountNumberMaxLength).WithMessage(DomainConstants.BankAccount.AccountNumberTooLong);
        RuleFor(c => c.Iban).Must(BankAccount.IsValidIban).WithMessage(DomainConstants.BankAccount.IbanInvalid);
        RuleFor(c => c.OpeningBalance).GreaterThanOrEqualTo(0m).LessThanOrEqualTo(DomainConstants.BankAccount.AmountMax)
            .WithMessage(DomainConstants.BankAccount.OpeningBalanceInvalid);
        RuleFor(c => c.OpeningDate).LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.Today))
            .WithMessage(DomainConstants.BankAccount.OpeningDateFuture);
    }
}

public sealed class UpdateBankAccountCommandHandler : IRequestHandler<UpdateBankAccountCommand, Guid>
{
    private readonly IApplicationDbContext _db;

    public UpdateBankAccountCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(UpdateBankAccountCommand request, CancellationToken cancellationToken)
    {
        var account = await _db.BankAccounts.FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(DomainConstants.BankAccount.NotFound);

        account.Update(
            request.BankName,
            request.AccountName,
            request.AccountNumber,
            request.Iban,
            request.OpeningBalance,
            request.OpeningDate);

        await BankAccountUniqueness.EnsureAvailable(_db, account.AccountNumber, account.Iban, account.Id, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        return account.Id;
    }
}

public sealed record SetBankAccountActiveCommand(Guid Id, bool IsActive) : IRequest;

public sealed class SetBankAccountActiveCommandHandler : IRequestHandler<SetBankAccountActiveCommand>
{
    private readonly IApplicationDbContext _db;

    public SetBankAccountActiveCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(SetBankAccountActiveCommand request, CancellationToken cancellationToken)
    {
        var account = await _db.BankAccounts.FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(DomainConstants.BankAccount.NotFound);

        account.SetActive(request.IsActive);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record DeleteBankAccountCommand(Guid Id) : IRequest;

public sealed class DeleteBankAccountCommandHandler : IRequestHandler<DeleteBankAccountCommand>
{
    private readonly IApplicationDbContext _db;

    public DeleteBankAccountCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeleteBankAccountCommand request, CancellationToken cancellationToken)
    {
        var account = await _db.BankAccounts.FirstOrDefaultAsync(a => a.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(DomainConstants.BankAccount.NotFound);

        var usedByPayment = await _db.Payments.AnyAsync(p => p.BankAccountId == account.Id, cancellationToken);
        var usedByExpense = await _db.Expenses.AnyAsync(e => e.BankAccountId == account.Id, cancellationToken);
        var usedByTransaction = await _db.BankTransactions.AnyAsync(
            t => t.BankAccountId == account.Id || t.TargetBankAccountId == account.Id, cancellationToken);
        if (usedByPayment || usedByExpense || usedByTransaction)
        {
            throw new DomainException(DomainConstants.BankAccount.HasMovements);
        }

        _db.BankAccounts.Remove(account);
        await _db.SaveChangesAsync(cancellationToken);
    }
}

internal static class BankAccountUniqueness
{
    public static async Task EnsureAvailable(
        IApplicationDbContext db,
        string accountNumber,
        string? iban,
        Guid? exceptId,
        CancellationToken cancellationToken)
    {
        var numberTaken = await db.BankAccounts.AsNoTracking().AnyAsync(
            a => a.AccountNumber == accountNumber && (exceptId == null || a.Id != exceptId), cancellationToken);
        if (numberTaken)
        {
            throw new DomainException(DomainConstants.BankAccount.AccountNumberDuplicate);
        }

        if (iban is null)
        {
            return;
        }

        var ibanTaken = await db.BankAccounts.AsNoTracking().AnyAsync(
            a => a.Iban == iban && (exceptId == null || a.Id != exceptId), cancellationToken);
        if (ibanTaken)
        {
            throw new DomainException(DomainConstants.BankAccount.IbanDuplicate);
        }
    }
}