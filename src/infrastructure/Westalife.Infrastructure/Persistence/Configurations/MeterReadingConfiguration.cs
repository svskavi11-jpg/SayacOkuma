using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class MeterReadingConfiguration : IEntityTypeConfiguration<MeterReading>
{
    public void Configure(EntityTypeBuilder<MeterReading> builder)
    {
        builder.ToTable("MeterReadings");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.IndexValue)
            .HasPrecision(DomainConstants.IndexPrecision, DomainConstants.IndexScale);

        builder.Property(r => r.ConsumptionKWh)
            .HasPrecision(DomainConstants.EnergyPrecision, DomainConstants.EnergyScale);

        // Aynı sayacın aynı anndaki okuması iki kez kaydedilemez (idempotency).
        builder.HasIndex(r => new { r.MeterId, r.ReadingDateUtc })
            .IsUnique()
            .HasDatabaseName("UX_MeterReadings_MeterId_ReadingDateUtc");

        builder.HasIndex(r => r.ReadingDateUtc)
            .HasDatabaseName("IX_MeterReadings_ReadingDateUtc");

        builder.HasIndex(r => r.IsProcessed)
            .HasDatabaseName("IX_MeterReadings_IsProcessed");
    }
}
