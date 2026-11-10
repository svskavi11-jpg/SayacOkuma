using Microsoft.AspNetCore.Authorization;
using Westalife.Infrastructure.Auth;

namespace Westalife.Api.Infrastructure;

public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission) : base(PermissionPolicyProvider.Prefix + permission)
    {
    }
}

/// <summary>Kodlardan herhangi biri varsa geçer. İki ayrı HasPermission birleşince VE olur; bu özellik VEYA içindir.</summary>
public sealed class HasAnyPermissionAttribute : AuthorizeAttribute
{
    public HasAnyPermissionAttribute(params string[] permissions)
        : base(PermissionPolicyProvider.Prefix + string.Join('|', permissions))
    {
    }
}
