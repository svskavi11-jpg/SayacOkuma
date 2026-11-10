using Westalife.Domain.Common;
using Westalife.Domain.Constants;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;

namespace Westalife.Domain.Entities;

/// <summary>Tek bir daireye yapılan bildirim denemesinin değiştirilmez kaydı.</summary>
public class NotificationLog : BaseEntity, ITenantEntity
{
    /// <summary>Kaydın ait olduğu site.</summary>
    public Guid TenantId { get; private set; }

    public void AssignTenant(Guid tenantId) => TenantId = tenantId;

    public Guid ApartmentId { get; private set; }

    public Apartment Apartment { get; private set; } = null!;

    public NotificationChannel Channel { get; private set; }

    public string RecipientAddress { get; private set; } = string.Empty;

    public string Message { get; private set; } = string.Empty;

    public DateTimeOffset SentAt { get; private set; }

    public NotificationDeliveryStatus Status { get; private set; }

    public string? ErrorMessage { get; private set; }

    private NotificationLog() { }

    public static NotificationLog Record(
        Guid apartmentId,
        NotificationChannel channel,
        string? recipientAddress,
        string message,
        NotificationDeliveryStatus status,
        string? errorMessage,
        DateTimeOffset sentAt)
    {
        if (apartmentId == Guid.Empty)
        {
            throw new DomainException(DomainConstants.Notification.ApartmentNotFound);
        }

        if (!Enum.IsDefined(channel))
        {
            throw new DomainException(DomainConstants.Notification.ChannelInvalid);
        }

        if (!Enum.IsDefined(status))
        {
            throw new DomainException(DomainConstants.Notification.StatusInvalid);
        }

        return new NotificationLog
        {
            ApartmentId = apartmentId,
            Channel = channel,
            RecipientAddress = Trim(recipientAddress, DomainConstants.Notification.AddressMaxLength) ?? string.Empty,
            Message = Trim(message, DomainConstants.Notification.BodyMaxLength) ?? string.Empty,
            SentAt = sentAt,
            Status = status,
            ErrorMessage = Trim(errorMessage, DomainConstants.Notification.ErrorMaxLength),
        };
    }

    private static string? Trim(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
