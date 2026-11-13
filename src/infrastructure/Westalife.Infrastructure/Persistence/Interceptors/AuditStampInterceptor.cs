using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Westalife.Domain.Common;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Interceptors;

/// <summary>
/// BaseEntity satırlarına oturum damgası yazar. Identity tabloları BaseEntity olmadığı için dışarıda kalır.
/// SavingChanges, DetectChanges'ten sonra çalıştığı için LastModifiedBy elle IsModified işaretlenir.
/// </summary>
public sealed class AuditStampInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditStampInterceptor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var actor = ResolveActor();
        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(e => e.CreatedBy).CurrentValue = actor;
                if (entry.Entity is MeterReadingAuditLog)
                {
                    entry.Property(nameof(MeterReadingAuditLog.ChangedBy)).CurrentValue = actor;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                var modifiedBy = entry.Property(e => e.LastModifiedBy);
                modifiedBy.CurrentValue = actor;
                modifiedBy.IsModified = true;
            }
        }
    }

    private string ResolveActor()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            return AuditActor.System;
        }

        var name = user.FindFirstValue(DomainConstants.Auth.FullNameClaim);
        var id = user.FindFirstValue(DomainConstants.Auth.UserIdClaim);
        return AuditActor.Format(name, id);
    }
}
