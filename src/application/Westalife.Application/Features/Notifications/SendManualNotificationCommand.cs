using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Notifications;

public sealed record SendManualNotificationCommand(
    IReadOnlyList<Guid> ApartmentIds,
    NotificationChannel Channel,
    Guid? TemplateId,
    string? Subject,
    string? Body,
    NotificationPurpose Purpose = NotificationPurpose.General,
    Guid? GasInvoiceId = null,
    bool IncludePdf = false) : IRequest<SendManualNotificationResult>;

public sealed record SendManualNotificationResult(
    int Sent,
    int Failed,
    int Simulated,
    NotificationPurpose Purpose,
    IReadOnlyList<NotificationLogItem> Logs);

public sealed class SendManualNotificationCommandValidator : AbstractValidator<SendManualNotificationCommand>
{
    public SendManualNotificationCommandValidator()
    {
        RuleFor(c => c.ApartmentIds)
            .NotEmpty()
            .WithMessage(DomainConstants.Notification.ApartmentsRequired);
        RuleFor(c => c.Channel).IsInEnum().WithMessage(DomainConstants.Notification.ChannelInvalid);
        RuleFor(c => c.Purpose).IsInEnum().WithMessage(DomainConstants.Notification.PurposeInvalid);
        RuleFor(c => c)
            .Must(c => !c.IncludePdf || c.GasInvoiceId is { } id && id != Guid.Empty)
            .WithMessage(DomainConstants.Notification.PdfInvoiceRequired);
    }
}

public sealed class SendManualNotificationCommandHandler
    : IRequestHandler<SendManualNotificationCommand, SendManualNotificationResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IEnumerable<INotificationService> _services;
    private readonly INoticePdfGenerator _pdfGenerator;

    public SendManualNotificationCommandHandler(
        IApplicationDbContext db,
        IEnumerable<INotificationService> services,
        INoticePdfGenerator pdfGenerator)
    {
        _db = db;
        _services = services;
        _pdfGenerator = pdfGenerator;
    }

    public async Task<SendManualNotificationResult> Handle(
        SendManualNotificationCommand request, CancellationToken cancellationToken)
    {
        var (subjectTemplate, bodyTemplate) = await ResolveTemplateAsync(request, cancellationToken);
        var settings = await _db.AppSettings.FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(DomainConstants.Notification.SettingsNotFound);
        var credentials = NotificationSettingsMapper.ToCredentials(settings);
        var service = _services.FirstOrDefault(s => s.Channel == request.Channel)
            ?? throw new DomainException(DomainConstants.Notification.ChannelInvalid);

        var wantPdf = request.IncludePdf
            || (request.GasInvoiceId is { } invoiceId && invoiceId != Guid.Empty
                && request.Purpose == NotificationPurpose.Heating);

        if (wantPdf && request.GasInvoiceId is { } requiredInvoice)
        {
            var exists = await _db.GasInvoices.AsNoTracking()
                .AnyAsync(i => i.Id == requiredInvoice, cancellationToken);
            if (!exists)
            {
                throw new NotFoundException(DomainConstants.Notification.PdfInvoiceRequired);
            }
        }

        var contexts = await NotificationContextLoader.LoadAsync(_db, request.ApartmentIds, cancellationToken);
        var sentAt = DateTimeOffset.UtcNow;
        var logs = new List<NotificationLog>();
        var baseUrl = string.IsNullOrWhiteSpace(settings.PublicBaseUrl)
            ? null
            : settings.PublicBaseUrl.TrimEnd('/');

        foreach (var context in contexts)
        {
            if (context.LoadError is not null)
            {
                logs.Add(NotificationLog.Record(
                    context.ApartmentId, request.Channel, null, string.Empty,
                    NotificationDeliveryStatus.Failed, context.LoadError, sentAt));
                continue;
            }

            string? pdfLink = null;
            NotificationPdfAttachment? attachment = null;
            if (wantPdf && request.GasInvoiceId is { } gasInvoiceId)
            {
                try
                {
                    var token = NoticePdfAccessToken.Create(gasInvoiceId, context.ApartmentId, sentAt);
                    _db.NoticePdfAccessTokens.Add(token);
                    if (!string.IsNullOrWhiteSpace(baseUrl))
                    {
                        pdfLink = $"{baseUrl}/api/v1/reports/notice.pdf?token={token.Token:D}";
                    }

                    if (request.Channel is NotificationChannel.Email or NotificationChannel.WhatsApp)
                    {
                        var bytes = await _pdfGenerator.GenerateApartmentNoticeAsync(
                            gasInvoiceId, context.ApartmentId, cancellationToken);
                        attachment = new NotificationPdfAttachment(
                            bytes,
                            $"BireyselGiderBildirimi-{context.DoorNumber}.pdf");
                    }
                }
                catch (Exception)
                {
                    // PDF hatası metin gönderimini engellemez; link/ek olmadan devam.
                    pdfLink = null;
                    attachment = null;
                }
            }

            var enriched = context.WithPdfLink(pdfLink);
            var address = request.Channel == NotificationChannel.Email ? enriched.Email : enriched.Phone;
            var values = enriched.ToValues();
            var message = NotificationPlaceholderRenderer.Apply(bodyTemplate, values);
            if (string.IsNullOrWhiteSpace(address))
            {
                logs.Add(NotificationLog.Record(
                    enriched.ApartmentId, request.Channel, null, message,
                    NotificationDeliveryStatus.Failed, DomainConstants.Notification.RecipientMissing, sentAt));
                continue;
            }

            NotificationSendResult result;
            try
            {
                var subject = request.Channel == NotificationChannel.Email
                    ? NotificationPlaceholderRenderer.Apply(subjectTemplate, values)
                    : null;
                if (request.Channel == NotificationChannel.Email && string.IsNullOrWhiteSpace(subject))
                {
                    subject = DomainConstants.Notification.DefaultEmailSubject;
                }

                result = await service.SendAsync(
                    new NotificationDispatch(
                        address, subject, message, settings.NotificationTestMode, credentials, attachment),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                result = new NotificationSendResult(NotificationDeliveryStatus.Failed, ex.Message);
            }

            logs.Add(NotificationLog.Record(
                enriched.ApartmentId, request.Channel, address, message, result.Status, result.ErrorMessage, sentAt));
        }

        foreach (var log in logs)
        {
            _db.NotificationLogs.Add(log);
        }

        await _db.SaveChangesAsync(cancellationToken);

        var items = logs.Select(log => NotificationLogItem.From(log, contexts)).ToList();
        return new SendManualNotificationResult(
            logs.Count(l => l.Status == NotificationDeliveryStatus.Sent),
            logs.Count(l => l.Status == NotificationDeliveryStatus.Failed),
            logs.Count(l => l.Status == NotificationDeliveryStatus.Simulated),
            request.Purpose,
            items);
    }

    private async Task<(string? Subject, string Body)> ResolveTemplateAsync(
        SendManualNotificationCommand request, CancellationToken cancellationToken)
    {
        if (request.TemplateId is { } templateId && templateId != Guid.Empty)
        {
            var template = await _db.NotificationTemplates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken)
                ?? throw new NotFoundException(DomainConstants.Notification.TemplateNotFound);
            if (template.Channel != request.Channel)
            {
                throw new DomainException(DomainConstants.Notification.TemplateChannelMismatch);
            }

            return (template.Subject, template.Body);
        }

        if (!string.IsNullOrWhiteSpace(request.Body))
        {
            return (request.Subject, request.Body.Trim());
        }

        return (request.Subject, NotificationPurposeCatalog.DefaultBody(request.Purpose));
    }
}
