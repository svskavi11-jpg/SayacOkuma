using Westalife.Domain.Common;
using Westalife.Domain.Constants;

namespace Westalife.Domain.Entities;

/// <summary>Site (müşteri). Her sitenin kendi daire, fatura ve ayar kayıtları vardır.</summary>
public class Tenant : BaseEntity
{
    public string Name { get; private set; } = string.Empty;

    public string Address { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;

    private Tenant() { }

    public Tenant(string name, string? address = null)
    {
        Rename(name);
        SetAddress(address);
    }

    public void UpdateDetails(string name, string? address)
    {
        Rename(name);
        SetAddress(address);
        LastModifiedOn = DateTimeOffset.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        LastModifiedOn = DateTimeOffset.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        LastModifiedOn = DateTimeOffset.UtcNow;
    }

    private void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.TenantNameRequired, nameof(name));
        }

        var trimmed = name.Trim();
        if (trimmed.Length > DomainConstants.Tenant.NameMaxLength)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.TenantNameTooLong, nameof(name));
        }

        Name = trimmed;
    }

    private void SetAddress(string? address)
    {
        var trimmed = address?.Trim() ?? string.Empty;
        if (trimmed.Length > DomainConstants.Tenant.AddressMaxLength)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.TenantAddressTooLong, nameof(address));
        }

        Address = trimmed;
    }
}
