using Westalife.Domain.Common;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Domain.Entities;

/// <summary>
/// Site gideri. Ya bir butce kalemine baglidir ya da serbest gider turu tasir; ikisi birden olamaz.
/// </summary>
public class Expense : BaseEntity, ITenantEntity
{
    /// <summary>Kaydın ait olduğu site.</summary>
    public Guid TenantId { get; private set; }

    public void AssignTenant(Guid tenantId) => TenantId = tenantId;

    public DateOnly ExpenseDate { get; private set; }

    /// <summary>Tutar (TL, kurus hassasiyeti).</summary>
    public decimal Amount { get; private set; }

    public string? Description { get; private set; }

    /// <summary>Butce takibi kapaliyken serbest gider turu. Kaleme bagliysa bostur.</summary>
    public string? ExpenseType { get; private set; }

    public Guid? BudgetItemId { get; private set; }

    public BudgetItem? BudgetItem { get; private set; }

    /// <summary>Fatura veya makbuz numarasi. Denetimde belge takibi icin.</summary>
    public string? DocumentNumber { get; private set; }

    /// <summary>Harcamanin dusulecegi ozel toplama fonu.</summary>
    public Guid? FundAidatInvoiceId { get; private set; }

    /// <summary>Paranin ciktigi banka hesabi. Bossa gider bankaya baglanmamistir.</summary>
    public Guid? BankAccountId { get; private set; }

    public BankAccount? BankAccount { get; private set; }

    private Expense() { } // EF Core

    public Expense(
        DateOnly expenseDate,
        decimal amount,
        string? description,
        string? expenseType,
        Guid? budgetItemId,
        string? documentNumber = null,
        Guid? fundAidatInvoiceId = null,
        Guid? bankAccountId = null)
    {
        Apply(expenseDate, amount, description, expenseType, budgetItemId, documentNumber, fundAidatInvoiceId, bankAccountId);
    }

    public void Update(
        DateOnly expenseDate,
        decimal amount,
        string? description,
        string? expenseType,
        Guid? budgetItemId,
        string? documentNumber,
        Guid? fundAidatInvoiceId,
        Guid? bankAccountId)
    {
        Apply(expenseDate, amount, description, expenseType, budgetItemId, documentNumber, fundAidatInvoiceId, bankAccountId);
        LastModifiedOn = DateTimeOffset.UtcNow;
    }

    private void Apply(
        DateOnly expenseDate,
        decimal amount,
        string? description,
        string? expenseType,
        Guid? budgetItemId,
        string? documentNumber,
        Guid? fundAidatInvoiceId,
        Guid? bankAccountId)
    {
        if (amount <= 0m || amount > DomainConstants.Budget.AmountMax)
        {
            throw new ArgumentException(DomainConstants.Budget.ExpenseAmountInvalid, nameof(amount));
        }

        if (expenseDate > DateOnly.FromDateTime(DateTime.Today))
        {
            throw new ArgumentException(DomainConstants.Budget.ExpenseDateFuture, nameof(expenseDate));
        }

        if (expenseDate.Year < DomainConstants.Budget.YearMin || expenseDate.Year > DomainConstants.Budget.YearMax)
        {
            throw new ArgumentException(DomainConstants.Budget.YearInvalid, nameof(expenseDate));
        }

        var note = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (note is not null && note.Length > DomainConstants.Budget.DescriptionMaxLength)
        {
            throw new ArgumentException(DomainConstants.Budget.DescriptionTooLong, nameof(description));
        }

        var type = string.IsNullOrWhiteSpace(expenseType) ? null : expenseType.Trim();
        if (type is not null && type.Length > DomainConstants.Budget.ExpenseTypeMaxLength)
        {
            throw new ArgumentException(DomainConstants.Budget.ExpenseTypeTooLong, nameof(expenseType));
        }

        var document = string.IsNullOrWhiteSpace(documentNumber) ? null : documentNumber.Trim();
        if (document is not null && document.Length > DomainConstants.Budget.DocumentNumberMaxLength)
        {
            throw new ArgumentException(DomainConstants.Budget.DocumentNumberTooLong, nameof(documentNumber));
        }

        var hasItem = budgetItemId.HasValue && budgetItemId.Value != Guid.Empty;
        if (hasItem == type is not null)
        {
            throw new DomainException(hasItem
                ? DomainConstants.Budget.ExpenseTypeNotAllowedWithItem
                : DomainConstants.Budget.ExpenseTypeRequired);
        }

        ExpenseDate = expenseDate;
        Amount = decimal.Round(amount, DomainConstants.MoneyScale);
        Description = note;
        ExpenseType = type;
        BudgetItemId = hasItem ? budgetItemId : null;
        DocumentNumber = document;
        FundAidatInvoiceId = fundAidatInvoiceId is Guid fundId && fundId != Guid.Empty ? fundId : null;
        BankAccountId = bankAccountId is Guid accountId && accountId != Guid.Empty ? accountId : null;
    }
}