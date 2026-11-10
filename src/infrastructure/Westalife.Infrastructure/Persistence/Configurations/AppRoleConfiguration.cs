using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class AppRoleConfiguration : IEntityTypeConfiguration<AppRole>
{
    public void Configure(EntityTypeBuilder<AppRole> builder)
    {
        builder.Property(role => role.DisplayName).HasMaxLength(100).IsRequired();
        builder.HasIndex(role => role.TenantId);

        builder.HasData(
            Role(DomainConstants.Auth.SuperAdminRoleId, DomainConstants.Auth.SuperAdmin, "Süper Yönetici", "role-superadmin"),
            Role(DomainConstants.Auth.TenantAdminRoleId, DomainConstants.Auth.TenantAdmin, "Site yöneticisi", "role-tenantadmin"),
            Role(DomainConstants.Auth.AccountantRoleId, DomainConstants.Auth.Accountant, "Muhasebe", "role-accountant"),
            Role(DomainConstants.Auth.MeterReaderRoleId, DomainConstants.Auth.MeterReader, "Sayaç okuyucu", "role-meterreader"),
            Role(DomainConstants.Auth.ResidentRoleId, DomainConstants.Auth.Resident, "Sakin", "role-resident"));
    }

    private static object Role(Guid id, string name, string displayName, string stamp) => new
    {
        Id = id,
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        ConcurrencyStamp = stamp,
        DisplayName = displayName,
        IsSystem = true,
        TenantId = (Guid?)null
    };
}
