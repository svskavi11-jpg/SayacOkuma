using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class ApartmentDimensionConfiguration : IEntityTypeConfiguration<ApartmentDimension>
{
    public void Configure(EntityTypeBuilder<ApartmentDimension> builder)
    {
        builder.ToTable("ApartmentDimensions", t =>
        {
            // ValidFrom her zaman ValidTo'dan önce olmalı (ValidTo NULL = sonsuz geçerli).
            t.HasCheckConstraint(
                "CK_ApartmentDimensions_Period",
                "\"ValidFrom\" < COALESCE(\"ValidTo\", 'infinity'::timestamptz)");
        });

        builder.HasKey(d => d.Id);

        builder.Property(d => d.SquareMeters)
            .HasPrecision(DomainConstants.RatioPrecision, DomainConstants.RatioScale);

        builder.Property(d => d.CommonAreaRatio)
            .HasPrecision(DomainConstants.RatioPrecision, DomainConstants.RatioScale);

        // Not: tstzrange tabanlı exclusion constraint (aralık çakışması yasağı) ve
        // "tek aktif kayıt" partial unique index'i migration içinde raw SQL ile eklenir
        // (EF Core native desteklemez) → bkz. InitialCreate migration.

        builder.HasIndex(d => new { d.ApartmentId, d.ValidFrom })
            .HasDatabaseName("IX_ApartmentDimensions_ApartmentId_ValidFrom");

        builder.HasIndex(d => d.ApartmentId)
            .HasDatabaseName("IX_ApartmentDimensions_ApartmentId");

        // Seed: her daire için 100 m²'lik başlangıç SCD kaydı (aktif, ValidTo NULL).
        // Gerçek değerler Faz 5'te arayüzden girildiğinde bu kayıt kapanıp yenisi açılacak (veri ezme yok).
        var apartmentCount = DomainConstants.SeedData.ApartmentCount;
        var defaultSquareMeters = DomainConstants.SeedData.DefaultSquareMeters;
        var defaultCommonAreaRatio = DomainConstants.SeedData.DefaultCommonAreaRatio;
        var seedTimestamp = DomainConstants.SeedData.SeedTimestamp;

        var dimensions = new List<object>(apartmentCount);
        for (var i = 1; i <= apartmentCount; i++)
        {
            dimensions.Add(new
            {
                Id = new Guid($"00000000-0000-0000-0001-{i:D12}"),
                ApartmentId = new Guid($"00000000-0000-0000-0000-{i:D12}"),
                SquareMeters = defaultSquareMeters,
                CommonAreaRatio = defaultCommonAreaRatio,
                ValidFrom = seedTimestamp,
                ValidTo = (DateTimeOffset?)null,
                CreatedOn = seedTimestamp,
                LastModifiedOn = (DateTimeOffset?)null
            });
        }

        builder.HasData(dimensions);
    }
}
