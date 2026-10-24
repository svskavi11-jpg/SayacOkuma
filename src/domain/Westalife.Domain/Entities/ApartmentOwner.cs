using Westalife.Domain.Common;
using Westalife.Domain.Constants;

namespace Westalife.Domain.Entities;

/// <summary>
/// Dairenin kat maliki kaydı (SCD Type 2 — Etap 6.7).
/// Malik değiştiğinde eski kayıt EZİLMEZ; ValidTo kapanır ve yeni kayıt açılır.
/// m² bilgisiyle (ApartmentDimension) AYRI tutulur — malik ve m² değişim tarihleri farklı olabilir.
/// Kurallar (DB seviyesinde exclusion constraint ile güvence altında):
///  - Bir dairenin zaman aralıkları asla çakışamaz.
///  - Bir dairenin ValidTo'su NULL olan en fazla 1 kaydı vardır (aktif kayıt).
/// </summary>
public class ApartmentOwner : BaseEntity
{
    public Guid ApartmentId { get; private set; }

    public Apartment Apartment { get; private set; } = null!;

    /// <summary>Kat maliki adı soyadı.</summary>
    public string OwnerName { get; private set; } = string.Empty;

    /// <summary>Bu malikin geçerlilik başlangıcı (timestamptz, dahil).</summary>
    public DateTimeOffset ValidFrom { get; private set; }

    /// <summary>Geçerlilik bitişi (timestamptz, hariç). NULL = hâlâ aktif malik.</summary>
    public DateTimeOffset? ValidTo { get; private set; }

    private ApartmentOwner() { } // EF Core

    public ApartmentOwner(Guid apartmentId, string ownerName, DateTimeOffset validFrom)
    {
        if (apartmentId == Guid.Empty)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.ApartmentNotFoundForDoorNumber, nameof(apartmentId));
        }

        if (string.IsNullOrWhiteSpace(ownerName))
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.OwnerNameRequired, nameof(ownerName));
        }

        ApartmentId = apartmentId;
        OwnerName = ownerName.Trim();
        ValidFrom = validFrom;
    }

    /// <summary>Aktif malik kaydının aralığını kapatır (SCD Type 2 "expire" adımı).</summary>
    internal void Close(DateTimeOffset closedAt)
    {
        if (closedAt <= ValidFrom)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.ValidFromMustBeBeforeValidTo, nameof(closedAt));
        }

        ValidTo = closedAt;
        LastModifiedOn = DateTimeOffset.UtcNow;
    }

    /// <summary>Verilen zaman anında bu kayıt geçerli mi? (tstzrange semantiği: from dahil, to hariç)</summary>
    public bool IsValidAt(DateTimeOffset moment) =>
        ValidFrom <= moment && (ValidTo is null || moment < ValidTo.Value);
}