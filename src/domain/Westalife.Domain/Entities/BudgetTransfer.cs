using Westalife.Domain.Common;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Domain.Entities;

/// <summary>
/// Butce kalemleri arasi virman. Kaynak kalemin kullanilabilir tutarini dusurur, hedefi artirir.
/// </summary>
public class BudgetTransfer : BaseEntity
{
    public DateOnly TransferDate { get; private set; }

    public decimal Amount { get; private set; }

    public string? Note { get; private set; }

    public Guid FromBudgetItemId { get; private set; }

    public Guid ToBudgetItemId { get; private set; }

    public BudgetItem? FromBudgetItem { get; private set; }

    public BudgetItem? ToBudgetItem { get; private set; }

    private BudgetTransfer() { }

    public BudgetTransfer(
        DateOnly transferDate,
        decimal amount,
        Guid fromBudgetItemId,
        Guid toBudgetItemId,
        string? note)
    {
        if (amount <= 0m || amount > DomainConstants.Budget.AmountMax)
        {
            throw new ArgumentException(DomainConstants.Budget.ExpenseAmountInvalid, nameof(amount));
        }

        if (transferDate > DateOnly.FromDateTime(DateTime.Today))
        {
            throw new ArgumentException(DomainConstants.Budget.ExpenseDateFuture, nameof(transferDate));
        }

        if (transferDate.Year < DomainConstants.Budget.YearMin || transferDate.Year > DomainConstants.Budget.YearMax)
        {
            throw new ArgumentException(DomainConstants.Budget.YearInvalid, nameof(transferDate));
        }

        if (fromBudgetItemId == Guid.Empty || toBudgetItemId == Guid.Empty)
        {
            throw new ArgumentException(DomainConstants.Budget.BudgetItemRequired, nameof(fromBudgetItemId));
        }

        if (fromBudgetItemId == toBudgetItemId)
        {
            throw new DomainException(DomainConstants.Budget.TransferSameItem);
        }

        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmed is not null && trimmed.Length > DomainConstants.Budget.TransferNoteMaxLength)
        {
            throw new ArgumentException(DomainConstants.Budget.DescriptionTooLong, nameof(note));
        }

        TransferDate = transferDate;
        Amount = decimal.Round(amount, DomainConstants.MoneyScale);
        FromBudgetItemId = fromBudgetItemId;
        ToBudgetItemId = toBudgetItemId;
        Note = trimmed;
    }
}
