namespace Westalife.Domain.Common;

/// <summary>Siteye ait kök kayıt. Sorgu filtresi ve kayıt damgası bu sözleşmeyi kullanır.</summary>
public interface ITenantEntity
{
    Guid TenantId { get; }

    void AssignTenant(Guid tenantId);
}
