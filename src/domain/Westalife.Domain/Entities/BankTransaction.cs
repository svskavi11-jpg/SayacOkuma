using Westalife.Domain.Common;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Domain.Entities;

public enum BankTransactionType
{
    BankInterest = 1,
    BankFee = 2,
    TransferOut = 3,
    TransferIn = 4,
}

/// <summary>
/// Bankanın kendi hareketi. Tahsilat ve gider değildir; daire borcunu ve bütçeyi değiştirmez.
/// Virman iki satırdır ve aynı TransferGroupId ile birbirine bağlıdır.
/// </summary>
public class BankTransaction : BaseEntity
{
    public Guid BankAccountId { get; private set; }

    public BankAccount? BankAccount { get; private set; }

    public BankTransactionType Type { get; private set; }

    public decimal Amount { get; private set; }

    public DateOnly TransactionDate { get; private set; }

    public string? Description { get; private set; }

    public Guid? TargetBankAccountId { get; private set; }

    public BankAccount? TargetBankAccount { get; private set; }

    public Guid? TransferGroupId { get; private set; }

    private BankTransaction()
    {
    }

    public static BankTransaction CreateInterest(
        Guid bankAccountId,
        DateOnly openingDate,
        decimal amount,
        DateOnly transactionDate,
        string? description) =>
        CreateSingle(bankAccountId, openingDate, BankTransactionType.BankInterest, amount, transactionDate, description);

    public static BankTransaction CreateFee(
        Guid bankAccountId,
        DateOnly openingDate,
        decimal amount,
        DateOnly transactionDate,
        string? description) =>
        CreateSingle(bankAccountId, openingDate, BankTransactionType.BankFee, amount, transactionDate, description);

    public static (BankTransaction Outgoing, BankTransaction Incoming) CreateTransfer(
        Guid sourceAccountId,
        DateOnly sourceOpeningDate,
        Guid targetAccountId,
        DateOnly targetOpeningDate,
        decimal amount,
        DateOnly transactionDate,
        string? description)
    {
        if (sourceAccountId == Guid.Empty || targetAccountId == Guid.Empty)
        {
            throw new DomainException(DomainConstants.BankAccount.NotFound);
        }

        if (sourceAccountId == targetAccountId)
        {
            throw new DomainException(DomainConstants.BankTransaction.SameAccount);
        }

        var groupId = Guid.NewGuid();
        var note = NormalizeDescription(description);
        var outgoing = new BankTransaction();
        outgoing.Apply(
            sourceAccountId,
            targetAccountId,
            BankTransactionType.TransferOut,
            amount,
            transactionDate,
            note,
            groupId,
            sourceOpeningDate);
        var incoming = new BankTransaction();
        incoming.Apply(
            targetAccountId,
            sourceAccountId,
            BankTransactionType.TransferIn,
            amount,
            transactionDate,
            note,
            groupId,
            targetOpeningDate);
        return (outgoing, incoming);
    }

    private static BankTransaction CreateSingle(
        Guid bankAccountId,
        DateOnly openingDate,
        BankTransactionType type,
        decimal amount,
        DateOnly transactionDate,
        string? description)
    {
        if (bankAccountId == Guid.Empty)
        {
            throw new DomainException(DomainConstants.BankAccount.NotFound);
        }

        var transaction = new BankTransaction();
        transaction.Apply(bankAccountId, null, type, amount, transactionDate, NormalizeDescription(description), null, openingDate);
        return transaction;
    }

    private void Apply(
        Guid bankAccountId,
        Guid? targetBankAccountId,
        BankTransactionType type,
        decimal amount,
        DateOnly transactionDate,
        string? description,
        Guid? transferGroupId,
        DateOnly openingDate)
    {
        if (amount <= 0m || amount > DomainConstants.BankAccount.AmountMax)
        {
            throw new DomainException(DomainConstants.BankTransaction.AmountInvalid);
        }

        if (transactionDate > CalendarToday())
        {
            throw new DomainException(DomainConstants.BankTransaction.DateFuture);
        }

        if (transactionDate < openingDate)
        {
            throw new DomainException(DomainConstants.BankTransaction.DateBeforeOpening);
        }

        if (description is not null && description.Length > DomainConstants.BankTransaction.DescriptionMaxLength)
        {
            throw new DomainException(DomainConstants.BankTransaction.DescriptionTooLong);
        }

        var isTransfer = type is BankTransactionType.TransferOut or BankTransactionType.TransferIn;
        if (isTransfer)
        {
            if (targetBankAccountId is null || targetBankAccountId == Guid.Empty || transferGroupId is null || transferGroupId == Guid.Empty)
            {
                throw new DomainException(DomainConstants.BankTransaction.TypeInvalid);
            }

            if (targetBankAccountId == bankAccountId)
            {
                throw new DomainException(DomainConstants.BankTransaction.SameAccount);
            }
        }
        else if (targetBankAccountId is not null || transferGroupId is not null)
        {
            throw new DomainException(DomainConstants.BankTransaction.TypeInvalid);
        }

        BankAccountId = bankAccountId;
        TargetBankAccountId = isTransfer ? targetBankAccountId : null;
        Type = type;
        Amount = decimal.Round(amount, DomainConstants.MoneyScale);
        TransactionDate = transactionDate;
        Description = description;
        TransferGroupId = isTransfer ? transferGroupId : null;
    }

    public static DateOnly CalendarToday()
    {
        var zoneId = OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul";
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone));
    }

    private static string? NormalizeDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        return description.Trim();
    }
}
