using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;

namespace Westalife.Infrastructure.Auth;

public sealed class AccountSecurity : IAccountSecurity
{
    private readonly UserManager<AppUser> _users;
    private readonly RoleManager<AppRole> _roles;
    private readonly IHttpContextAccessor _http;
    private readonly ITenantContext _tenant;
    private readonly IApplicationDbContext _db;

    public AccountSecurity(
        UserManager<AppUser> users,
        RoleManager<AppRole> roles,
        IHttpContextAccessor http,
        ITenantContext tenant,
        IApplicationDbContext db)
    {
        _users = users;
        _roles = roles;
        _http = http;
        _tenant = tenant;
        _db = db;
    }

    public async Task ChangeOwnPasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken)
    {
        var user = await RequireCurrentUserAsync();
        if (string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
        {
            throw new DomainException("Yeni şifre mevcut şifreden farklı olmalıdır.");
        }

        var result = await _users.ChangePasswordAsync(user, currentPassword, newPassword);
        if (!result.Succeeded)
        {
            throw new DomainException(Describe(result.Errors));
        }
    }

    public async Task ResetStaffPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken)
    {
        RequireTenant();
        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null || user.TenantId != _tenant.TenantId)
        {
            throw new NotFoundException("Kullanıcı bulunamadı.");
        }

        var roles = await _users.GetRolesAsync(user);
        if (roles.Count == 0
            || roles.Contains(DomainConstants.Auth.Resident)
            || roles.Contains(DomainConstants.Auth.SuperAdmin))
        {
            throw new NotFoundException("Kullanıcı bulunamadı.");
        }

        await ApplyResetAsync(user, newPassword);
    }

    public async Task ResetResidentPasswordAsync(Guid apartmentId, string newPassword, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var apartment = await _db.Apartments
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == apartmentId, cancellationToken);
        if (apartment is null || apartment.TenantId != tenantId)
        {
            throw new NotFoundException("Daire", apartmentId);
        }

        var candidates = await _users.Users
            .Where(user => user.ApartmentId == apartmentId && user.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        AppUser? resident = null;
        foreach (var candidate in candidates)
        {
            var roles = await _users.GetRolesAsync(candidate);
            if (roles.Count == 1 && roles[0] == DomainConstants.Auth.Resident)
            {
                resident = candidate;
                break;
            }
        }

        if (resident is null)
        {
            throw new NotFoundException("Bu dairenin portal hesabı yok.");
        }

        await ApplyResetAsync(resident, newPassword);
    }

    public async Task<IReadOnlyList<StaffAccountItem>> ListStaffAsync(CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var users = await _users.Users
            .Where(user => user.TenantId == tenantId)
            .OrderBy(user => user.FullName)
            .ToListAsync(cancellationToken);

        var items = new List<StaffAccountItem>();
        foreach (var user in users)
        {
            var roles = await _users.GetRolesAsync(user);
            if (roles.Count == 0
                || roles.Contains(DomainConstants.Auth.Resident)
                || roles.Contains(DomainConstants.Auth.SuperAdmin))
            {
                continue;
            }

            var role = await _roles.FindByNameAsync(roles[0]);
            var label = role is null || string.IsNullOrWhiteSpace(role.DisplayName) ? roles[0] : role.DisplayName;
            items.Add(new StaffAccountItem(user.Id, user.FullName, user.Email ?? string.Empty, label));
        }

        return items;
    }

    private async Task ApplyResetAsync(AppUser user, string newPassword)
    {
        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var result = await _users.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            throw new DomainException(Describe(result.Errors));
        }

        await _users.ResetAccessFailedCountAsync(user);
        await _users.SetLockoutEndDateAsync(user, null);
    }

    private async Task<AppUser> RequireCurrentUserAsync()
    {
        var raw = _http.HttpContext?.User.FindFirst(DomainConstants.Auth.UserIdClaim)?.Value;
        if (!Guid.TryParse(raw, out var userId))
        {
            throw new UnauthorizedException("Oturum doğrulanamadı.");
        }

        return await _users.FindByIdAsync(userId.ToString())
            ?? throw new UnauthorizedException("Oturum doğrulanamadı.");
    }

    private Guid RequireTenant()
    {
        if (_tenant.TenantId == Guid.Empty)
        {
            throw new DomainException("Şifre sıfırlamak için bir site seçilmelidir.");
        }

        return _tenant.TenantId;
    }

    private static string Describe(IEnumerable<IdentityError> errors)
    {
        var message = string.Join(' ', errors.Select(error => error.Code switch
        {
            "PasswordMismatch" => "Mevcut şifre hatalı.",
            "PasswordTooShort" => "Şifre en az 8 karakter olmalıdır.",
            "PasswordRequiresDigit" => "Şifre en az bir rakam içermelidir.",
            "PasswordRequiresUpper" => "Şifre en az bir büyük harf içermelidir.",
            "PasswordRequiresLower" => "Şifre en az bir küçük harf içermelidir.",
            "PasswordRequiresNonAlphanumeric" => "Şifre en az bir özel karakter içermelidir.",
            "InvalidToken" => "Şifre sıfırlama doğrulanamadı.",
            _ => error.Description
        }));

        return string.IsNullOrWhiteSpace(message) ? "Şifre güncellenemedi." : message;
    }
}
