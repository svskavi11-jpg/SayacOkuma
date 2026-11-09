namespace Westalife.Domain.Enums;

/// <summary>
/// Bildirim gönderim amacı. Şablon ve daire seçimini yönlendirir; gönderim motoru aynıdır.
/// </summary>
public enum NotificationPurpose
{
    General = 1,
    Aidat = 2,
    Heating = 3,
    AllDebtors = 4,
}
