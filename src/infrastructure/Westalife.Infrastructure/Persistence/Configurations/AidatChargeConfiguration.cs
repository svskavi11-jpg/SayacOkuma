using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class AidatChargeConfiguration : IEntityTypeConfiguration<AidatCharge>
{
    public void Configure(EntityTypeBuilder<AidatCharge> builder)
    {
        builder.ToTable("AidatCharges");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Amount)
            .HasPrecision(DomainConstants.MoneyPrecision, DomainConstants.MoneyScale);

        // Bir daireye aynı tahakkuk döneminde tek borç satırı açılır.
        builder.HasIndex(c => new { c.AidatInvoiceId, c.ApartmentId })
            .IsUnique()
            .HasDatabaseName("UX_AidatCharges_Invoice_Apartment");

        builder.HasIndex(c => c.ApartmentId)
            .HasDatabaseName("IX_AidatCharges_ApartmentId");

        builder.HasOne(c => c.AidatInvoice)
            .WithMany(i => i.Charges)
            .HasForeignKey(c => c.AidatInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Apartment)
            .WithMany()
            .HasForeignKey(c => c.ApartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
