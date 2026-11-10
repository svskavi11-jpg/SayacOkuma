using Microsoft.AspNetCore.Identity;

namespace Westalife.Domain.Entities;

/// <summary>Giriş hesabı. Süper yöneticinin sitesi yoktur.</summary>
public class AppUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;

    public Guid? TenantId { get; set; }

    /// <summary>Sakin hesabının dairesi. Yalnız Resident rolünde doludur.</summary>
    public Guid? ApartmentId { get; set; }
}
