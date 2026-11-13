using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Westalife.Application;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Auth.ChangePassword;
using Westalife.Application.Features.Auth.Login;
using Westalife.Application.Features.Auth.ResetUserPassword;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;
using Westalife.Infrastructure.Persistence;
using Xunit;

namespace Westalife.Infrastructure.Tests;

public class AccountSecurityTests
{
    [Fact]
    public async Task Lockout_Reset_AndChangePassword_FollowTheRules()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:SigningKey"] = "test-signing-key-at-least-32-chars",
                ["Auth:Issuer"] = "westalife",
                ["Auth:Audience"] = "westalife",
            })
            .Build());
        services.AddDataProtection();
        services.AddAuthentication();
        services.AddInfrastructure(DesignTimeDbContextFactory.DesignTimeConnectionString);
        services.AddApplication();
        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();
        var db = provider.GetRequiredService<IApplicationDbContext>();
        var users = provider.GetRequiredService<ITenantUserProvisioner>();
        var manager = provider.GetRequiredService<UserManager<AppUser>>();
        var tenantId = await db.Apartments.Select(a => a.TenantId).FirstAsync();
        var email = $"kilit-{Guid.NewGuid():N}@test.local";
        const string first = "Kilit1!aA";
        const string second = "Sifir1!aB";
        const string third = "Yeni1!aC";
        Guid? created = null;

        try
        {
            created = await users.CreateAsync(email, first, "Kilit Test", tenantId, DomainConstants.Auth.TenantAdmin, CancellationToken.None);

            for (var attempt = 1; attempt <= 4; attempt++)
            {
                var wrong = await Assert.ThrowsAsync<UnauthorizedException>(() =>
                    mediator.Send(new LoginCommand(email, "yanlis-1")));
                Assert.Equal("E-posta veya şifre hatalı.", wrong.Message);
            }

            var locked = await Assert.ThrowsAsync<UnauthorizedException>(() =>
                mediator.Send(new LoginCommand(email, "yanlis-2")));
            Assert.Equal("Hesabınız 15 dakika süreyle kilitlendi.", locked.Message);

            var stillLocked = await Assert.ThrowsAsync<UnauthorizedException>(() =>
                mediator.Send(new LoginCommand(email, first)));
            Assert.Equal("Hesabınız 15 dakika süreyle kilitlendi.", stillLocked.Message);

            SignIn(provider, created.Value, tenantId);
            var missing = await Assert.ThrowsAsync<NotFoundException>(() =>
                mediator.Send(new ResetUserPasswordCommand(Guid.NewGuid(), second, PasswordResetTarget.Staff)));
            Assert.Contains("Kullanıcı bulunamadı", missing.Message);

            await mediator.Send(new ResetUserPasswordCommand(created.Value, second, PasswordResetTarget.Staff));
            var session = await mediator.Send(new LoginCommand(email, second));
            Assert.Equal(created.Value, session.UserId);

            var mismatch = await Assert.ThrowsAsync<DomainException>(() =>
                mediator.Send(new ChangePasswordCommand(first, third)));
            Assert.Contains("Mevcut şifre hatalı.", mismatch.Message);

            await mediator.Send(new ChangePasswordCommand(second, third));
            await mediator.Send(new LoginCommand(email, third));

            var taken = await manager.Users
                .Where(user => user.TenantId == tenantId && user.ApartmentId != null)
                .Select(user => user.ApartmentId!.Value)
                .ToListAsync();
            var apartmentId = await db.Apartments
                .Where(apartment => apartment.TenantId == tenantId && !taken.Contains(apartment.Id))
                .Select(apartment => apartment.Id)
                .FirstOrDefaultAsync();
            if (apartmentId != Guid.Empty)
            {
                var portal = await Assert.ThrowsAsync<NotFoundException>(() =>
                    mediator.Send(new ResetUserPasswordCommand(apartmentId, second, PasswordResetTarget.Resident)));
                Assert.Contains("portal hesabı yok", portal.Message);
            }
        }
        finally
        {
            if (created is Guid id)
            {
                var user = await manager.FindByIdAsync(id.ToString());
                if (user is not null)
                {
                    await manager.DeleteAsync(user);
                }
            }
        }
    }

    private static void SignIn(ServiceProvider provider, Guid userId, Guid tenantId)
    {
        var http = provider.GetRequiredService<IHttpContextAccessor>();
        var identity = new ClaimsIdentity(
            [
                new Claim(DomainConstants.Auth.UserIdClaim, userId.ToString()),
                new Claim(DomainConstants.Auth.TenantIdClaim, tenantId.ToString()),
            ],
            authenticationType: "Test");
        http.HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }
}
