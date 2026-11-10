using Westalife.Domain.Common;
using Westalife.Domain.Constants;

namespace Westalife.Domain.Entities;

/// <summary>
/// Butce yili. Kilitli yila yeni gider, gelir kalemi veya virman yazilamaz.
/// </summary>
public class BudgetPeriod : BaseEntity, ITenantEntity
{
    /// <summary>Kaydın ait olduğu site.</summary>
    public Guid TenantId { get; private set; }

    public void AssignTenant(Guid tenantId) => TenantId = tenantId;

    public int Year { get; private set; }

    public bool IsLocked { get; private set; }

    public DateTimeOffset? LockedOn { get; private set; }

    private BudgetPeriod() { }

    public BudgetPeriod(int year)
    {
        if (year < DomainConstants.Budget.YearMin || year > DomainConstants.Budget.YearMax)
        {
            throw new ArgumentException(DomainConstants.Budget.YearInvalid, nameof(year));
        }

        Year = year;
    }

    public void Lock()
    {
        IsLocked = true;
        LockedOn = DateTimeOffset.UtcNow;
        LastModifiedOn = DateTimeOffset.UtcNow;
    }

    public void Unlock()
    {
        IsLocked = false;
        LockedOn = null;
        LastModifiedOn = DateTimeOffset.UtcNow;
    }
}
