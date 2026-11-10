using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Auth;

/// <summary>İlk kurulum. Veritabanında hiç kullanıcı yoksa süper yönetici ve site yöneticisini oluşturur.</summary>
public static class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        if (await users.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var options = scope.ServiceProvider.GetRequiredService<IOptions<AuthOptions>>().Value;
        if (string.IsNullOrWhiteSpace(options.SeedPassword))
        {
            throw new InvalidOperationException("İlk kurulum için Auth:SeedPassword ayarı zorunludur.");
        }

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();

        await EnsureRoleAsync(roles, DomainConstants.Auth.SuperAdmin);
        await EnsureRoleAsync(roles, DomainConstants.Auth.TenantAdmin);
        await EnsureRoleAsync(roles, DomainConstants.Auth.Accountant);
        await EnsureRoleAsync(roles, DomainConstants.Auth.MeterReader);
        await EnsureRoleAsync(roles, DomainConstants.Auth.Resident);

        await EnsureUserAsync(users, "superadmin@westalife.local", "Süper Yönetici", null, DomainConstants.Auth.SuperAdmin, options.SeedPassword);
        await EnsureUserAsync(
            users,
            "yonetici@westalife.local",
            "Site Yöneticisi",
            DomainConstants.SeedData.DefaultTenantId,
            DomainConstants.Auth.TenantAdmin,
            options.SeedPassword);
    }

    private static async Task EnsureRoleAsync(RoleManager<AppRole> roles, string name)
    {
        if (!await roles.RoleExistsAsync(name))
        {
            await roles.CreateAsync(new AppRole { Name = name });
        }
    }

    private static async Task EnsureUserAsync(
        UserManager<AppUser> users,
        string email,
        string fullName,
        Guid? tenantId,
        string role,
        string password)
    {
        if (await users.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName,
            TenantId = tenantId
        };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(string.Join(' ', created.Errors.Select(e => e.Description)));
        }

        await users.AddToRoleAsync(user, role);
    }
}
