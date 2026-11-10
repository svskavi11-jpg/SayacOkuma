using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Infrastructure.Persistence.Contexts;

namespace Westalife.Infrastructure.Auth;

public sealed class PermissionRequirement : IAuthorizationRequirement
{
    public PermissionRequirement(params string[] permissions) => Permissions = permissions;

    public IReadOnlyList<string> Permissions { get; }
}

public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    public const string Prefix = "perm:";

    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options) =>
        _fallback = new DefaultAuthorizationPolicyProvider(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return _fallback.GetPolicyAsync(policyName);
        }

        var codes = policyName[Prefix.Length..]
            .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new PermissionRequirement(codes))
            .Build();
        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private const string CacheKey = "westalife.permissions";

    private readonly IHttpContextAccessor _http;
    private readonly UserManager<AppUser> _users;
    private readonly ApplicationDbContext _db;

    public PermissionAuthorizationHandler(
        IHttpContextAccessor http,
        UserManager<AppUser> users,
        ApplicationDbContext db)
    {
        _http = http;
        _users = users;
        _db = db;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.IsInRole(DomainConstants.Auth.SuperAdmin))
        {
            context.Succeed(requirement);
            return;
        }

        var http = _http.HttpContext;
        if (http is null)
        {
            return;
        }

        if (http.Items[CacheKey] is not HashSet<string> permissions)
        {
            permissions = await LoadAsync(context);
            http.Items[CacheKey] = permissions;
        }

        if (requirement.Permissions.Any(permissions.Contains))
        {
            context.Succeed(requirement);
        }
    }

    private async Task<HashSet<string>> LoadAsync(AuthorizationHandlerContext context)
    {
        var raw = context.User.FindFirst(DomainConstants.Auth.UserIdClaim)?.Value;
        if (!Guid.TryParse(raw, out var userId))
        {
            return [];
        }

        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return [];
        }

        var names = await _users.GetRolesAsync(user);
        if (names.Count == 0)
        {
            return [];
        }

        var roleIds = await _db.Roles
            .AsNoTracking()
            .Where(role => role.Name != null && names.Contains(role.Name))
            .Select(role => role.Id)
            .ToListAsync();

        var codes = await _db.RolePermissions
            .AsNoTracking()
            .Where(item => roleIds.Contains(item.RoleId))
            .Select(item => item.Permission)
            .ToListAsync();

        return codes.ToHashSet(StringComparer.Ordinal);
    }
}
