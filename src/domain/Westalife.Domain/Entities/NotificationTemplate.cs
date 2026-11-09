using Westalife.Domain.Common;
using Westalife.Domain.Constants;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;

namespace Westalife.Domain.Entities;

/// <summary>Kanala bağlı bildirim şablonu. Gövdedeki yer tutucular gönderimde doldurulur.</summary>
public class NotificationTemplate : BaseEntity
{
    public string Name { get; private set; } = string.Empty;

    public NotificationChannel Channel { get; private set; }

    /// <summary>Yalnızca e-posta kanalında kullanılır.</summary>
    public string? Subject { get; private set; }

    public string Body { get; private set; } = string.Empty;

    private NotificationTemplate() { }

    public NotificationTemplate(string name, NotificationChannel channel, string? subject, string body)
    {
        Update(name, channel, subject, body);
        LastModifiedOn = null;
    }

    public void Update(string name, NotificationChannel channel, string? subject, string body)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > DomainConstants.Notification.NameMaxLength)
        {
            throw new DomainException(DomainConstants.Notification.NameRequired);
        }

        if (!Enum.IsDefined(channel))
        {
            throw new DomainException(DomainConstants.Notification.ChannelInvalid);
        }

        if (string.IsNullOrWhiteSpace(body) || body.Trim().Length > DomainConstants.Notification.BodyMaxLength)
        {
            throw new DomainException(DomainConstants.Notification.BodyRequired);
        }

        var normalizedSubject = string.IsNullOrWhiteSpace(subject) ? null : subject.Trim();
        if (normalizedSubject is { Length: > DomainConstants.Notification.SubjectMaxLength })
        {
            throw new DomainException(DomainConstants.Notification.SubjectTooLong);
        }

        Name = name.Trim();
        Channel = channel;
        Subject = normalizedSubject;
        Body = body.Trim();
        LastModifiedOn = DateTimeOffset.UtcNow;
    }
}
