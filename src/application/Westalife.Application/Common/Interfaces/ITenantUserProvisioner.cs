namespace Westalife.Application.Common.Interfaces;

/// <summary>Site kullanıcısı oluşturur. Identity altyapısı Application katmanına sızmaz.</summary>
public interface ITenantUserProvisioner
{
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);

    Task<string> RequireAssignableRoleNameAsync(Guid roleId, Guid tenantId, CancellationToken cancellationToken);

    Task<Guid> CreateAsync(
        string email,
        string password,
        string fullName,
        Guid tenantId,
        string role,
        CancellationToken cancellationToken);

    /// <summary>
    /// Sakin portal hesabı açar. Aynı sitedeki mevcut sakin hesabının şifresini ve dairesini günceller.
    /// </summary>
    Task<Guid> ProvisionResidentAsync(
        string email,
        string password,
        string fullName,
        Guid tenantId,
        Guid apartmentId,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, int>> CountByTenantAsync(CancellationToken cancellationToken);
}
