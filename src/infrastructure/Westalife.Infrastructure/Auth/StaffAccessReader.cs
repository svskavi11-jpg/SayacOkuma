using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;
using Westalife.Infrastructure.Persistence.Contexts;

namespace Westalife.Infrastructure.Auth;

public sealed class StaffAccessReader : IAccessProfileReader
{
    private readonly IHttpContextAccessor _http;
    private readonly UserManager<AppUser> _users;
    private readonly ApplicationDbContext _db;

    public StaffAccessReader(IHttpContextAccessor http, UserManager<AppUser> users, ApplicationDbContext db)
    {
        _http = http;
        _users = users;
        _db = db;
    }

    public async Task<(string Label, bool IsSuperAdmin, IReadOnlyList<string> Permissions)> DescribeAsync(
        AppUser user,
        CancellationToken cancellationToken)
    {
        var names = await _users.GetRolesAsync(user);
        var isSuperAdmin = names.Contains(DomainConstants.Auth.SuperAdmin);
        var name = isSuperAdmin
            ? DomainConstants.Auth.SuperAdmin
            : names.FirstOrDefault() ?? string.Empty;

        if (isSuperAdmin)
        {
            return (name, true, Permissions.All);
        }

        var role = string.IsNullOrEmpty(name)
            ? null
            : await _db.Roles.AsNoTracking().FirstOrDefaultAsync(item => item.Name == name, cancellationToken);
        var label = role is { IsSystem: false } && !string.IsNullOrWhiteSpace(role.DisplayName)
            ? role.DisplayName
            : name;
        if (role is null)
        {
            return (label, false, []);
        }

        var codes = await _db.RolePermissions
            .AsNoTracking()
            .Where(item => item.RoleId == role.Id)
            .Select(item => item.Permission)
            .ToListAsync(cancellationToken);
        return (label, false, codes);
    }

    public async Task<AccessProfile> CurrentAsync(CancellationToken cancellationToken)
    {
        var raw = _http.HttpContext?.User.FindFirst(DomainConstants.Auth.UserIdClaim)?.Value;
        if (!Guid.TryParse(raw, out var userId))
        {
            throw new UnauthorizedException("Oturum doğrulanamadı.");
        }

        var user = await _users.FindByIdAsync(userId.ToString())
            ?? throw new UnauthorizedException("Oturum doğrulanamadı.");
        var (label, isSuperAdmin, permissions) = await DescribeAsync(user, cancellationToken);
        return new AccessProfile(label, user.FullName, isSuperAdmin, permissions);
    }
}
