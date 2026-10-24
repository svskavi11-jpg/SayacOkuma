using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class MeterConfiguration : IEntityTypeConfiguration<Meter>
{
    public void Configure(EntityTypeBuilder<Meter> builder)
    {
        builder.ToTable("Meters");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.SerialNumber)
            .IsRequired()
            .HasMaxLength(DomainConstants.Meter.SerialNumberMaxLength);

        builder.Property(m => m.Manufacturer)
            .IsRequired()
            .HasMaxLength(DomainConstants.Meter.ManufacturerMaxLength);

        builder.Property(m => m.MbusPrimaryAddress);

        // Seri numarası tüm sayaç tiplerinde unique (M-Bus adresi benzersiz kimliktir).
        builder.HasIndex(m => m.SerialNumber)
            .IsUnique()
            .HasDatabaseName("UX_Meters_SerialNumber");

        // Kalorimetre bir daireye bağlıdır; ana sayaç (GasMasterMeter) ApartmentId = NULL.
        builder.HasOne(m => m.Apartment)
            .WithMany(a => a.Meters)
            .HasForeignKey(m => m.ApartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Okuma geçmişi kaybolmasın: sayaç silinemez, okumalar Restrict.
        builder.HasMany(m => m.Readings)
            .WithOne(r => r.Meter)
            .HasForeignKey(r => r.MeterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
