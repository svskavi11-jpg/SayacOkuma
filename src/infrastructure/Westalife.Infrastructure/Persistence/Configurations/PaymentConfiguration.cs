using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Amount)
            .HasPrecision(DomainConstants.MoneyPrecision, DomainConstants.MoneyScale);

        builder.Property(p => p.Description)
            .HasMaxLength(DomainConstants.Payment.DescriptionMaxLength);

        builder.Property(p => p.ReceivedBy)
            .IsRequired()
            .HasMaxLength(DomainConstants.Payment.ReceivedByMaxLength);

        builder.Property(p => p.CancellationReason)
            .HasMaxLength(DomainConstants.Payment.CancellationReasonMaxLength);

        builder.HasOne(p => p.Apartment)
            .WithMany()
            .HasForeignKey(p => p.ApartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Allocations)
            .WithOne(a => a.Payment)
            .HasForeignKey(a => a.PaymentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Aktif (iptal edilmemiş) ödemeleri hızlı getirmek için
        builder.HasIndex(p => new { p.ApartmentId, p.IsCancelled })
            .HasDatabaseName("IX_Payments_Apartment_Active");

        builder.HasIndex(p => p.PaymentDate)
            .HasDatabaseName("IX_Payments_PaymentDate");
    }
}
