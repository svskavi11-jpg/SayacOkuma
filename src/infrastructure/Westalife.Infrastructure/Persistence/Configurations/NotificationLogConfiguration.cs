using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Configurations;

public class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> builder)
    {
        builder.ToTable("NotificationLogs");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Channel).HasConversion<int>();
        builder.Property(l => l.RecipientAddress).IsRequired().HasMaxLength(DomainConstants.Notification.AddressMaxLength);
        builder.Property(l => l.Message).IsRequired().HasMaxLength(DomainConstants.Notification.BodyMaxLength);
        builder.Property(l => l.Status).HasConversion<int>();
        builder.Property(l => l.ErrorMessage).HasMaxLength(DomainConstants.Notification.ErrorMaxLength);
        builder.HasIndex(l => l.SentAt).HasDatabaseName("IX_NotificationLogs_SentAt");

        builder.HasOne(l => l.Apartment)
            .WithMany()
            .HasForeignKey(l => l.ApartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
