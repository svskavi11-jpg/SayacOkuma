using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;

namespace Westalife.Infrastructure.Auth;

public sealed class ResidentAccess : IResidentAccess
{
    private readonly IHttpContextAccessor _http;
    private readonly UserManager<AppUser> _users;
    private readonly IApplicationDbContext _db;

    public ResidentAccess(IHttpContextAccessor http, UserManager<AppUser> users, IApplicationDbContext db)
    {
        _http = http;
        _users = users;
        _db = db;
    }

    public async Task<ResidentScope> RequireAsync(CancellationToken cancellationToken)
    {
        var raw = _http.HttpContext?.User.FindFirst(DomainConstants.Auth.UserIdClaim)?.Value;
        if (!Guid.TryParse(raw, out var userId))
        {
            throw new ForbiddenException("Oturum doğrulanamadı.");
        }

        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null || !await _users.IsInRoleAsync(user, DomainConstants.Auth.Resident))
        {
            throw new ForbiddenException("Bu alana yalnızca sakinler girebilir.");
        }

        if (user.ApartmentId is not Guid apartmentId || user.TenantId is not Guid tenantId)
        {
            throw new ForbiddenException("Hesabınıza bir daire bağlı değil.");
        }

        var apartment = await _db.Apartments
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == apartmentId, cancellationToken);

        if (apartment is null || apartment.TenantId != tenantId)
        {
            throw new ForbiddenException("Daire hesabınıza bağlı değil.");
        }

        return new ResidentScope(user.Id, tenantId, apartment.Id);
    }
}
