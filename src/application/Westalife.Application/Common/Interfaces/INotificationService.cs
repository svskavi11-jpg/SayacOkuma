using Westalife.Domain.Enums;

namespace Westalife.Application.Common.Interfaces;

/// <summary>Tek kanalın gönderim sözleşmesi. Kimlik bilgisi çağıran katmandan gelir.</summary>
public interface INotificationService
{
    NotificationChannel Channel { get; }

    Task<NotificationSendResult> SendAsync(NotificationDispatch request, CancellationToken cancellationToken);
}

public sealed record NotificationChannelCredentials(
    string? SmtpHost,
    int SmtpPort,
    string? SmtpUser,
    string? SmtpPassword,
    string? SmsProvider,
    string? SmsUser,
    string? SmsPassword,
    string? SmsHeader,
    string? WhatsAppToken,
    string? WhatsAppPhoneNumberId);

public sealed record NotificationPdfAttachment(
    byte[] Content,
    string FileName,
    string ContentType = "application/pdf");

public sealed record NotificationDispatch(
    string Recipient,
    string? Subject,
    string Body,
    bool TestMode,
    NotificationChannelCredentials Credentials,
    NotificationPdfAttachment? PdfAttachment = null);

public sealed record NotificationSendResult(NotificationDeliveryStatus Status, string? ErrorMessage);

/// <summary>Bireysel gider bildirimi PDF üretimi (Infrastructure).</summary>
public interface INoticePdfGenerator
{
    Task<byte[]> GenerateApartmentNoticeAsync(
        Guid gasInvoiceId,
        Guid apartmentId,
        CancellationToken cancellationToken = default);
}
