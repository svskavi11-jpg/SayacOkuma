using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class WaiverOffsetConfiguration : IEntityTypeConfiguration<WaiverOffset>
{
    public void Configure(EntityTypeBuilder<WaiverOffset> builder)
    {
        builder.ToTable("WaiverOffsets");
        builder.HasKey(w => w.Id);

        builder.Property(w => w.Amount)
            .HasPrecision(DomainConstants.MoneyPrecision, DomainConstants.MoneyScale);
        builder.Property(w => w.Description)
            .HasMaxLength(DomainConstants.WaiverOffset.DescriptionMaxLength)
            .IsRequired();

        builder.Property(w => w.IsCancelled)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(w => w.CancelledAt);

        builder.HasOne(w => w.AidatCharge)
            .WithMany()
            .HasForeignKey(w => w.AidatChargeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.RoleAssignment)
            .WithMany()
            .HasForeignKey(w => w.RoleAssignmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Apartment>()
            .WithMany()
            .HasForeignKey(w => w.ApartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(w => w.BudgetItem)
            .WithMany()
            .HasForeignKey(w => w.BudgetItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(w => w.AidatChargeId)
            .IsUnique()
            .HasFilter("\"IsCancelled\" = FALSE")
            .HasDatabaseName("UX_WaiverOffsets_AidatChargeId");
        builder.HasIndex(w => w.ApartmentId).HasDatabaseName("IX_WaiverOffsets_ApartmentId");
        builder.HasIndex(w => w.BudgetItemId).HasDatabaseName("IX_WaiverOffsets_BudgetItemId");
    }
}
