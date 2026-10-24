using Westalife.Domain.Common;
using Westalife.Domain.Constants;

namespace Westalife.Domain.Entities;

/// <summary>
/// Bina (Westalife Residence). 30/70 hesabındaki sabit gider oranı burada tutulur;
/// hesaplama motoru (Faz 3) oranı koddan değil bu kayıttan okur.
/// </summary>
public class Building : BaseEntity
{
    public string Name { get; private set; } = string.Empty;

    public string Address { get; private set; } = string.Empty;

    /// <summary>Sabit gider oranı (0.30 = %30). Kalan oran (1 - bu değer) tüketim gideridir.</summary>
    public decimal FixedCostRatio { get; private set; }

    public bool IsActive { get; private set; } = true;

    private Building() { } // EF Core

    public Building(string name, string address, decimal fixedCostRatio = DomainConstants.Building.DefaultFixedCostRatio)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.BuildingNameRequired, nameof(name));
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.BuildingAddressRequired, nameof(address));
        }

        Name = name.Trim();
        Address = address.Trim();
        FixedCostRatio = fixedCostRatio;
    }

    /// <summary>Tüketim gideri oranı (%70 varsayılan). Sabit gider oranının kalanıdır.</summary>
    public decimal ConsumptionCostRatio => 1m - FixedCostRatio;

    public void UpdateDetails(string name, string address)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.BuildingNameRequired, nameof(name));
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.BuildingAddressRequired, nameof(address));
        }

        Name = name.Trim();
        Address = address.Trim();
        LastModifiedOn = DateTimeOffset.UtcNow;
    }

    public void UpdateFixedCostRatio(decimal ratio)
    {
        FixedCostRatio = ratio;
        LastModifiedOn = DateTimeOffset.UtcNow;
    }
}
