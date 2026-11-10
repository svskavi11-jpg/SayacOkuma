using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Infrastructure.Persistence.Contexts;

namespace Westalife.Infrastructure.Auth;

/// <summary>Sistem rollerinin yetki paketini eksik satır kalmayacak şekilde tamamlar.</summary>
public static class PermissionSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rows = await db.RolePermissions.ToListAsync(cancellationToken);
        var known = rows.Select(item => (item.RoleId, item.Permission)).ToHashSet();

        foreach (var group in rows.GroupBy(item => item.RoleId))
        {
            Rewrite(
                db, known, group,
                [Permissions.Legacy.ReportsView, Permissions.Legacy.ReportsDownload],
                Permissions.Reports.Features);
            Rewrite(
                db, known, group,
                [Permissions.Legacy.SettingsView, Permissions.Legacy.SettingsEdit],
                Permissions.Settings.Features);
        }

        Add(db, known, DomainConstants.Auth.TenantAdminRoleId, Permissions.All);
        Add(db, known, DomainConstants.Auth.AccountantRoleId, Permissions.Accountant);
        Add(db, known, DomainConstants.Auth.MeterReaderRoleId, Permissions.MeterReader);

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static void Rewrite(
        ApplicationDbContext db,
        HashSet<(Guid RoleId, string Permission)> known,
        IGrouping<Guid, RolePermission> group,
        string[] oldCodes,
        IEnumerable<string> next)
    {
        var present = group.Where(item => oldCodes.Contains(item.Permission)).ToList();
        if (present.Count == 0)
        {
            return;
        }

        Add(db, known, group.Key, next);
        foreach (var row in present)
        {
            db.RolePermissions.Remove(row);
            known.Remove((row.RoleId, row.Permission));
        }
    }

    private static void Add(ApplicationDbContext db, HashSet<(Guid RoleId, string Permission)> known, Guid roleId, IEnumerable<string> codes)
    {
        foreach (var code in codes)
        {
            if (!known.Add((roleId, code)))
            {
                continue;
            }

            db.RolePermissions.Add(new RolePermission
            {
                Id = Guid.NewGuid(),
                RoleId = roleId,
                Permission = code
            });
        }
    }
}
