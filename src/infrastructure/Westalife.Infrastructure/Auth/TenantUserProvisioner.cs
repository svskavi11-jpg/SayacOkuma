using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;

namespace Westalife.Infrastructure.Auth;

public sealed class TenantUserProvisioner : ITenantUserProvisioner
{
    private readonly UserManager<AppUser> _users;
    private readonly RoleManager<AppRole> _roles;

    public TenantUserProvisioner(UserManager<AppUser> users, RoleManager<AppRole> roles)
    {
        _users = users;
        _roles = roles;
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) =>
        _users.Users.AnyAsync(user => user.NormalizedEmail == email.Trim().ToUpperInvariant(), cancellationToken);

    public async Task<string> RequireAssignableRoleNameAsync(Guid roleId, Guid tenantId, CancellationToken cancellationToken)
    {
        var role = await _roles.Roles.FirstOrDefaultAsync(item => item.Id == roleId, cancellationToken)
            ?? throw new NotFoundException("Rol", roleId);

        var assignableSystem = role.IsSystem && role.Name is DomainConstants.Auth.TenantAdmin
            or DomainConstants.Auth.Accountant
            or DomainConstants.Auth.MeterReader;
        var assignableCustom = !role.IsSystem && role.TenantId == tenantId;
        if (!assignableSystem && !assignableCustom)
        {
            throw new DomainException("Bu rol personele verilemez.");
        }

        return role.Name ?? throw new DomainException("Rol bulunamadı.");
    }

    public async Task<Guid> CreateAsync(
        string email,
        string password,
        string fullName,
        Guid tenantId,
        string role,
        CancellationToken cancellationToken)
    {
        var identityRole = await _roles.FindByNameAsync(role)
            ?? throw new DomainException("Rol bulunamadı.");
        if (identityRole.Name is DomainConstants.Auth.SuperAdmin or DomainConstants.Auth.Resident)
        {
            throw new DomainException("Bu rol site kullanıcısına verilemez.");
        }

        var user = new AppUser
        {
            UserName = email.Trim(),
            Email = email.Trim(),
            EmailConfirmed = true,
            FullName = fullName.Trim(),
            TenantId = tenantId
        };

        var created = await _users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            throw new DomainException(Describe(created.Errors));
        }

        var membership = await _users.AddToRoleAsync(user, role);
        if (!membership.Succeeded)
        {
            throw new DomainException(Describe(membership.Errors));
        }

        return user.Id;
    }

    public async Task<Guid> ProvisionResidentAsync(
        string email,
        string password,
        string fullName,
        Guid tenantId,
        Guid apartmentId,
        CancellationToken cancellationToken)
    {
        if (!await _roles.RoleExistsAsync(DomainConstants.Auth.Resident))
        {
            var createdRole = await _roles.CreateAsync(new AppRole
            {
                Id = DomainConstants.Auth.ResidentRoleId,
                Name = DomainConstants.Auth.Resident
            });
            if (!createdRole.Succeeded)
            {
                throw new DomainException(Describe(createdRole.Errors));
            }
        }

        var existing = await _users.FindByEmailAsync(email.Trim());
        if (existing is null)
        {
            var user = new AppUser
            {
                UserName = email.Trim(),
                Email = email.Trim(),
                EmailConfirmed = true,
                FullName = fullName.Trim(),
                TenantId = tenantId,
                ApartmentId = apartmentId
            };

            var created = await _users.CreateAsync(user, password);
            if (!created.Succeeded)
            {
                throw new DomainException(Describe(created.Errors));
            }

            var membership = await _users.AddToRoleAsync(user, DomainConstants.Auth.Resident);
            if (!membership.Succeeded)
            {
                throw new DomainException(Describe(membership.Errors));
            }

            return user.Id;
        }

        var roles = await _users.GetRolesAsync(existing);
        var isResident = roles.Contains(DomainConstants.Auth.Resident);
        var isStaff = roles.Any(role => role != DomainConstants.Auth.Resident);
        if (!isResident || isStaff)
        {
            throw new DomainException("Bu e-posta personel hesabına ait.");
        }

        if (existing.TenantId != tenantId)
        {
            throw new DomainException("Bu e-posta başka bir siteye kayıtlı.");
        }

        existing.FullName = fullName.Trim();
        existing.TenantId = tenantId;
        existing.ApartmentId = apartmentId;
        var updated = await _users.UpdateAsync(existing);
        if (!updated.Succeeded)
        {
            throw new DomainException(Describe(updated.Errors));
        }

        if (await _users.HasPasswordAsync(existing))
        {
            var removed = await _users.RemovePasswordAsync(existing);
            if (!removed.Succeeded)
            {
                throw new DomainException(Describe(removed.Errors));
            }
        }

        var added = await _users.AddPasswordAsync(existing, password);
        if (!added.Succeeded)
        {
            throw new DomainException(Describe(added.Errors));
        }

        return existing.Id;
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountByTenantAsync(CancellationToken cancellationToken)
    {
        var rows = await _users.Users
            .Where(user => user.TenantId != null)
            .GroupBy(user => user.TenantId!.Value)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Key, row => row.Count);
    }

    private static string Describe(IEnumerable<IdentityError> errors)
    {
        var message = string.Join(' ', errors.Select(error => error.Code switch
        {
            "DuplicateEmail" or "DuplicateUserName" => "Bu e-posta zaten kayıtlı.",
            "PasswordTooShort" => "Şifre en az 8 karakter olmalıdır.",
            "PasswordRequiresDigit" => "Şifre en az bir rakam içermelidir.",
            "PasswordRequiresUpper" => "Şifre en az bir büyük harf içermelidir.",
            "PasswordRequiresLower" => "Şifre en az bir küçük harf içermelidir.",
            "PasswordRequiresNonAlphanumeric" => "Şifre en az bir özel karakter içermelidir.",
            _ => error.Description
        }));

        return string.IsNullOrWhiteSpace(message) ? "Kullanıcı oluşturulamadı." : message;
    }
}
