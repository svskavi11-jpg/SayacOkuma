using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;

namespace Westalife.Infrastructure.Auth;

/// <summary>HTTP isteği yokken (migration, test) mevcut tek siteyi kullanır.</summary>
public sealed class AmbientTenantContext : ITenantContext
{
    public Guid TenantId => DomainConstants.SeedData.DefaultTenantId;

    public bool BypassTenantFilter => false;
}
