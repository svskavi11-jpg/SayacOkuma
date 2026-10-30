using Westalife.Domain.Common;
using Westalife.Domain.Constants;

namespace Westalife.Domain.Entities;

/// <summary>
/// Daire. m² ve ortak alan oranı bilgisi burada TUTULMAZ —
/// <see cref="ApartmentDimension"/> (SCD Type 2) üzerinden dönemsel olarak çözülür.
/// </summary>
public class Apartment : BaseEntity
{
    public Guid BuildingId { get; private set; }

    public Building Building { get; private set; } = null!;

    public string DoorNumber { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Devir (açılış) bakiyesi — ANAPARA kısmı (Etap 6.8 → ikiye ayrıldı).
    /// + = daire borçlu, - = alacaklı (fazla ödeme). Bu tutar <see cref="OpeningBalanceDate"/>
    /// tarihinden itibaren günlük basit faize tabidir.
    /// </summary>
    public decimal OpeningBalancePrincipal { get; private set; }

    /// <summary>
    /// Devir ile gelen GEÇMİŞ dönem gecikme faizi. Kapalı bir borçtur —
    /// bu tutara tekrar faiz İŞLEMEZ (faiz üzerine faiz yasağı).
    /// </summary>
    public decimal OpeningBalanceInterest { get; private set; }

    /// <summary>Devir bakiyesinin geçerli olduğu tarih (faiz bu tarihten işler).</summary>
    public DateOnly? OpeningBalanceDate { get; private set; }

    /// <summary>
    /// Faz 6.9: dairenin bağlı olduğu aidat tarifesı. Tarife-bazlı dağıtımda
    /// dönem borcu bu tarifenin tutarından hesaplanır. null = tarife atanmamış.
    /// </summary>
    public Guid? AidatTariffId { get; private set; }

    public AidatTariff? AidatTariff { get; private set; }

    private readonly List<ApartmentDimension> _dimensions = [];
    public IReadOnlyCollection<ApartmentDimension> Dimensions => _dimensions.AsReadOnly();

    private readonly List<ApartmentOwner> _owners = [];
    public IReadOnlyCollection<ApartmentOwner> Owners => _owners.AsReadOnly();

    private readonly List<Meter> _meters = [];
    public IReadOnlyCollection<Meter> Meters => _meters.AsReadOnly();

    private Apartment() { } // EF Core

    public Apartment(Guid buildingId, string doorNumber, string? title = null)
    {
        if (buildingId == Guid.Empty)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.ApartmentBuildingIdRequired, nameof(buildingId));
        }

        if (string.IsNullOrWhiteSpace(doorNumber))
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.ApartmentDoorNumberRequired, nameof(doorNumber));
        }

        BuildingId = buildingId;
        DoorNumber = doorNumber.Trim();
        Title = string.IsNullOrWhiteSpace(title) ? doorNumber.Trim() : title.Trim();
    }

    /// <summary>
    /// SCD Type 2: mevcut aktif kaydın ValidTo'su kapatılır ve yeni dimension kaydı açılır.
    /// Eski veri EZİLMEZ, sadece aralığı kapanır. Atomiklik transaction ile sağlanır (Faz 3).
    /// </summary>
    public ApartmentDimension AddDimension(decimal squareMeters, decimal commonAreaRatio, DateTimeOffset validFrom)
    {
        if (squareMeters <= 0m)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.SquareMetersMustBePositive, nameof(squareMeters));
        }

        if (commonAreaRatio < 0m)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.CommonAreaRatioMustBeNonNegative, nameof(commonAreaRatio));
        }

        var current = _dimensions.FirstOrDefault(d => d.ValidTo is null);
        if (current is not null)
        {
            current.Close(validFrom);
        }

        var dimension = new ApartmentDimension(Id, squareMeters, commonAreaRatio, validFrom);
        _dimensions.Add(dimension);
        LastModifiedOn = DateTimeOffset.UtcNow;
        return dimension;
    }

    /// <summary>
    /// Kat malikini günceller — SCD Type 2 (Etap 6.7): mevcut aktif malik kaydının ValidTo'su
    /// kapatılır ve yeni kayıt açılır. Eski malik ASLA silinmez, geçmiş sorgulanabilir kalır.
    /// Ad değişmediyse mevcut kayıt dokunulmadan döner (no-op).
    /// </summary>
    public ApartmentOwner UpdateOwner(string ownerName, DateTimeOffset validFrom)
    {
        if (string.IsNullOrWhiteSpace(ownerName))
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.OwnerNameRequired, nameof(ownerName));
        }

        var trimmed = ownerName.Trim();
        var current = _owners.FirstOrDefault(o => o.ValidTo is null);

        // No-op: aynı malik zaten aktif — yeni kayıt açılmaz (geçmiş gereksiz şişmez).
        if (current is not null && current.OwnerName.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
        {
            return current;
        }

        current?.Close(validFrom);

        var owner = new ApartmentOwner(Id, trimmed, validFrom);
        _owners.Add(owner);
        LastModifiedOn = DateTimeOffset.UtcNow;
        return owner;
    }

    /// <summary>
    /// Devir bakiyesini ANAPARA + GEÇMİŞ FAİZ olarak ayarlar. Her ikisi 0 verilirse devir temizlenir.
    /// Anapara veya faiz 0'dan farklıysa tarih zorunludur (faiz başlangıcı ve defter sırası için).
    /// Geçmiş faiz tutarına tekrar faiz işletilmez; faiz yalnızca anapara kalanından işler.
    /// </summary>
    public void SetOpeningBalance(decimal principal, decimal interest, DateOnly? balanceDate)
    {
        if (Math.Abs(principal) > DomainConstants.Apartment.OpeningBalanceMaxAbs)
        {
            throw new ArgumentException(DomainConstants.Apartment.OpeningBalanceOutOfRange, nameof(principal));
        }

        if (Math.Abs(interest) > DomainConstants.Apartment.OpeningBalanceMaxAbs)
        {
            throw new ArgumentException(DomainConstants.Apartment.OpeningBalanceOutOfRange, nameof(interest));
        }

        if ((principal != 0m || interest != 0m) && balanceDate is null)
        {
            throw new ArgumentException(DomainConstants.Apartment.OpeningBalanceDateRequired, nameof(balanceDate));
        }

        OpeningBalancePrincipal = decimal.Round(principal, 2);
        OpeningBalanceInterest = decimal.Round(interest, 2);
        OpeningBalanceDate = principal == 0m && interest == 0m ? null : balanceDate;
        LastModifiedOn = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Faz 6.9: dairenin aidat tarifesini atar veya temizler (null = tarifesiz).
    /// Aynı tarife tekrar atanırsa no-op döner (LastModifiedOn güncellenmez).
    /// </summary>
    public void SetAidatTariff(Guid? tariffId)
    {
        if (tariffId == Guid.Empty)
        {
            tariffId = null;
        }

        if (AidatTariffId == tariffId)
        {
            return;
        }

        AidatTariffId = tariffId;
        LastModifiedOn = DateTimeOffset.UtcNow;
    }
}
