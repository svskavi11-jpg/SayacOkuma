using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Enums;

namespace Westalife.Infrastructure.Notifications;

public sealed class EmailNotificationService : INotificationService
{
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(ILogger<EmailNotificationService> logger) => _logger = logger;

    public NotificationChannel Channel => NotificationChannel.Email;

    public async Task<NotificationSendResult> SendAsync(NotificationDispatch request, CancellationToken cancellationToken)
    {
        if (NotificationSimulationPolicy.ShouldSimulate(Channel, request))
        {
            _logger.LogInformation(
                "{Simulated}. Kanal: {Channel}, Alıcı: {Recipient}, PdfEk: {HasPdf} ({PdfBytes} bayt)",
                DomainConstants.Notification.SimulatedLog,
                Channel,
                request.Recipient,
                request.PdfAttachment is not null,
                request.PdfAttachment?.Content.Length ?? 0);
            return new NotificationSendResult(NotificationDeliveryStatus.Simulated, null);
        }

        try
        {
            var credentials = request.Credentials;
            if (string.IsNullOrWhiteSpace(credentials.SmtpUser)
                || string.IsNullOrWhiteSpace(credentials.SmtpHost)
                || string.IsNullOrWhiteSpace(credentials.SmtpPassword))
            {
                return new NotificationSendResult(NotificationDeliveryStatus.Failed, "SMTP kullanıcısı tanımlı değil.");
            }

            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(credentials.SmtpUser));
            message.To.Add(MailboxAddress.Parse(request.Recipient));
            message.Subject = string.IsNullOrWhiteSpace(request.Subject)
                ? DomainConstants.Notification.DefaultEmailSubject
                : request.Subject;

            var body = new TextPart("plain") { Text = request.Body };
            if (request.PdfAttachment is { } pdf)
            {
                var multipart = new Multipart("mixed");
                multipart.Add(body);
                multipart.Add(new MimePart(pdf.ContentType)
                {
                    Content = new MimeContent(new MemoryStream(pdf.Content)),
                    ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                    ContentTransferEncoding = ContentEncoding.Base64,
                    FileName = pdf.FileName,
                });
                message.Body = multipart;
            }
            else
            {
                message.Body = body;
            }

            using var client = new SmtpClient();
            var socket = credentials.SmtpPort == 465
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTlsWhenAvailable;
            await client.ConnectAsync(credentials.SmtpHost, credentials.SmtpPort, socket, cancellationToken);
            await client.AuthenticateAsync(credentials.SmtpUser, credentials.SmtpPassword, cancellationToken);
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            return new NotificationSendResult(NotificationDeliveryStatus.Sent, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "E-posta gönderilemedi. Alıcı: {Recipient}", request.Recipient);
            return new NotificationSendResult(NotificationDeliveryStatus.Failed, Clip(ex.Message));
        }
    }

    private static string Clip(string value) =>
        value.Length <= DomainConstants.Notification.ErrorMaxLength
            ? value
            : value[..DomainConstants.Notification.ErrorMaxLength];
}
