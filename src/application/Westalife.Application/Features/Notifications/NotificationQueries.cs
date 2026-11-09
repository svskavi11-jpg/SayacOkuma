using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Notifications;

public sealed record NotificationLogItem(
    Guid Id,
    Guid ApartmentId,
    string DoorNumber,
    NotificationChannel Channel,
    string RecipientAddress,
    string Message,
    DateTimeOffset SentAt,
    NotificationDeliveryStatus Status,
    string? ErrorMessage)
{
    public static NotificationLogItem From(NotificationLog log, IEnumerable<ApartmentNotificationContext> contexts)
    {
        var door = contexts.FirstOrDefault(c => c.ApartmentId == log.ApartmentId)?.DoorNumber ?? string.Empty;
        return new NotificationLogItem(
            log.Id, log.ApartmentId, door, log.Channel, log.RecipientAddress,
            log.Message, log.SentAt, log.Status, log.ErrorMessage);
    }
}

public sealed record NotificationTemplateItem(
    Guid Id,
    string Name,
    NotificationChannel Channel,
    string? Subject,
    string Body);

public sealed record GetNotificationPlaceholdersQuery : IRequest<IReadOnlyList<NotificationPlaceholderItem>>;

public sealed class GetNotificationPlaceholdersQueryHandler
    : IRequestHandler<GetNotificationPlaceholdersQuery, IReadOnlyList<NotificationPlaceholderItem>>
{
    public Task<IReadOnlyList<NotificationPlaceholderItem>> Handle(
        GetNotificationPlaceholdersQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(NotificationPlaceholderRenderer.Catalog);
}

public sealed record GetNotificationLogsQuery(int Take = 200) : IRequest<IReadOnlyList<NotificationLogItem>>;

public sealed class GetNotificationLogsQueryHandler
    : IRequestHandler<GetNotificationLogsQuery, IReadOnlyList<NotificationLogItem>>
{
    private readonly IApplicationDbContext _db;

    public GetNotificationLogsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<NotificationLogItem>> Handle(
        GetNotificationLogsQuery request, CancellationToken cancellationToken)
    {
        var take = request.Take is < 1 or > 500 ? 200 : request.Take;
        var logs = await _db.NotificationLogs.AsNoTracking()
            .OrderByDescending(l => l.SentAt)
            .ThenByDescending(l => l.CreatedOn)
            .Take(take)
            .ToListAsync(cancellationToken);

        if (logs.Count == 0)
        {
            return [];
        }

        var apartmentIds = logs.Select(l => l.ApartmentId).Distinct().ToList();
        var doors = await _db.Apartments.AsNoTracking()
            .Where(a => apartmentIds.Contains(a.Id))
            .Select(a => new { a.Id, a.DoorNumber })
            .ToDictionaryAsync(a => a.Id, a => a.DoorNumber, cancellationToken);

        return logs.Select(log => new NotificationLogItem(
            log.Id,
            log.ApartmentId,
            doors.GetValueOrDefault(log.ApartmentId) ?? string.Empty,
            log.Channel,
            log.RecipientAddress,
            log.Message,
            log.SentAt,
            log.Status,
            log.ErrorMessage)).ToList();
    }
}

public sealed record GetNotificationTemplatesQuery : IRequest<IReadOnlyList<NotificationTemplateItem>>;

public sealed class GetNotificationTemplatesQueryHandler
    : IRequestHandler<GetNotificationTemplatesQuery, IReadOnlyList<NotificationTemplateItem>>
{
    private readonly IApplicationDbContext _db;

    public GetNotificationTemplatesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<NotificationTemplateItem>> Handle(
        GetNotificationTemplatesQuery request, CancellationToken cancellationToken)
    {
        return await _db.NotificationTemplates.AsNoTracking()
            .OrderBy(t => t.Channel)
            .ThenBy(t => t.Name)
            .Select(t => new NotificationTemplateItem(t.Id, t.Name, t.Channel, t.Subject, t.Body))
            .ToListAsync(cancellationToken);
    }
}

public sealed record UpsertNotificationTemplateCommand(
    Guid? Id,
    string Name,
    NotificationChannel Channel,
    string? Subject,
    string Body) : IRequest<NotificationTemplateItem>;

public sealed class UpsertNotificationTemplateCommandValidator : AbstractValidator<UpsertNotificationTemplateCommand>
{
    public UpsertNotificationTemplateCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(DomainConstants.Notification.NameMaxLength);
        RuleFor(c => c.Channel).IsInEnum();
        RuleFor(c => c.Body).NotEmpty().MaximumLength(DomainConstants.Notification.BodyMaxLength);
        RuleFor(c => c.Subject).MaximumLength(DomainConstants.Notification.SubjectMaxLength);
    }
}

