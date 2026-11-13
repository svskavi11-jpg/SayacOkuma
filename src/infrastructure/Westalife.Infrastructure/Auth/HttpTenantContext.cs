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
        var headers = _http.HttpContext?.Request.Headers;
        if (headers is null || !headers.TryGetValue(DomainConstants.Auth.TenantHeader, out var values))
        {
            return false;
        }

        foreach (var value in values)
        {
            foreach (var part in (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var text = part.Trim().Trim('"');
                if (Guid.TryParse(text, out var parsed) && parsed != Guid.Empty)
                {
                    tenantId = parsed;
                    return true;
                }
            }
        }

        return false;
    }
}
