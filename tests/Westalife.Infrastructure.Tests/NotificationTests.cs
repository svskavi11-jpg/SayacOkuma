using Microsoft.Extensions.Logging.Abstractions;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Notifications;
using Westalife.Domain.Entities;
using Westalife.Domain.Enums;
using Westalife.Domain.Services.Ledger;
using Westalife.Infrastructure.Notifications;

namespace Westalife.Infrastructure.Tests;

public class NotificationTests
{
    private static NotificationChannelCredentials EmptyCredentials() =>
        new(null, 587, null, null, null, null, null, null, null, null);

    private static NotificationChannelCredentials FullCredentials() =>
        new("smtp.ornek.com", 587, "posta@site.com", "smtp-sifre", "Netgsm", "sms-user", "sms-sifre", "WESTALIFE", "wa-token", "12345");

    [Fact]
    public void Placeholder_UsesLedgerTotalBalance_NotOnlyPrincipal()
    {
        var apartment = new Apartment(Guid.NewGuid(), "12", "Daire 12");
        apartment.SetOpeningBalance(1000m, 200m, new DateOnly(2026, 1, 1));

        var ledger = LedgerService.Calculate(
            apartment, [], [], annualInterestRate: 0m, asOfDate: new DateOnly(2026, 3, 1));

        Assert.Equal(1200m, ledger.TotalBalance);

        var text = NotificationPlaceholderRenderer.Apply(
            "Sayın [AdSoyad], daire [DaireNo], dönem [Donem], bakiye [Tutar].",
            new NotificationPlaceholderValues("Ayşe Yılmaz", apartment.DoorNumber, ledger.TotalBalance, "Ocak 2026"));

        Assert.Equal("Sayın Ayşe Yılmaz, daire 12, dönem Ocak 2026, bakiye 1.200,00 TL.", text);
    }

    [Fact]
    public void Placeholder_SplitsAidatAndHeating_AndKeepsOpeningOutOfThoseDebts()
    {
        var apartment = new Apartment(Guid.NewGuid(), "12");
        apartment.SetOpeningBalance(1000m, 200m, new DateOnly(2026, 1, 1));
        var asOf = new DateOnly(2026, 11, 9);
        var ledger = LedgerService.Calculate(apartment, [], [], 0m, asOf);

        var money = NotificationPlaceholderRenderer.ReadAmounts(ledger, [], [], asOf);

        Assert.Equal(1200m, ledger.TotalBalance);
        Assert.Equal(0m, money.AidatDebt);
        Assert.Equal(0m, money.HeatingDebt);

        var text = NotificationPlaceholderRenderer.Apply(
            "Aidat [AidatBorc], ısınma [IsinmaBorc], bu ay [BuDonemAidat] / [BuDonemIsinma].",
            new NotificationPlaceholderValues("Ali", "12", ledger.TotalBalance, "Kasım 2026", 3000m, 2000m, 1500m, 800m));

        Assert.Equal("Aidat 3.000,00 TL, ısınma 2.000,00 TL, bu ay 1.500,00 TL / 800,00 TL.", text);
    }

