using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("RolePermissions");
        builder.Property(item => item.Permission).HasMaxLength(80).IsRequired();
        builder.HasIndex(item => new { item.RoleId, item.Permission }).IsUnique();
        builder.HasOne(item => item.Role)
            .WithMany()
            .HasForeignKey(item => item.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
