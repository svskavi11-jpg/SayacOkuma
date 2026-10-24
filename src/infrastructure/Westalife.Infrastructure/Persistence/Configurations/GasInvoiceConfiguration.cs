using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class GasInvoiceConfiguration : IEntityTypeConfiguration<GasInvoice>
{
    public void Configure(EntityTypeBuilder<GasInvoice> builder)
    {
        builder.ToTable("GasInvoices");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.InvoiceNumber)
            .IsRequired()
            .HasMaxLength(DomainConstants.GasInvoice.InvoiceNumberMaxLength);

        builder.Property(i => i.SubscriberNumber)
            .IsRequired()
            .HasMaxLength(DomainConstants.GasInvoice.SubscriberNumberMaxLength);

        builder.Property(i => i.MasterMeterSerial)
            .IsRequired()
            .HasMaxLength(DomainConstants.GasInvoice.MeterSerialMaxLength);

        // Aynı fatura numarası iki kez girilemez.
        builder.HasIndex(i => i.InvoiceNumber)
            .IsUnique()
            .HasDatabaseName("UX_GasInvoices_InvoiceNumber");

        builder.Property(i => i.CorrectedVolumeM3)
            .HasPrecision(DomainConstants.VolumePrecision, DomainConstants.VolumeScale);

        builder.Property(i => i.TotalEnergyKWh)
            .HasPrecision(DomainConstants.EnergyPrecision, DomainConstants.EnergyScale);

        builder.Property(i => i.PressureCorrectionFactor)
            .HasPrecision(9, 6);

        builder.Property(i => i.ConsumptionAmount)
            .HasPrecision(DomainConstants.MoneyPrecision, DomainConstants.MoneyScale);

        builder.Property(i => i.ConsumptionVat)
            .HasPrecision(DomainConstants.MoneyPrecision, DomainConstants.MoneyScale);

        builder.Property(i => i.DistributionAmount)
            .HasPrecision(DomainConstants.MoneyPrecision, DomainConstants.MoneyScale);

        builder.Property(i => i.DistributionVat)
            .HasPrecision(DomainConstants.MoneyPrecision, DomainConstants.MoneyScale);

        builder.Property(i => i.TotalAmount)
            .HasPrecision(DomainConstants.MoneyPrecision, DomainConstants.MoneyScale);

        // Etap 6.7: fatura duyuru/not alanı — PDF "Ek Açıklamalar" kutusuna basılır.
        builder.Property(i => i.Note)
            .HasMaxLength(DomainConstants.GasInvoice.NoteMaxLength);

        // Dönem kilidi (Etap 6.6): varsayılan false; LockedOn denetim izi için opsiyonel timestamptz.
        builder.Property(i => i.IsLocked)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(i => i.LockedOn);

        builder.HasIndex(i => i.IsLocked)
            .HasDatabaseName("IX_GasInvoices_IsLocked");

        builder.HasIndex(i => new { i.ReadingDateFrom, i.ReadingDateTo })
            .HasDatabaseName("IX_GasInvoices_ReadingPeriod");

        // Not: fatura dönemlerinin çakışmaması (tstzrange exclusion) migration içinde raw SQL ile eklenir.
    }
}
