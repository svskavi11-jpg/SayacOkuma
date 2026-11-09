using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Enums;

namespace Westalife.Infrastructure.Notifications;

/// <summary>
/// Şifre yoksa veya test modu açıksa gerçek sağlayıcı çağrılmaz.
/// </summary>
public static class NotificationSimulationPolicy
{
    public static bool ShouldSimulate(NotificationChannel channel, NotificationDispatch request)
    {
        if (request.TestMode)
        {
            return true;
        }

        var credentials = request.Credentials;
        return channel switch
        {
            NotificationChannel.Email =>
                string.IsNullOrWhiteSpace(credentials.SmtpHost) || string.IsNullOrWhiteSpace(credentials.SmtpPassword),
            NotificationChannel.Sms => string.IsNullOrWhiteSpace(credentials.SmsPassword),
            NotificationChannel.WhatsApp =>
                string.IsNullOrWhiteSpace(credentials.WhatsAppToken)
                || string.IsNullOrWhiteSpace(credentials.WhatsAppPhoneNumberId),
            _ => true,
        };
    }
}
