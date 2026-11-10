using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;
using Westalife.Infrastructure.Persistence.Contexts;

namespace Westalife.Infrastructure.Auth;

public sealed class RoleAdmin : IRoleAdmin
{
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        DomainConstants.Auth.SuperAdmin,
        DomainConstants.Auth.TenantAdmin,
        DomainConstants.Auth.Accountant,
        DomainConstants.Auth.MeterReader,
        DomainConstants.Auth.Resident,
        "Süper Yönetici",
        "Site yöneticisi",
        "Muhasebe",
        "Sayaç okuyucu",
        "Sakin"
    };

    private static readonly string[] VisibleSystem =
    [
        DomainConstants.Auth.TenantAdmin,
        DomainConstants.Auth.Accountant,
        DomainConstants.Auth.MeterReader
    ];

    private readonly RoleManager<AppRole> _roles;
    private readonly UserManager<AppUser> _users;
    private readonly ApplicationDbContext _db;

    public RoleAdmin(RoleManager<AppRole> roles, UserManager<AppUser> users, ApplicationDbContext db)
    {
        _roles = roles;
        _users = users;
        _db = db;
    }

    public async Task<IReadOnlyList<RoleListItem>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var roles = await _roles.Roles
            .AsNoTracking()
            .Where(role => VisibleSystem.Contains(role.Name) || role.TenantId == tenantId)
            .OrderBy(role => role.IsSystem ? 0 : 1)
            .ThenBy(role => role.DisplayName)
            .ToListAsync(cancellationToken);

        var ids = roles.Select(role => role.Id).ToList();
        var grants = await _db.RolePermissions
            .AsNoTracking()
            .Where(item => ids.Contains(item.RoleId))
            .ToListAsync(cancellationToken);

        var items = new List<RoleListItem>(roles.Count);
        foreach (var role in roles)
        {
            var members = string.IsNullOrEmpty(role.Name) ? [] : await _users.GetUsersInRoleAsync(role.Name);
            var count = members.Count(user => user.TenantId == tenantId);
            var codes = grants
                .Where(item => item.RoleId == role.Id)
                .Select(item => item.Permission)
                .OrderBy(code => code, StringComparer.Ordinal)
                .ToArray();
            items.Add(new RoleListItem(role.Id, role.DisplayName, role.IsSystem, count, codes));
        }

        return items;
    }

    public async Task<Guid> CreateAsync(
        Guid tenantId,
        string displayName,
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken)
    {
        var name = displayName.Trim();
        await EnsureNameFreeAsync(tenantId, name, null, cancellationToken);
        var codes = Normalize(permissions);
        var role = new AppRole
        {
            Id = Guid.NewGuid(),
            Name = InternalName(tenantId, name),
            DisplayName = name,
            TenantId = tenantId,
            IsSystem = false
        };
        var created = await _roles.CreateAsync(role);
        if (!created.Succeeded)
        {
            throw new DomainException(string.Join(' ', created.Errors.Select(error => error.Description)));
        }

        Grant(role.Id, codes);
        await _db.SaveChangesAsync(cancellationToken);
        return role.Id;
    }

    public async Task UpdateAsync(
        Guid tenantId,
        Guid roleId,
        string displayName,
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken)
    {
        var role = await RequireCustomAsync(tenantId, roleId, cancellationToken);
        var name = displayName.Trim();
        await EnsureNameFreeAsync(tenantId, name, role.Id, cancellationToken);
        role.DisplayName = name;
        role.Name = InternalName(tenantId, name);
        var updated = await _roles.UpdateAsync(role);
        if (!updated.Succeeded)
        {
            throw new DomainException(string.Join(' ', updated.Errors.Select(error => error.Description)));
        }

        var current = await _db.RolePermissions.Where(item => item.RoleId == role.Id).ToListAsync(cancellationToken);
        _db.RolePermissions.RemoveRange(current);
        Grant(role.Id, Normalize(permissions));
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken)
    {
        var role = await RequireCustomAsync(tenantId, roleId, cancellationToken);
        var members = await _users.GetUsersInRoleAsync(role.Name ?? string.Empty);
        if (members.Count > 0)
        {
            throw new DomainException("Bu rolde kullanıcı var. Önce kullanıcıları başka role alın.");
        }

        var removed = await _roles.DeleteAsync(role);
        if (!removed.Succeeded)
        {
            throw new DomainException(string.Join(' ', removed.Errors.Select(error => error.Description)));
        }
    }

    private void Grant(Guid roleId, IReadOnlyList<string> codes)
    {
        foreach (var code in codes)
        {
            _db.RolePermissions.Add(new RolePermission
            {
                Id = Guid.NewGuid(),
                RoleId = roleId,
                Permission = code
            });
        }
    }

    private async Task<AppRole> RequireCustomAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken)
    {
        var role = await _roles.Roles.FirstOrDefaultAsync(item => item.Id == roleId, cancellationToken)
            ?? throw new NotFoundException("Rol", roleId);
        if (role.IsSystem || role.TenantId != tenantId)
        {
            throw new DomainException("Sistem rolleri değiştirilemez.");
        }

        return role;
    }

    private async Task EnsureNameFreeAsync(Guid tenantId, string displayName, Guid? exceptId, CancellationToken cancellationToken)
    {
        if (ReservedNames.Contains(displayName))
        {
            throw new DomainException("Bu rol adı sistemde ayrılmıştır.");
        }

        var taken = await _roles.Roles.AnyAsync(
            role => role.Id != exceptId
                && (role.TenantId == tenantId || role.IsSystem)
                && role.DisplayName.ToLower() == displayName.ToLower(),
            cancellationToken);
        if (taken)
        {
            throw new DomainException("Bu rol adı zaten kullanılıyor.");
        }
    }

    private static IReadOnlyList<string> Normalize(IReadOnlyList<string> permissions)
    {
        var codes = permissions
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (codes.Length == 0)
        {
            throw new DomainException("En az bir yetki seçin.");
        }

        var unknown = codes.FirstOrDefault(code => !Permissions.IsKnown(code));
        if (unknown is not null)
        {
            throw new DomainException($"Bilinmeyen yetki: {unknown}");
        }

        return codes;
    }

    private static string InternalName(Guid tenantId, string displayName) => $"{tenantId:N}:{displayName}";
}
