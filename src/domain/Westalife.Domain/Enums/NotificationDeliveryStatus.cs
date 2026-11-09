namespace Westalife.Domain.Enums;

/// <summary>Tek bir gönderim denemesinin sonucu. Simülasyon gerçek API çağırmaz.</summary>
public enum NotificationDeliveryStatus
{
    Sent = 1,
    Failed = 2,
    Simulated = 3,
}
