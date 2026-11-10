using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Enums;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public static readonly Guid EmailSeedId = new("11111111-1111-1111-1111-111111111101");
    public static readonly Guid SmsSeedId = new("11111111-1111-1111-1111-111111111102");
    public static readonly Guid WhatsAppSeedId = new("11111111-1111-1111-1111-111111111103");

    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.ToTable("NotificationTemplates");
        builder.HasKey(t => t.Id);
        TenantOwnedConfiguration.Configure(builder);

        builder.Property(t => t.Name).IsRequired().HasMaxLength(DomainConstants.Notification.NameMaxLength);
        builder.Property(t => t.Channel).HasConversion<int>();
        builder.Property(t => t.Subject).HasMaxLength(DomainConstants.Notification.SubjectMaxLength);
        builder.Property(t => t.Body).IsRequired().HasMaxLength(DomainConstants.Notification.BodyMaxLength);
        builder.HasIndex(t => new { t.Channel, t.Name }).IsUnique().HasDatabaseName("IX_NotificationTemplates_Channel_Name");

        var body = DomainConstants.Notification.DefaultBody;
        builder.HasData(
            new
            {
                Id = EmailSeedId,
                TenantId = DomainConstants.SeedData.DefaultTenantId,
                Name = "Aidat hatırlatma",
                Channel = NotificationChannel.Email,
                Subject = "Aidat ve bakiye hatırlatması",
                Body = body,
                CreatedOn = DomainConstants.SeedData.SeedTimestamp,
                LastModifiedOn = (DateTimeOffset?)null,
            },
            new
            {
                Id = SmsSeedId,
                TenantId = DomainConstants.SeedData.DefaultTenantId,
                Name = "Aidat hatırlatma",
                Channel = NotificationChannel.Sms,
                Subject = (string?)null,
                Body = body,
                CreatedOn = DomainConstants.SeedData.SeedTimestamp,
                LastModifiedOn = (DateTimeOffset?)null,
            },
            new
            {
                Id = WhatsAppSeedId,
                TenantId = DomainConstants.SeedData.DefaultTenantId,
                Name = "Aidat hatırlatma",
                Channel = NotificationChannel.WhatsApp,
                Subject = (string?)null,
                Body = body,
                CreatedOn = DomainConstants.SeedData.SeedTimestamp,
                LastModifiedOn = (DateTimeOffset?)null,
            });
    }
}
