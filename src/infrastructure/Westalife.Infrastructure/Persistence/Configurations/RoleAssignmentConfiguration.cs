using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Enums;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class RoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment>
{
    public void Configure(EntityTypeBuilder<RoleAssignment> builder)
    {
        builder.ToTable("RoleAssignments");
        builder.HasKey(r => r.Id);
        TenantOwnedConfiguration.Configure(builder);

        builder.Property(r => r.PersonName)
            .HasMaxLength(DomainConstants.RoleAssignment.PersonNameMaxLength)
            .IsRequired();
        builder.Property(r => r.RoleType).HasConversion<int>();
        builder.Property(r => r.ExemptionRate)
            .HasPrecision(DomainConstants.MoneyPrecision, DomainConstants.MoneyScale);

        builder.Property(r => r.PaymentType)
            .HasConversion<int>()
            .HasDefaultValue(RolePaymentType.Oran);

        builder.Property(r => r.FixedAmount)
            .HasPrecision(DomainConstants.MoneyPrecision, DomainConstants.MoneyScale);

        builder.Property(r => r.PeriodType)
            .HasConversion<int?>();

        builder.Property(r => r.IsCancelled)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(r => r.CancelledAt);

        builder.HasOne(r => r.Apartment)
            .WithMany()
            .HasForeignKey(r => r.ApartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.ApartmentId)
            .IsUnique()
            .HasFilter("\"EndDate\" IS NULL AND \"IsCancelled\" = FALSE")
            .HasDatabaseName("UX_RoleAssignments_OpenApartment");
    }
}
