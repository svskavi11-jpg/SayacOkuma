using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Design;
using Westalife.Infrastructure.Persistence.Contexts;

namespace Westalife.Infrastructure.Persistence;

/// <summary>
/// dotnet-ef migrations için tasarım zamanı factory'si.
/// Migration üretimi sırasında API host'unun ayağa kalkması gerekmez.
/// Bağlantı dizesi geliştirme ortamı (Docker Compose, port 55432) varsayılanıdır.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public const string DesignTimeConnectionString =
        "Host=localhost;Port=55432;Database=sayacokuma;Username=sayacokuma;Password=SayaOkuma2026!;Include Error Detail=true";

    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(DesignTimeConnectionString)
            // EF 10'un bilinen yanlış pozitifi (AddInfrastructure ile aynı):
            // migration snapshot ile model eşitliği boş diff migration ile teyit edilmiştir.
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
