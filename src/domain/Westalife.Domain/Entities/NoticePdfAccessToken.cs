using Westalife.Domain.Common;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Domain.Entities;

/// <summary>
/// SMS bildirimlerindeki güvenli PDF indirme bağlantısı.
/// Token tahmin edilemez; süre dolunca geçersizdir.
/// </summary>
public class NoticePdfAccessToken : BaseEntity
{
    public Guid Token { get; private set; }

    public Guid GasInvoiceId { get; private set; }

    public Guid ApartmentId { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    private NoticePdfAccessToken() { }

    public static NoticePdfAccessToken Create(Guid gasInvoiceId, Guid apartmentId, DateTimeOffset? now = null)
    {
        if (gasInvoiceId == Guid.Empty)
        {
            throw new DomainException(DomainConstants.Notification.PdfInvoiceRequired);
        }

        if (apartmentId == Guid.Empty)
        {
            throw new DomainException(DomainConstants.Notification.ApartmentNotFound);
        }

        var stamp = now ?? DateTimeOffset.UtcNow;
        return new NoticePdfAccessToken
        {
            Id = Guid.NewGuid(),
            Token = Guid.NewGuid(),
            GasInvoiceId = gasInvoiceId,
            ApartmentId = apartmentId,
            ExpiresAt = stamp.AddDays(DomainConstants.Notification.PdfTokenDays),
            CreatedOn = stamp,
        };
    }

    public bool IsValidAt(DateTimeOffset at) => at <= ExpiresAt;
}
