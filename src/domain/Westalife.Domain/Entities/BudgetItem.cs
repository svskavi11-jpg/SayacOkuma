using Westalife.Domain.Common;
using Westalife.Domain.Constants;

namespace Westalife.Domain.Entities;

/// <summary>
/// Yıllık bütçe kalemi (işletme projesi). Tahmini tutar sabittir;
/// gerçekleşen, bu kaleme bağlı giderlerin toplamıdır.
/// </summary>
public class BudgetItem : BaseEntity, ITenantEntity
{
    /// <summary>Kaydın ait olduğu site.</summary>
    public Guid TenantId { get; private set; }

    public void AssignTenant(Guid tenantId) => TenantId = tenantId;

    public int Year { get; private set; }

    public string Category { get; private set; } = string.Empty;

    /// <summary>Tahmini tutar (TL, kuruş hassasiyeti). Harcama bu alanı düşürmez.</summary>
    public decimal EstimatedAmount { get; private set; }

    private BudgetItem() { } // EF Core

    public BudgetItem(int year, string category, decimal estimatedAmount)
    {
        Apply(year, category, estimatedAmount);
    }

    /// <summary>Kategori ve tahmini tutarı günceller. Yıl değişmez.</summary>
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
