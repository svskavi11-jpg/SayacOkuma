using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

/// <summary>Tek satırlık sistem ayarları (singleton).</summary>
public class AppSettingsConfiguration : IEntityTypeConfiguration<AppSettings>
{
    public static readonly Guid SingletonId = new("00000000-0000-0000-0000-00000000AA01");

    public void Configure(EntityTypeBuilder<AppSettings> builder)
    {
        builder.ToTable("AppSettings");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.CompanyName)
            .IsRequired()
            .HasMaxLength(DomainConstants.AppSettings.CompanyNameMaxLength);

        builder.Property(s => s.BuildingName)
            .IsRequired()
            .HasMaxLength(DomainConstants.Building.NameMaxLength);

        builder.Property(s => s.FixedCostRatio)
            .HasPrecision(DomainConstants.RatioPrecision, DomainConstants.RatioScale);

        builder.Property(s => s.ConsumptionRatio)
            .HasPrecision(DomainConstants.RatioPrecision, DomainConstants.RatioScale);

        // Bireysel Gider Bildirimi ayarları (logolar byte[] olarak DB'de tutulur).
        builder.Property(s => s.NoticeLeftTitle).HasMaxLength(DomainConstants.AppSettings.NoticeTitleMaxLength);
        builder.Property(s => s.NoticeRightTitle).HasMaxLength(DomainConstants.AppSettings.NoticeTitleMaxLength);
        builder.Property(s => s.LogoLeftContentType).HasMaxLength(50);
        builder.Property(s => s.LogoRightContentType).HasMaxLength(50);
        builder.Property(s => s.LogoLeftHeightMm).HasDefaultValue(DomainConstants.AppSettings.LogoHeightMmDefault);
        builder.Property(s => s.LogoRightHeightMm).HasDefaultValue(DomainConstants.AppSettings.LogoHeightMmDefault);
        builder.Property(s => s.DueDateDays).HasDefaultValue(DomainConstants.AppSettings.DueDateDaysDefault);
        builder.Property(s => s.ReadingServiceFee)
            .HasPrecision(DomainConstants.MoneyPrecision, DomainConstants.MoneyScale)
            .HasDefaultValue(0m);
        builder.Property(s => s.DefaultNoticeNote).HasMaxLength(DomainConstants.AppSettings.NoticeNoteMaxLength);
        builder.Property(s => s.DefaultSubscriberNumber).HasMaxLength(DomainConstants.AppSettings.SubscriberNumberMaxLength);

        // Seed: "Çınar Teknik" (kullanıcı Ayarlar sayfasından değiştirebilir).
        builder.HasData(new
        {
            Id = SingletonId,
            CompanyName = "Çınar Teknik",
            BuildingName = "Westalife Residence",
            FixedCostRatio = DomainConstants.Building.DefaultFixedCostRatio,
            ConsumptionRatio = 0.70m,
            MeterConnectionType = DomainConstants.AppSettings.ConnectionSimulator,
            TcpHost = (string?)null,
            TcpPort = 2000,
            UsbPortName = (string?)null,
            UsbBaudRate = 2400,
            DueDateDays = DomainConstants.AppSettings.DueDateDaysDefault,
            ReadingServiceFee = 0m,
            LogoLeftHeightMm = DomainConstants.AppSettings.LogoHeightMmDefault,
            LogoRightHeightMm = DomainConstants.AppSettings.LogoHeightMmDefault,
            CreatedOn = DomainConstants.SeedData.SeedTimestamp,
            LastModifiedOn = (DateTimeOffset?)null
        });
    }
}
