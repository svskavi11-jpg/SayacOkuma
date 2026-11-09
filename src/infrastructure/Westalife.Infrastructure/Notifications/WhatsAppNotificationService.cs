using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Enums;

namespace Westalife.Infrastructure.Notifications;

/// <summary>Meta WhatsApp Cloud API. PDF varsa önce media yükler, sonra document gönderir.</summary>
public sealed class WhatsAppNotificationService : INotificationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WhatsAppNotificationService> _logger;

    public WhatsAppNotificationService(IHttpClientFactory httpClientFactory, ILogger<WhatsAppNotificationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public NotificationChannel Channel => NotificationChannel.WhatsApp;

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

        var phone = PhoneNumberNormalizer.ToTurkeyGsm(request.Recipient);
        if (phone is null)
        {
            return new NotificationSendResult(NotificationDeliveryStatus.Failed, "Telefon numarası geçersiz.");
        }

        var phoneNumberId = request.Credentials.WhatsAppPhoneNumberId!.Trim();
        var client = _httpClientFactory.CreateClient(nameof(WhatsAppNotificationService));

        try
        {
            if (request.PdfAttachment is { } pdf)
            {
                var mediaId = await UploadMediaAsync(
                    client, phoneNumberId, request.Credentials.WhatsAppToken!, pdf, cancellationToken);
                if (mediaId is null)
                {
                    return new NotificationSendResult(
                        NotificationDeliveryStatus.Failed, "WhatsApp PDF yüklemesi başarısız.");
                }

                var documentPayload = new
                {
                    messaging_product = "whatsapp",
                    to = phone,
                    type = "document",
                    document = new
                    {
                        id = mediaId,
                        filename = pdf.FileName,
                        caption = request.Body.Length > 1024 ? request.Body[..1024] : request.Body,
                    },
                };

                return await PostMessageAsync(
                    client, phoneNumberId, request.Credentials.WhatsAppToken!, documentPayload, cancellationToken);
            }

            var textPayload = new
            {
                messaging_product = "whatsapp",
                to = phone,
                type = "text",
                text = new { body = request.Body },
            };

            return await PostMessageAsync(
                client, phoneNumberId, request.Credentials.WhatsAppToken!, textPayload, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WhatsApp gönderilemedi. Alıcı: {Recipient}", request.Recipient);
            return new NotificationSendResult(NotificationDeliveryStatus.Failed, Clip(ex.Message));
        }
    }

    private static async Task<string?> UploadMediaAsync(
        HttpClient client,
        string phoneNumberId,
        string token,
        NotificationPdfAttachment pdf,
        CancellationToken cancellationToken)
    {
        var endpoint = $"https://graph.facebook.com/v21.0/{phoneNumberId}/media";
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent("whatsapp"), "messaging_product");
        form.Add(new StringContent("application/pdf"), "type");
        var fileContent = new ByteArrayContent(pdf.Content);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "file", pdf.FileName);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        httpRequest.Content = form;

        using var response = await client.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return document.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
    }

    private async Task<NotificationSendResult> PostMessageAsync(
        HttpClient client,
        string phoneNumberId,
        string token,
        object payload,
        CancellationToken cancellationToken)
    {
        var endpoint = $"https://graph.facebook.com/v21.0/{phoneNumberId}/messages";
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return new NotificationSendResult(
                NotificationDeliveryStatus.Failed,
                Clip($"WhatsApp yanıtı: {(int)response.StatusCode} {body}"));
        }

        return new NotificationSendResult(NotificationDeliveryStatus.Sent, null);
    }

    private static string Clip(string value) =>
        value.Length <= DomainConstants.Notification.ErrorMaxLength
            ? value
            : value[..DomainConstants.Notification.ErrorMaxLength];
}
