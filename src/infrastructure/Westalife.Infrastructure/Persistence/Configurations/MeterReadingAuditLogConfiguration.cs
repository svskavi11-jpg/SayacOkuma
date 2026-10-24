using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

/// <summary>Okuma düzeltme denetim izi (Etap 6.7). Kayıtlar asla silinmez/güncellenmez (append-only).</summary>
public class MeterReadingAuditLogConfiguration : IEntityTypeConfiguration<MeterReadingAuditLog>
{
    public void Configure(EntityTypeBuilder<MeterReadingAuditLog> builder)
    {
        builder.ToTable("MeterReadingAuditLogs");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.OldIndexValue)
            .HasPrecision(DomainConstants.IndexPrecision, DomainConstants.IndexScale);

        builder.Property(l => l.NewIndexValue)
            .HasPrecision(DomainConstants.IndexPrecision, DomainConstants.IndexScale);

        builder.Property(l => l.OldConsumptionKWh)
            .HasPrecision(DomainConstants.EnergyPrecision, DomainConstants.EnergyScale);

        builder.Property(l => l.NewConsumptionKWh)
            .HasPrecision(DomainConstants.EnergyPrecision, DomainConstants.EnergyScale);

        builder.Property(l => l.Reason)
            .IsRequired()
            .HasMaxLength(DomainConstants.MeterReadingAuditLog.ReasonMaxLength);

        builder.Property(l => l.ChangedBy)
            .IsRequired()
            .HasMaxLength(DomainConstants.MeterReadingAuditLog.ChangedByMaxLength);

        builder.HasOne(l => l.MeterReading)
            .WithMany()
            .HasForeignKey(l => l.MeterReadingId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => l.MeterReadingId)
            .HasDatabaseName("IX_MeterReadingAuditLogs_MeterReadingId");

        builder.HasIndex(l => l.ChangedOnUtc)
            .HasDatabaseName("IX_MeterReadingAuditLogs_ChangedOnUtc");
    }
}