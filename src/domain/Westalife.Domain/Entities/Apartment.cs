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
}
