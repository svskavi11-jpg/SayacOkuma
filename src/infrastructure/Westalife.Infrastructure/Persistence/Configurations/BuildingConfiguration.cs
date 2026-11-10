using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class BuildingConfiguration : IEntityTypeConfiguration<Building>
{
    public void Configure(EntityTypeBuilder<Building> builder)
    {
        builder.ToTable("Buildings");

        builder.HasKey(b => b.Id);
        TenantOwnedConfiguration.Configure(builder);

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(DomainConstants.Building.NameMaxLength);

        builder.Property(b => b.Address)
            .IsRequired()
            .HasMaxLength(DomainConstants.Building.AddressMaxLength);

        builder.Property(b => b.FixedCostRatio)
            .HasPrecision(DomainConstants.RatioPrecision, DomainConstants.RatioScale);

        // Seed: Westalife Residence
        builder.HasData(new
        {
            Id = DomainConstants.SeedData.BuildingId,
            TenantId = DomainConstants.SeedData.DefaultTenantId,
            Name = "Westalife Residence",
            Address = "Zonguldak",
            FixedCostRatio = DomainConstants.Building.DefaultFixedCostRatio,
            IsActive = true,
            CreatedOn = DomainConstants.SeedData.SeedTimestamp,
            LastModifiedOn = (DateTimeOffset?)null
        });
    }
}
