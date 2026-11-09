using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Notifications;

public sealed record NotificationSettingsResponse(
    bool TestMode,
    string? PublicBaseUrl,
    string? SmtpHost,
    int SmtpPort,
    string? SmtpUser,
    bool SmtpPasswordSet,
    string SmsProvider,
    string? SmsUser,
    bool SmsPasswordSet,
    string? SmsHeader,
    bool WhatsAppTokenSet,
    string? WhatsAppPhoneNumberId);

public sealed record GetNotificationSettingsQuery : IRequest<NotificationSettingsResponse>;

public sealed class GetNotificationSettingsQueryHandler
    : IRequestHandler<GetNotificationSettingsQuery, NotificationSettingsResponse>
{
    private readonly IApplicationDbContext _db;

    public GetNotificationSettingsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<NotificationSettingsResponse> Handle(
        GetNotificationSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(DomainConstants.Notification.SettingsNotFound);
        return NotificationSettingsMapper.ToResponse(settings);
    }
}

public sealed record UpdateNotificationSettingsCommand(
    bool TestMode,
    string? PublicBaseUrl,
    string? SmtpHost,
    int SmtpPort,
    string? SmtpUser,
    string? SmtpPassword,
    string? SmsProvider,
    string? SmsUser,
    string? SmsPassword,
    string? SmsHeader,
    string? WhatsAppToken,
    string? WhatsAppPhoneNumberId) : IRequest<NotificationSettingsResponse>;

public sealed class UpdateNotificationSettingsCommandValidator : AbstractValidator<UpdateNotificationSettingsCommand>
{
    public UpdateNotificationSettingsCommandValidator()
    {
        RuleFor(c => c.SmtpPort)
            .InclusiveBetween(DomainConstants.AppSettings.TcpPortMin, DomainConstants.AppSettings.TcpPortMax)
            .WithMessage(DomainConstants.Notification.SmtpPortInvalid);
        RuleFor(c => c.PublicBaseUrl).MaximumLength(DomainConstants.Notification.PublicBaseUrlMaxLength);
        RuleFor(c => c.SmtpHost).MaximumLength(DomainConstants.Notification.HostMaxLength);
        RuleFor(c => c.SmtpUser).MaximumLength(DomainConstants.Notification.HostMaxLength);
        RuleFor(c => c.SmsProvider).MaximumLength(DomainConstants.Notification.ProviderMaxLength);
        RuleFor(c => c.SmsHeader).MaximumLength(DomainConstants.Notification.HeaderMaxLength);
        RuleFor(c => c.WhatsAppPhoneNumberId).MaximumLength(DomainConstants.Notification.PhoneNumberIdMaxLength);
    }
}

public sealed class UpdateNotificationSettingsCommandHandler
    : IRequestHandler<UpdateNotificationSettingsCommand, NotificationSettingsResponse>
{
    private readonly IApplicationDbContext _db;

    public UpdateNotificationSettingsCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<NotificationSettingsResponse> Handle(
        UpdateNotificationSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(DomainConstants.Notification.SettingsNotFound);

        settings.UpdateNotificationSettings(
            request.TestMode,
            request.PublicBaseUrl,
            request.SmtpHost,
            request.SmtpPort,
            request.SmtpUser,
            request.SmtpPassword,
            request.SmsProvider,
            request.SmsUser,
            request.SmsPassword,
            request.SmsHeader,
            request.WhatsAppToken,
            request.WhatsAppPhoneNumberId);

        await _db.SaveChangesAsync(cancellationToken);
        return NotificationSettingsMapper.ToResponse(settings);
    }
}

internal static class NotificationSettingsMapper
{
    public static NotificationSettingsResponse ToResponse(AppSettings settings) => new(
        settings.NotificationTestMode,
        settings.PublicBaseUrl,
        settings.SmtpHost,
        settings.SmtpPort == 0 ? DomainConstants.Notification.SmtpPortDefault : settings.SmtpPort,
        settings.SmtpUser,
        !string.IsNullOrEmpty(settings.SmtpPassword),
        string.IsNullOrWhiteSpace(settings.SmsProvider)
            ? DomainConstants.Notification.SmsProviderNetgsm
            : settings.SmsProvider,
        settings.SmsUser,
        !string.IsNullOrEmpty(settings.SmsPassword),
        settings.SmsHeader,
        !string.IsNullOrEmpty(settings.WhatsAppToken),
        settings.WhatsAppPhoneNumberId);

    public static NotificationChannelCredentials ToCredentials(AppSettings settings) => new(
        settings.SmtpHost,
        settings.SmtpPort == 0 ? DomainConstants.Notification.SmtpPortDefault : settings.SmtpPort,
        settings.SmtpUser,
        settings.SmtpPassword,
        settings.SmsProvider,
        settings.SmsUser,
        settings.SmsPassword,
        settings.SmsHeader,
        settings.WhatsAppToken,
        settings.WhatsAppPhoneNumberId);
}
