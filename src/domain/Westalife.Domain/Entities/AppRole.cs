using Microsoft.AspNetCore.Identity;

namespace Westalife.Domain.Entities;

/// <summary>Giriş rolü. Daire görev kaydı olan RoleAssignment ile ilgisi yoktur.</summary>
public class AppRole : IdentityRole<Guid>
{
    /// <summary>Boşsa sistem rolüdür ve her site görür. Doluysa rol yalnızca o siteye aittir.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>Ekranda görünen ad. Özel rollerde Identity adı tenant kimliğiyle öneklenir.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Sistem rolleri silinemez ve yetki paketi sabittir.</summary>
    public bool IsSystem { get; set; }
}
