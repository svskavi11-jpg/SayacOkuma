using Westalife.Domain.Common;
using Westalife.Domain.Constants;

namespace Westalife.Domain.Entities;

/// <summary>
/// Dairenin dönemsel ölçüleri (SCD Type 2).
/// m² veya ortak alan oranı değiştiğinde eski kayıt ezilmez; ValidTo kapanır ve yeni kayıt açılır.
/// Kurallar (DB seviyesinde exclusion constraint ile de güvence altındadır):
///  - Bir dairenin zaman aralıkları asla çakışamaz.
///  - Bir dairenin ValidTo'su NULL olan en fazla 1 kaydı vardır (aktif kayıt).
/// </summary>
public class ApartmentDimension : BaseEntity
{
    public Guid ApartmentId { get; private set; }

    public Apartment Apartment { get; private set; } = null!;

    /// <summary>Daire brüt alanı (m²).</summary>
    public decimal SquareMeters { get; private set; }

    /// <summary>Ortak alan payı oranı (örn. 0.05 = %5 ek pay). Paylaştırmada m² ile birlikte çarpan olabilir.</summary>
    public decimal CommonAreaRatio { get; private set; }

    /// <summary>Bu değerlerin geçerli olduğu ilk an (timestamptz, dahil).</summary>
    public DateTimeOffset ValidFrom { get; private set; }

    /// <summary>Geçerlilik bitişi (timestamptz, hariç). NULL = hâlâ geçerli (aktif kayıt).</summary>
    public DateTimeOffset? ValidTo { get; private set; }

    private ApartmentDimension() { } // EF Core

    public ApartmentDimension(Guid apartmentId, decimal squareMeters, decimal commonAreaRatio, DateTimeOffset validFrom)
    {
        if (squareMeters <= 0m)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.SquareMetersMustBePositive, nameof(squareMeters));
        }

        if (commonAreaRatio < 0m)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.CommonAreaRatioMustBeNonNegative, nameof(commonAreaRatio));
        }

        ApartmentId = apartmentId;
        SquareMeters = squareMeters;
        CommonAreaRatio = commonAreaRatio;
        ValidFrom = validFrom;
    }

    /// <summary>Aktif kaydın geçerlilik aralığını kapatır (SCD Type 2 "expire" adımı).</summary>
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
