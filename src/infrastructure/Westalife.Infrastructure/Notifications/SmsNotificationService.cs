using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Enums;

namespace Westalife.Infrastructure.Notifications;

/// <summary>Netgsm REST SMS iskeleti. Kimlik bilgisi yoksa veya test modundaysa simüle eder.</summary>
public sealed class SmsNotificationService : INotificationService
{
    public const string Endpoint = "https://api.netgsm.com.tr/sms/rest/v2/send";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SmsNotificationService> _logger;

    public SmsNotificationService(IHttpClientFactory httpClientFactory, ILogger<SmsNotificationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public NotificationChannel Channel => NotificationChannel.Sms;

    public async Task<NotificationSendResult> SendAsync(NotificationDispatch request, CancellationToken cancellationToken)
    {
        if (NotificationSimulationPolicy.ShouldSimulate(Channel, request))
        {
            _logger.LogInformation(
                "{Simulated}. Kanal: {Channel}, Alıcı: {Recipient}",
                DomainConstants.Notification.SimulatedLog,
                Channel,
                request.Recipient);
            return new NotificationSendResult(NotificationDeliveryStatus.Simulated, null);
        }

        var credentials = request.Credentials;
        if (string.IsNullOrWhiteSpace(credentials.SmsUser) || string.IsNullOrWhiteSpace(credentials.SmsHeader))
        {
            return new NotificationSendResult(NotificationDeliveryStatus.Failed, "SMS kullanıcı adı veya başlık tanımlı değil.");
        }

        var phone = PhoneNumberNormalizer.ToTurkeyGsm(request.Recipient);
        if (phone is null)
        {
            return new NotificationSendResult(NotificationDeliveryStatus.Failed, "Telefon numarası geçersiz.");
        }

        try
        {
            var payload = new
            {
                msgheader = credentials.SmsHeader,
                messages = new[] { new { msg = request.Body, no = phone } },
                encoding = "TR",
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.SmsUser}:{credentials.SmsPassword}"));
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
            httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var client = _httpClientFactory.CreateClient(nameof(SmsNotificationService));
            using var response = await client.SendAsync(httpRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode || !body.Contains("\"code\":\"00\"", StringComparison.OrdinalIgnoreCase)
                && !body.Contains("\"code\": \"00\"", StringComparison.OrdinalIgnoreCase))
            {
                return new NotificationSendResult(
                    NotificationDeliveryStatus.Failed,
                    Clip($"Netgsm yanıtı: {(int)response.StatusCode} {body}"));
            }

            return new NotificationSendResult(NotificationDeliveryStatus.Sent, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMS gönderilemedi. Alıcı: {Recipient}", request.Recipient);
            return new NotificationSendResult(NotificationDeliveryStatus.Failed, Clip(ex.Message));
        }
    }

    private static string Clip(string value) =>
        value.Length <= DomainConstants.Notification.ErrorMaxLength
            ? value
            : value[..DomainConstants.Notification.ErrorMaxLength];
}
