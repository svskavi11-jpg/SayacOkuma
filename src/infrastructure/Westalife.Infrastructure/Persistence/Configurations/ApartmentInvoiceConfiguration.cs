using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class ApartmentInvoiceConfiguration : IEntityTypeConfiguration<ApartmentInvoice>
{
    public void Configure(EntityTypeBuilder<ApartmentInvoice> builder)
    {
        builder.ToTable("ApartmentInvoices");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.SquareMetersUsed)
            .HasPrecision(DomainConstants.RatioPrecision, DomainConstants.RatioScale);

        builder.Property(i => i.ConsumptionKWhUsed)
            .HasPrecision(DomainConstants.EnergyPrecision, DomainConstants.EnergyScale);

        builder.Property(i => i.FixedAmount)
            .HasPrecision(DomainConstants.MoneyPrecision, DomainConstants.MoneyScale);

        builder.Property(i => i.ConsumptionAmount)
            .HasPrecision(DomainConstants.MoneyPrecision, DomainConstants.MoneyScale);

        builder.Property(i => i.TotalAmount)
            .HasPrecision(DomainConstants.MoneyPrecision, DomainConstants.MoneyScale);

        builder.HasOne(i => i.GasInvoice)
            .WithMany()
            .HasForeignKey(i => i.GasInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Apartment)
            .WithMany()
            .HasForeignKey(i => i.ApartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Faz 7.1: Hibrit Model borç sahipliği — borç sahibi sakin + müteselsil ev sahibi.
        builder.HasOne(i => i.ApartmentOwner)
            .WithMany()
            .HasForeignKey(i => i.ApartmentOwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.LegalResponsibleOwner!)
            .WithMany()
            .HasForeignKey(i => i.LegalResponsibleOwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.ApartmentOwnerId)
            .HasDatabaseName("IX_ApartmentInvoices_ApartmentOwnerId");

        // Bir fatura için bir dairenin en fazla 1 GEÇERLİ satırı olabilir;
        // superseded satırlar ayrık tutulur (audit trail).
        builder.HasIndex(i => new { i.GasInvoiceId, i.ApartmentId })
            .IsUnique()
            .HasFilter("\"IsSuperseded\" = false")
            .HasDatabaseName("UX_ApartmentInvoices_GasInvoice_Apartment_Active");

        builder.HasIndex(i => i.IsSuperseded)
            .HasDatabaseName("IX_ApartmentInvoices_IsSuperseded");

        builder.HasIndex(i => i.GasInvoiceId)
            .HasDatabaseName("IX_ApartmentInvoices_GasInvoiceId");
    }
}