public sealed class UpsertNotificationTemplateCommandHandler
    : IRequestHandler<UpsertNotificationTemplateCommand, NotificationTemplateItem>
{
    private readonly IApplicationDbContext _db;

    public UpsertNotificationTemplateCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<NotificationTemplateItem> Handle(
        UpsertNotificationTemplateCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var duplicate = await _db.NotificationTemplates.AnyAsync(
            t => t.Channel == request.Channel && t.Name == name && t.Id != request.Id,
            cancellationToken);
        if (duplicate)
        {
            throw new DomainException(DomainConstants.Notification.TemplateNameExists);
        }

        NotificationTemplate template;
        if (request.Id is { } id && id != Guid.Empty)
        {
            template = await _db.NotificationTemplates.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
                ?? throw new NotFoundException(DomainConstants.Notification.TemplateNotFound);
            template.Update(name, request.Channel, request.Subject, request.Body);
        }
        else
        {
            template = new NotificationTemplate(name, request.Channel, request.Subject, request.Body);
            _db.NotificationTemplates.Add(template);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new NotificationTemplateItem(template.Id, template.Name, template.Channel, template.Subject, template.Body);
    }
}

public sealed record PreviewNotificationQuery(
    Guid ApartmentId,
    NotificationChannel Channel,
    Guid? TemplateId,
    string? Subject,
    string? Body,
    NotificationPurpose Purpose = NotificationPurpose.General) : IRequest<NotificationPreviewResponse>;

public sealed record NotificationPreviewResponse(
    string RecipientName,
    string? RecipientAddress,
    string? Subject,
    string Body,
    bool HasRecipient);

public sealed class PreviewNotificationQueryValidator : AbstractValidator<PreviewNotificationQuery>
{
    public PreviewNotificationQueryValidator()
    {
        RuleFor(q => q.ApartmentId).NotEmpty();
        RuleFor(q => q.Channel).IsInEnum();
        RuleFor(q => q.Purpose).IsInEnum();
    }
}

public sealed class PreviewNotificationQueryHandler
    : IRequestHandler<PreviewNotificationQuery, NotificationPreviewResponse>
{
    private readonly IApplicationDbContext _db;

    public PreviewNotificationQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<NotificationPreviewResponse> Handle(
        PreviewNotificationQuery request, CancellationToken cancellationToken)
    {
        string? subjectTemplate;
        string bodyTemplate;
        if (request.TemplateId is { } templateId && templateId != Guid.Empty)
        {
            var template = await _db.NotificationTemplates.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == templateId, cancellationToken)
                ?? throw new NotFoundException(DomainConstants.Notification.TemplateNotFound);
            subjectTemplate = template.Subject;
            bodyTemplate = template.Body;
        }
        else if (!string.IsNullOrWhiteSpace(request.Body))
        {
            subjectTemplate = request.Subject;
            bodyTemplate = request.Body.Trim();
        }
        else
        {
            subjectTemplate = request.Subject;
            bodyTemplate = NotificationPurposeCatalog.DefaultBody(request.Purpose);
        }

        var context = (await NotificationContextLoader.LoadAsync(_db, [request.ApartmentId], cancellationToken))
            .Single();
        if (context.LoadError is not null && string.IsNullOrEmpty(context.DoorNumber))
        {
            throw new NotFoundException(context.LoadError);
        }

        var values = context.ToValues();
        var address = request.Channel == NotificationChannel.Email ? context.Email : context.Phone;
        var subject = request.Channel == NotificationChannel.Email
            ? NotificationPlaceholderRenderer.Apply(subjectTemplate, values)
            : null;

        return new NotificationPreviewResponse(
            values.FullName,
            address,
            subject,
            NotificationPlaceholderRenderer.Apply(bodyTemplate, values),
            !string.IsNullOrWhiteSpace(address));
    }
}
