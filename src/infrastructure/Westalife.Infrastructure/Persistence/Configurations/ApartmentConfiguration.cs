using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class ApartmentConfiguration : IEntityTypeConfiguration<Apartment>
{
    public void Configure(EntityTypeBuilder<Apartment> builder)
    {
        builder.ToTable("Apartments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.DoorNumber)
            .IsRequired()
            .HasMaxLength(DomainConstants.Apartment.DoorNumberMaxLength);

        builder.Property(a => a.Title)
            .IsRequired()
            .HasMaxLength(DomainConstants.Apartment.TitleMaxLength);

        // Etap 6.8: devir bakiyesi — ANAPARA ve GEÇMİŞ FAİZ olarak ikiye ayrıldı (+ borçlu / - alacaklı).
        builder.Property(a => a.OpeningBalancePrincipal)
            .HasPrecision(DomainConstants.MoneyPrecision, DomainConstants.MoneyScale)
            .HasDefaultValue(0m);
        builder.Property(a => a.OpeningBalanceInterest)
            .HasPrecision(DomainConstants.MoneyPrecision, DomainConstants.MoneyScale)
            .HasDefaultValue(0m);
        builder.Property(a => a.OpeningBalanceDate);

        // Bir binada aynı kapı numarasında iki daire olamaz.
        builder.HasIndex(a => new { a.BuildingId, a.DoorNumber })
            .IsUnique();

        builder.HasOne(a => a.Building)
            .WithMany()
            .HasForeignKey(a => a.BuildingId)
            .OnDelete(DeleteBehavior.Restrict);

        // Faz 6.9: aidat tarifesı — Restrict: daireye atanmış tarife silinemez.
        builder.HasOne(a => a.AidatTariff)
            .WithMany()
            .HasForeignKey(a => a.AidatTariffId)
            .OnDelete(DeleteBehavior.Restrict);

        // SCD Type 2 kayıtları daire silinse de geçmiş denetim için korunamaz;
        // sistem gereği daire asla fiziksel silinmez (IsActive ile pasiflenir), bu yüzden cascade güvenlidir.
        builder.HasMany(a => a.Dimensions)
            .WithOne(d => d.Apartment)
            .HasForeignKey(d => d.ApartmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Etap 6.7: kat maliki SCD Type 2 kayıtları (ApartmentOwners tablosu).
        builder.HasMany(a => a.Owners)
            .WithOne(o => o.Apartment)
            .HasForeignKey(o => o.ApartmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seed: 72 daire (DoorNumber: 1..72). Gerçek m² değerleri Faz 5'te girilecek.
        var buildingId = DomainConstants.SeedData.BuildingId;
        var seedTimestamp = DomainConstants.SeedData.SeedTimestamp;
        var apartmentCount = DomainConstants.SeedData.ApartmentCount;

        var apartments = new List<object>(apartmentCount);
        for (var i = 1; i <= apartmentCount; i++)
        {
            apartments.Add(new
            {
                Id = new Guid($"00000000-0000-0000-0000-{i:D12}"),
                BuildingId = buildingId,
                DoorNumber = i.ToString(),
                Title = $"Daire {i}",
                IsActive = true,
                OpeningBalancePrincipal = 0m,
                OpeningBalanceInterest = 0m,
                OpeningBalanceDate = (DateOnly?)null,
                AidatTariffId = (Guid?)null,
                CreatedOn = seedTimestamp,
                LastModifiedOn = (DateTimeOffset?)null
            });
        }

        builder.HasData(apartments);
    }
}
