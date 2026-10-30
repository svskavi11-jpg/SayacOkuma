using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class PaymentAllocationConfiguration : IEntityTypeConfiguration<PaymentAllocation>
{
    public void Configure(EntityTypeBuilder<PaymentAllocation> builder)
    {
        builder.ToTable("PaymentAllocations");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.AllocatedAmount)
            .HasPrecision(DomainConstants.MoneyPrecision, DomainConstants.MoneyScale);

        // Target: enum → int (1=OpeningBalance, 2=Invoice, 3=Interest)
        builder.Property(a => a.Target)
            .HasConversion<int>();

        builder.HasOne(a => a.ApartmentInvoice)
            .WithMany()
            .HasForeignKey(a => a.ApartmentInvoiceId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.PaymentId)
            .HasDatabaseName("IX_PaymentAllocations_PaymentId");

        builder.HasIndex(a => a.ApartmentInvoiceId)
            .HasDatabaseName("IX_PaymentAllocations_ApartmentInvoiceId");
    }
}
