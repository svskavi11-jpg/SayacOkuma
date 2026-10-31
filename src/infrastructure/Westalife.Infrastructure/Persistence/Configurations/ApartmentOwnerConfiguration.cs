using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

/// <summary>Kat maliki SCD Type 2 kayıtları (Etap 6.7). Çakışma yasağı exclusion constraint ile (migration raw SQL).</summary>
public class ApartmentOwnerConfiguration : IEntityTypeConfiguration<ApartmentOwner>
{
    public void Configure(EntityTypeBuilder<ApartmentOwner> builder)
    {
        builder.ToTable("ApartmentOwners", t =>
        {
            // ValidFrom her zaman ValidTo'dan önce olmalı (ValidTo NULL = sonsuz geçerli).
            t.HasCheckConstraint(
                "CK_ApartmentOwners_Period",
                "\"ValidFrom\" < COALESCE(\"ValidTo\", 'infinity'::timestamptz)");
        });

        builder.HasKey(o => o.Id);

        builder.Property(o => o.OwnerName)
            .IsRequired()
            .HasMaxLength(DomainConstants.ApartmentOwner.OwnerNameMaxLength);

        // Faz 7.1: Sakin iletişim bilgileri (Ev Sahibi + Kiracı).
        builder.Property(o => o.ResidentType)
            .IsRequired()
            .HasDefaultValue(Westalife.Domain.Enums.ResidentType.EvSahibi);

        builder.Property(o => o.PhoneNumber)
            .HasMaxLength(DomainConstants.ApartmentOwner.PhoneMaxLength);

        builder.Property(o => o.Email)
            .HasMaxLength(DomainConstants.ApartmentOwner.EmailMaxLength);

        builder.Property(o => o.IdentityNumber)
            .HasMaxLength(DomainConstants.ApartmentOwner.IdentityNumberLength);

        builder.Property(o => o.Notes)
            .HasMaxLength(DomainConstants.ApartmentOwner.NotesMaxLength);

        builder.HasIndex(o => new { o.ApartmentId, o.ValidFrom })
            .HasDatabaseName("IX_ApartmentOwners_ApartmentId_ValidFrom");

        builder.HasIndex(o => o.ApartmentId)
            .HasDatabaseName("IX_ApartmentOwners_ApartmentId");

        // Not: tstzrange tabanlı exclusion constraint (aralık çakışması yasağı) ve
        // "tek aktif kayıt" partial unique index'i migration içinde raw SQL ile eklenir
        // (EF Core native desteklemez) — ApartmentDimensions ile aynı desen.
    }
}