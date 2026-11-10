using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;

namespace Westalife.Infrastructure.Auth;

public sealed class JwtAuthService : IAuthService
{
    private readonly UserManager<AppUser> _users;
    private readonly StaffAccessReader _access;
    private readonly AuthOptions _options;

    public JwtAuthService(UserManager<AppUser> users, StaffAccessReader access, IOptions<AuthOptions> options)
    {
        _users = users;
        _access = access;
        _options = options.Value;
    }

    public async Task<LoginSession> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.SigningKey))
        {
            throw new InvalidOperationException("Auth:SigningKey ayari eksik.");
        }

        var user = await _users.FindByEmailAsync(email.Trim());
        if (user is null || !await _users.CheckPasswordAsync(user, password))
        {
            throw new UnauthorizedException("E-posta veya şifre hatalı.");
        }

        var (role, isSuperAdmin, permissions) = await _access.DescribeAsync(user, cancellationToken);
        var expires = DateTimeOffset.UtcNow.AddMinutes(_options.ExpiryMinutes <= 0 ? 480 : _options.ExpiryMinutes);
        var claims = new List<Claim>
        {
            new(DomainConstants.Auth.UserIdClaim, user.Id.ToString()),
            new(DomainConstants.Auth.RoleClaim, role),
            new(DomainConstants.Auth.FullNameClaim, user.FullName)
        };
        if (user.TenantId is Guid tenantId)
        {
            claims.Add(new Claim(DomainConstants.Auth.TenantIdClaim, tenantId.ToString()));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new LoginSession(
            new JwtSecurityTokenHandler().WriteToken(token),
            expires,
            user.Id,
            user.TenantId,
            role,
            user.FullName,
            isSuperAdmin,
            permissions);
    }
}
