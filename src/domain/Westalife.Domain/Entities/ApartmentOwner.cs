using Westalife.Domain.Common;
using Westalife.Domain.Constants;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;

namespace Westalife.Domain.Entities;

/// <summary>
/// Daire sakin kaydı (SCD Type 2 — Etap 6.7, Faz 7.1 genişletildi).
/// Faz 7.1 öncesi yalnızca kat maliki tutuluyordu; artık Ev Sahibi + Kiracı ayrı akışlarda
/// SCD Type 2 olarak tutulur: her türün kendi zaman çizelgesi vardır ve aynı anda
/// 1 aktif Ev Sahibi + 1 aktif Kiracı bulunabilir.
/// Kurallar (DB seviyesinde ResidentType kapsamlı constraint'lerle güvence altında):
///  - Aynı daire + aynı tür için zaman aralıkları asla çakışamaz.
///  - Aynı daire + aynı türün ValidTo'su NULL olan en fazla 1 kaydı vardır (aktif kayıt).
/// </summary>
public class ApartmentOwner : BaseEntity, ITenantEntity
{
    /// <summary>Kaydın ait olduğu site.</summary>
    public Guid TenantId { get; private set; }

    public void AssignTenant(Guid tenantId) => TenantId = tenantId;

    public Guid ApartmentId { get; private set; }

    public Apartment Apartment { get; private set; } = null!;

    /// <summary>Sakin adı soyadı (ev sahibi veya kiracı).</summary>
    public string OwnerName { get; private set; } = string.Empty;

    /// <summary>Sakin türü (Ev Sahibi / Kiracı).</summary>
    public ResidentType ResidentType { get; private set; }

    /// <summary>Telefon (opsiyonel, max 20 — rakam ve " ()+-" karakterleri).</summary>
    public string? PhoneNumber { get; private set; }

    /// <summary>E-posta (opsiyonel, basit @ doğrulaması).</summary>
    public string? Email { get; private set; }

    /// <summary>TCKN (opsiyonel — girildiyse 11 haneli rakam).</summary>
    public string? IdentityNumber { get; private set; }

    /// <summary>Yönetici notu (opsiyonel, max 500).</summary>
    public string? Notes { get; private set; }

    /// <summary>Bu sakinin geçerlilik başlangıcı (timestamptz, dahil).</summary>
    public DateTimeOffset ValidFrom { get; private set; }

    /// <summary>Geçerlilik bitişi (timestamptz, hariç). NULL = hâlâ aktif sakin.</summary>
    public DateTimeOffset? ValidTo { get; private set; }

    /// <summary>Kira veya tapu sözleşmesinin başlangıcı. Oturma çizelgesinden bağımsızdır.</summary>
    public DateOnly? LeaseStartDate { get; private set; }

    /// <summary>Sözleşme bitişi. Oturma çizelgesinden bağımsızdır.</summary>
    public DateOnly? LeaseEndDate { get; private set; }

    private ApartmentOwner() { } // EF Core

    public ApartmentOwner(
        Guid apartmentId,
        string ownerName,
        DateTimeOffset validFrom,
        ResidentType residentType = ResidentType.EvSahibi,
        string? phoneNumber = null,
        string? email = null,
        string? identityNumber = null,
        string? notes = null,
        DateOnly? leaseStartDate = null,
        DateOnly? leaseEndDate = null,
        DateTimeOffset? validTo = null)
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
        ResidentType = residentType;
        ApplyContact(phoneNumber, email, identityNumber, notes);
        ValidFrom = validFrom;
        if (validTo is { } end)
        {
            if (end <= validFrom)
            {
                throw new ArgumentException(DomainConstants.ValidationMessages.ValidFromMustBeBeforeValidTo, nameof(validTo));
            }

            ValidTo = end;
        }

        SetLease(leaseStartDate, leaseEndDate);
    }

    /// <summary>Sözleşme günlerini yazar. Oturma aralığını değiştirmez.</summary>
    internal void SetLease(DateOnly? start, DateOnly? end)
    {
        if (start is { } leaseStart && end is { } leaseEnd && leaseEnd < leaseStart)
        {
            throw new DomainException(DomainConstants.ApartmentOwner.LeaseEndBeforeStart);
        }

        LeaseStartDate = start;
        LeaseEndDate = end;
    }

    /// <summary>Açık kaydın girişini kaydırır. Bitişten sonraya geçemez.</summary>
    internal void MoveStart(DateTimeOffset newFrom)
    {
        if (ValidTo is { } end && newFrom >= end)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.ValidFromMustBeBeforeValidTo, nameof(newFrom));
        }

        if (newFrom == ValidFrom)
        {
            return;
        }

        ValidFrom = newFrom;
        LastModifiedOn = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Aynı sakinin iletişim bilgisini yerinde günceller. Ad değişmediği için yeni SCD satırı açılmaz.
    /// Değerler zaten aynıysa false döner.
    /// </summary>
    internal bool UpdateContact(string? phoneNumber, string? email, string? identityNumber, string? notes)
    {
        var phone = NormalizePhone(phoneNumber);
        var mail = NormalizeEmail(email);
        var identity = NormalizeIdentity(identityNumber);
        var note = NormalizeNotes(notes);

        if (PhoneNumber == phone && Email == mail && IdentityNumber == identity && Notes == note)
        {
            return false;
        }

        PhoneNumber = phone;
        Email = mail;
        IdentityNumber = identity;
        Notes = note;
        LastModifiedOn = DateTimeOffset.UtcNow;
        return true;
    }

    private void ApplyContact(string? phoneNumber, string? email, string? identityNumber, string? notes)
    {
        PhoneNumber = NormalizePhone(phoneNumber);
        Email = NormalizeEmail(email);
        IdentityNumber = NormalizeIdentity(identityNumber);
        Notes = NormalizeNotes(notes);
    }

    private static string? NormalizePhone(string? phoneNumber)
    {
        if (phoneNumber is { Length: > DomainConstants.ApartmentOwner.PhoneMaxLength } ||
            (phoneNumber is { Length: > 0 } && !phoneNumber.Trim().All(ch => char.IsDigit(ch) || " ()+-".Contains(ch))))
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.SakinPhoneInvalid, nameof(phoneNumber));
        }

        return string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
    }

    private static string? NormalizeEmail(string? email)
    {
        if (email is { Length: > DomainConstants.ApartmentOwner.EmailMaxLength } ||
            (email is { Length: > 0 } && (!email.Trim().Contains('@') || email.Trim().Contains(' '))))
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.SakinEmailInvalid, nameof(email));
        }

        return string.IsNullOrWhiteSpace(email) ? null : email.Trim();
    }

    private static string? NormalizeIdentity(string? identityNumber)
    {
        if (identityNumber is { Length: > 0 } &&
            (identityNumber.Trim().Length != DomainConstants.ApartmentOwner.IdentityNumberLength ||
             !identityNumber.Trim().All(char.IsDigit)))
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.SakinIdentityNumberInvalid, nameof(identityNumber));
        }

        return string.IsNullOrWhiteSpace(identityNumber) ? null : identityNumber.Trim();
    }

    private static string? NormalizeNotes(string? notes)
    {
        if (notes is { Length: > DomainConstants.ApartmentOwner.NotesMaxLength })
        {
            throw new ArgumentException(
                $"Sakin notu en fazla {DomainConstants.ApartmentOwner.NotesMaxLength} karakter olabilir.",
                nameof(notes));
        }

        return string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    /// <summary>Aktif sakin kaydının aralığını kapatır (SCD Type 2 "expire" adımı).</summary>
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
