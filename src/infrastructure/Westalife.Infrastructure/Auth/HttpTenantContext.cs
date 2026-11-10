using Microsoft.AspNetCore.Http;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;

namespace Westalife.Infrastructure.Auth;

/// <summary>JWT varsa siteyi token'dan okur. Süper yönetici X-Tenant-Id ile bir site seçer.</summary>
public sealed class HttpTenantContext : ITenantContext
{
    private readonly IHttpContextAccessor _http;

    public HttpTenantContext(IHttpContextAccessor http) => _http = http;

    public Guid TenantId
    {
        get
        {
            var user = _http.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return DomainConstants.SeedData.DefaultTenantId;
            }

            if (user.IsInRole(DomainConstants.Auth.SuperAdmin))
            {
                return TryImpersonatedTenant(out var impersonated) ? impersonated : Guid.Empty;
            }

            var claim = user.FindFirst(DomainConstants.Auth.TenantIdClaim)?.Value;
            return Guid.TryParse(claim, out var tenantId) ? tenantId : Guid.Empty;
        }
    }

    public bool BypassTenantFilter
    {
        get
        {
            var user = _http.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return false;
            }

            return user.IsInRole(DomainConstants.Auth.SuperAdmin) && !TryImpersonatedTenant(out _);
        }
    }

    private bool TryImpersonatedTenant(out Guid tenantId)
    {
        tenantId = Guid.Empty;
        var raw = _http.HttpContext?.Request.Headers[DomainConstants.Auth.TenantHeader].ToString();
        return Guid.TryParse(raw, out tenantId) && tenantId != Guid.Empty;
    }
}