    [Fact]
    public void CurrentPeriodAccrual_IncludesOnlyThisMonth_AndIgnoresPayments()
    {
        var november = new DateOnly(2026, 11, 9);
        var items = new[]
        {
            new NotificationAccrual(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30), 1500m),
            new NotificationAccrual(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), 900m),
            new NotificationAccrual(new DateOnly(2026, 11, 15), new DateOnly(2026, 12, 15), 400m),
        };

        Assert.Equal(1900m, NotificationPlaceholderRenderer.SumAccrual(items, november));
        Assert.Equal(10, NotificationPlaceholderRenderer.Catalog.Count);
        Assert.Contains(NotificationPlaceholderRenderer.Catalog, item => item.Token == "[BuDonemAidat]");
        Assert.Contains(NotificationPlaceholderRenderer.Catalog, item => item.Token == "[MevcutAy]");
        Assert.Contains(NotificationPlaceholderRenderer.Catalog, item => item.Token == "[PdfLink]");
    }

    [Fact]
    public void PurposeCatalog_FiltersAidatAndHeatingTemplates_AndProvidesDefaultBodies()
    {
        Assert.Contains("[AidatBorc]", NotificationPurposeCatalog.DefaultBody(NotificationPurpose.Aidat));
        Assert.Contains("[IsinmaBorc]", NotificationPurposeCatalog.DefaultBody(NotificationPurpose.Heating));
        Assert.Contains("[Tutar]", NotificationPurposeCatalog.DefaultBody(NotificationPurpose.AllDebtors));
        Assert.True(NotificationPurposeCatalog.MatchesTemplate(NotificationPurpose.Aidat, "Aidat hatırlatma", "borç [AidatBorc]"));
        Assert.True(NotificationPurposeCatalog.MatchesTemplate(NotificationPurpose.Heating, "Isınma", "[IsinmaBorc]"));
        Assert.True(NotificationPurposeCatalog.MatchesTemplate(NotificationPurpose.AllDebtors, "Borç", "[Tutar]"));
        Assert.False(NotificationPurposeCatalog.MatchesTemplate(NotificationPurpose.Aidat, "Genel", "Merhaba [AdSoyad]"));
    }

    [Fact]
    public void Apply_ReplacesMevcutAy_AndLeavesEmptyPdfLink()
    {
        var text = NotificationPlaceholderRenderer.Apply(
            "Ay: [MevcutAy]. Link: [PdfLink]. Dönem: [Donem].",
            new NotificationPlaceholderValues(
                "Ayşe", "12", 100m, "01.06.2026 – 31.05.2027",
                CurrentMonthLabel: "Kasım 2026"));

        Assert.Equal("Ay: Kasım 2026. Link: . Dönem: 01.06.2026 – 31.05.2027.", text);
        Assert.Equal("Kasım 2026", NotificationPlaceholderRenderer.FormatCurrentMonth(new DateOnly(2026, 11, 9)));
    }

    [Fact]
    public void NoticePdfAccessToken_IsUnpredictable_AndExpires()
    {
        var now = new DateTimeOffset(2026, 11, 9, 10, 0, 0, TimeSpan.Zero);
        var token = NoticePdfAccessToken.Create(Guid.NewGuid(), Guid.NewGuid(), now);
        Assert.NotEqual(Guid.Empty, token.Token);
        Assert.True(token.IsValidAt(now.AddDays(29)));
        Assert.False(token.IsValidAt(now.AddDays(31)));

        var withLink = NotificationPlaceholderRenderer.Apply(
            "Fatura: [PdfLink]",
            new NotificationPlaceholderValues(
                "Ayşe", "12", 0m, "dönem",
                CurrentMonthLabel: "Kasım 2026",
                PdfLink: "https://ornek/api/v1/reports/notice.pdf?token=abc"));
        Assert.Equal("Fatura: https://ornek/api/v1/reports/notice.pdf?token=abc", withLink);
    }

    [Fact]
    public void Placeholder_FormatsFractionalBalance_WithTurkishSeparators()
    {
        var apartment = new Apartment(Guid.NewGuid(), "7");
        apartment.SetOpeningBalance(1500.25m, 0m, new DateOnly(2026, 11, 1));
        var ledger = LedgerService.Calculate(
            apartment, [], [], 0m, new DateOnly(2026, 11, 1));

        var text = NotificationPlaceholderRenderer.Apply(
            "[Tutar]",
            new NotificationPlaceholderValues("Ali", "7", ledger.TotalBalance, "Kasım 2026"));

        Assert.Equal(1500.25m, ledger.TotalBalance);
        Assert.Equal("1.500,25 TL", text);
    }

    [Fact]
    public void ResolveResident_PrefersActiveTenant()
    {
        var apartmentId = Guid.NewGuid();
        var owner = new ApartmentOwner(apartmentId, "Malik", DateTimeOffset.UtcNow, ResidentType.EvSahibi, email: "malik@site.com");
        var tenant = new ApartmentOwner(apartmentId, "Kiracı", DateTimeOffset.UtcNow, ResidentType.Kiraci, phoneNumber: "5551112233");

        var resident = NotificationContextLoader.ResolveResident([owner, tenant]);

        Assert.Equal("Kiracı", resident?.OwnerName);
        Assert.Equal("5551112233", resident?.PhoneNumber);
    }

    [Theory]
    [InlineData(NotificationChannel.Email)]
    [InlineData(NotificationChannel.Sms)]
    [InlineData(NotificationChannel.WhatsApp)]
    public void SimulationPolicy_TestMode_SkipsRealProvider(NotificationChannel channel)
    {
        var request = new NotificationDispatch("alici", "Konu", "Metin", TestMode: true, FullCredentials());
        Assert.True(NotificationSimulationPolicy.ShouldSimulate(channel, request));
    }

    [Fact]
    public void SimulationPolicy_MissingSecrets_Simulates_FilledSecrets_Send()
    {
        var missing = new NotificationDispatch("alici", null, "Metin", false, EmptyCredentials());
        Assert.True(NotificationSimulationPolicy.ShouldSimulate(NotificationChannel.Email, missing));
        Assert.True(NotificationSimulationPolicy.ShouldSimulate(NotificationChannel.Sms, missing));
        Assert.True(NotificationSimulationPolicy.ShouldSimulate(NotificationChannel.WhatsApp, missing));

        var ready = new NotificationDispatch("alici", "Konu", "Metin", false, FullCredentials());
        Assert.False(NotificationSimulationPolicy.ShouldSimulate(NotificationChannel.Email, ready));
        Assert.False(NotificationSimulationPolicy.ShouldSimulate(NotificationChannel.Sms, ready));
        Assert.False(NotificationSimulationPolicy.ShouldSimulate(NotificationChannel.WhatsApp, ready));
    }

    [Fact]
    public async Task EmailService_WithoutPassword_ReturnsSimulated_AndDoesNotThrow()
    {
        var service = new EmailNotificationService(NullLogger<EmailNotificationService>.Instance);
        var result = await service.SendAsync(
            new NotificationDispatch("sakin@site.com", "Konu", "Merhaba", false, EmptyCredentials()),
            CancellationToken.None);

        Assert.Equal(NotificationDeliveryStatus.Simulated, result.Status);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task EmailAndWhatsApp_WithPdfAttachment_InTestMode_SimulateSuccessfully()
    {
        var pdf = new NotificationPdfAttachment([1, 2, 3, 4], "BireyselGiderBildirimi-12.pdf");
        var dispatch = new NotificationDispatch(
            "sakin@site.com", "Fatura", "Ekli bildirim", true, FullCredentials(), pdf);

        var email = await new EmailNotificationService(NullLogger<EmailNotificationService>.Instance)
            .SendAsync(dispatch, CancellationToken.None);
        var whatsApp = await new WhatsAppNotificationService(
                new UnusedHttpClientFactory(), NullLogger<WhatsAppNotificationService>.Instance)
            .SendAsync(dispatch with { Recipient = "05551112233" }, CancellationToken.None);

        Assert.Equal(NotificationDeliveryStatus.Simulated, email.Status);
        Assert.Equal(NotificationDeliveryStatus.Simulated, whatsApp.Status);
    }

    [Fact]
    public async Task SmsAndWhatsApp_TestMode_ReturnSimulated_EvenWhenSecretsExist()
    {
        var sms = new SmsNotificationService(new UnusedHttpClientFactory(), NullLogger<SmsNotificationService>.Instance);
        var whatsApp = new WhatsAppNotificationService(new UnusedHttpClientFactory(), NullLogger<WhatsAppNotificationService>.Instance);
        var dispatch = new NotificationDispatch("05551112233", null, "Bakiye", true, FullCredentials());

        var smsResult = await sms.SendAsync(dispatch, CancellationToken.None);
        var whatsAppResult = await whatsApp.SendAsync(dispatch, CancellationToken.None);

        Assert.Equal(NotificationDeliveryStatus.Simulated, smsResult.Status);
        Assert.Equal(NotificationDeliveryStatus.Simulated, whatsAppResult.Status);
    }

    private sealed class UnusedHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            throw new InvalidOperationException("Simülasyon modunda HTTP istemcisi açılmamalı.");
    }
}
