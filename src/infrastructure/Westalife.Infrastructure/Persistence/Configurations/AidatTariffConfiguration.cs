using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class AidatTariffConfiguration : IEntityTypeConfiguration<AidatTariff>
{
    public void Configure(EntityTypeBuilder<AidatTariff> builder)
    {
        builder.ToTable("AidatTariffs");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(DomainConstants.AidatTariff.NameMaxLength);

        // Aynı ada sahip iki tarife olamaz ("1+1 C" tek tanım).
        builder.HasIndex(t => t.Name)
            .IsUnique()
            .HasDatabaseName("UX_AidatTariffs_Name");

        builder.Property(t => t.SquareMeters)
            .HasPrecision(DomainConstants.AidatTariff.SquareMetersPrecision, DomainConstants.AidatTariff.SquareMetersScale);

        builder.Property(t => t.Amount)
            .HasPrecision(DomainConstants.MoneyPrecision, DomainConstants.MoneyScale);

        builder.Property(t => t.IsActive)
            .IsRequired()
            .HasDefaultValue(true);
    }
}
