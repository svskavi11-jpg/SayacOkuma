using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenants");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(DomainConstants.Tenant.NameMaxLength);

        builder.Property(t => t.Address)
            .IsRequired()
            .HasMaxLength(DomainConstants.Tenant.AddressMaxLength)
            .HasDefaultValue(string.Empty);

        builder.HasData(new
        {
            Id = DomainConstants.SeedData.DefaultTenantId,
            Name = DomainConstants.SeedData.DefaultTenantName,
            Address = string.Empty,
            IsActive = true,
            CreatedOn = DomainConstants.SeedData.SeedTimestamp,
            LastModifiedOn = (DateTimeOffset?)null
        });
    }
}
