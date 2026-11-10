using Westalife.Domain.Common;
using Westalife.Domain.Constants;

namespace Westalife.Domain.Entities;

/// <summary>
/// Yillik tahmini gelir kalemi. Ornek: banka faizi, ortak alan kira geliri.
/// Gerceklesen tahsilat bu tutari dusurmez; denge tahmini gelir eksi tahmini giderdir.
/// </summary>
public class IncomeItem : BaseEntity, ITenantEntity
{
    /// <summary>Kaydın ait olduğu site.</summary>
    public Guid TenantId { get; private set; }

    public void AssignTenant(Guid tenantId) => TenantId = tenantId;

    public int Year { get; private set; }

    public string Category { get; private set; } = string.Empty;

    public decimal EstimatedAmount { get; private set; }

    private IncomeItem() { }

    public IncomeItem(int year, string category, decimal estimatedAmount)
    {
        Apply(year, category, estimatedAmount);
    }

    public void Update(string category, decimal estimatedAmount)
    {
        Apply(Year, category, estimatedAmount);
        LastModifiedOn = DateTimeOffset.UtcNow;
    }

    private void Apply(int year, string category, decimal estimatedAmount)
    {
        if (year < DomainConstants.Budget.YearMin || year > DomainConstants.Budget.YearMax)
        {
            throw new ArgumentException(DomainConstants.Budget.YearInvalid, nameof(year));
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            throw new ArgumentException(DomainConstants.Budget.CategoryRequired, nameof(category));
        }

        var trimmed = category.Trim();
        if (trimmed.Length > DomainConstants.Budget.CategoryMaxLength)
        {
            throw new ArgumentException(DomainConstants.Budget.CategoryTooLong, nameof(category));
        }

        if (estimatedAmount <= 0m || estimatedAmount > DomainConstants.Budget.AmountMax)
        {
            throw new ArgumentException(DomainConstants.Budget.EstimatedAmountInvalid, nameof(estimatedAmount));
        }

        Year = year;
        Category = trimmed;
        EstimatedAmount = decimal.Round(estimatedAmount, DomainConstants.MoneyScale);
    }
}
