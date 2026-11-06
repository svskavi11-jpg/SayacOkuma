using Westalife.Domain.Common;
using Westalife.Domain.Constants;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;

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

    /// <summary>Bu devir borcunun sahibi. Çıkmış bir sakin de seçilebilir. Boşsa daire düzeyindedir.</summary>
    public Guid? OpeningBalanceOwnerId { get; private set; }

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
    /// Faz 7.1: EvSahibi türü için <see cref="UpdateResident"/> delegasyonu.
    /// </summary>
    public ApartmentOwner UpdateOwner(string ownerName, DateTimeOffset validFrom) =>
        UpdateResident(ResidentType.EvSahibi, ownerName, null, null, null, null, validFrom, replaceContact: false);

    /// <summary>
    /// Faz 7.1: Sakin ekler/günceller (Ev Sahibi veya Kiracı — SCD Type 2, tür bazlı ayrı akış).
    /// Ad aynıysa iletişim bilgisi mevcut satırda güncellenir. Ad değişirse eski kayıt kapanır
    /// ve iletişim bilgileriyle yeni kayıt açılır. Eski sakin kayıtları ASLA silinmez.
    /// Malik değiştirme ad-only akışı <paramref name="replaceContact"/> false ile çağrılır;
    /// ad aynıysa iletişim bilgisine dokunulmaz.
    /// </summary>
    public ApartmentOwner UpdateResident(
        ResidentType residentType,
        string ownerName,
        string? phoneNumber,
        string? email,
        string? identityNumber,
        string? notes,
        DateTimeOffset validFrom,
        bool replaceContact = true,
        DateTimeOffset? validTo = null,
        DateOnly? leaseStart = null,
        DateOnly? leaseEnd = null)
    {
        if (string.IsNullOrWhiteSpace(ownerName))
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.OwnerNameRequired, nameof(ownerName));
        }

        var trimmed = ownerName.Trim();

        // Bitiş doluysa çıkmış sakindir. Açık kayıt kapanmaz.
        if (validTo is { } end)
        {
            EnsureNoOverlap(residentType, validFrom, end, Guid.Empty);
            var closed = new ApartmentOwner(
                Id, trimmed, validFrom, residentType, phoneNumber, email, identityNumber, notes,
                leaseStart, leaseEnd, end);
            _owners.Add(closed);
            LastModifiedOn = DateTimeOffset.UtcNow;
            return closed;
        }

        var current = _owners.FirstOrDefault(o => o.ResidentType == residentType && o.ValidTo is null);

        // Aynı kişi: yeni SCD satırı açılmaz. İletişim ve giriş günü mevcut satıra yazılır.
        if (current is not null && current.OwnerName.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
        {
            if (replaceContact)
            {
                current.UpdateContact(phoneNumber, email, identityNumber, notes);
            }

            current.MoveStart(validFrom);
            current.SetLease(leaseStart, leaseEnd);
            EnsureNoOverlap(residentType, current.ValidFrom, current.ValidTo, current.Id);
            LastModifiedOn = DateTimeOffset.UtcNow;
            return current;
        }

        if (current is not null && validFrom <= current.ValidFrom)
        {
            throw new DomainException(DomainConstants.ApartmentOwner.StartMustFollowCurrent);
        }

        current?.Close(validFrom);

        var resident = new ApartmentOwner(
            Id, trimmed, validFrom, residentType, phoneNumber, email, identityNumber, notes,
            leaseStart, leaseEnd);
        _owners.Add(resident);
        EnsureNoOverlap(residentType, resident.ValidFrom, resident.ValidTo, resident.Id);
        LastModifiedOn = DateTimeOffset.UtcNow;
        return resident;
    }

    private void EnsureNoOverlap(ResidentType residentType, DateTimeOffset from, DateTimeOffset? to, Guid exceptId)
    {
        var thisEnd = to ?? DateTimeOffset.MaxValue;
        foreach (var other in _owners)
        {
            if (other.Id == exceptId || other.ResidentType != residentType)
            {
                continue;
            }

            var otherEnd = other.ValidTo ?? DateTimeOffset.MaxValue;
            if (from < otherEnd && other.ValidFrom < thisEnd)
            {
                throw new DomainException(DomainConstants.ApartmentOwner.PeriodOverlap);
            }
        }
    }

    /// <summary>Verilen anda aktif sakin (tür bazlı) — yoksa null.</summary>
    public ApartmentOwner? GetActiveResident(ResidentType residentType, DateTimeOffset moment) =>
        _owners.LastOrDefault(o => o.ResidentType == residentType && o.IsValidAt(moment));

    /// <summary>
    /// Hibrit Model (KMK m.22): borç sahibi çözümlemesi. O anda kiracı varsa borç kiracıya,
    /// yoksa ev sahibine yazılır. Ev sahibi her durumda müteselsil sorumlu olarak
    /// borç satırına ayrıca kaydedilir (LegalResponsibleOwnerId — Application katmanı).
    /// </summary>
    public ApartmentOwner? ResolveDebtOwner(DateTimeOffset moment) =>
        GetActiveResident(ResidentType.Kiraci, moment)
        ?? GetActiveResident(ResidentType.EvSahibi, moment);

    /// <summary>
    /// Devir bakiyesini ANAPARA + GEÇMİŞ FAİZ olarak ayarlar. Her ikisi 0 verilirse devir temizlenir.
    /// Anapara veya faiz 0'dan farklıysa tarih zorunludur (faiz başlangıcı ve defter sırası için).
    /// Geçmiş faiz tutarına tekrar faiz işletilmez; faiz yalnızca anapara kalanından işler.
    /// </summary>
    public void SetOpeningBalance(decimal principal, decimal interest, DateOnly? balanceDate, Guid? ownerId = null)
    {
        if (Math.Abs(principal) > DomainConstants.Apartment.OpeningBalanceMaxAbs)
        {
            throw new ArgumentException(DomainConstants.Apartment.OpeningBalanceOutOfRange, nameof(principal));
        }

        if (interest < 0m)
        {
            throw new ArgumentException(DomainConstants.Apartment.OpeningBalanceInterestCannotBeNegative, nameof(interest));
        }

        if (interest > DomainConstants.Apartment.OpeningBalanceMaxAbs)
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
        OpeningBalanceOwnerId = principal == 0m && interest == 0m ? null : ownerId;
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
