using Westalife.Domain.Common;
using Westalife.Domain.Constants;
using Westalife.Domain.Enums;

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
public class ApartmentOwner : BaseEntity
{
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

    private ApartmentOwner() { } // EF Core

    public ApartmentOwner(
        Guid apartmentId,
        string ownerName,
        DateTimeOffset validFrom,
        ResidentType residentType = ResidentType.EvSahibi,
        string? phoneNumber = null,
        string? email = null,
        string? identityNumber = null,
        string? notes = null)
    {
        if (apartmentId == Guid.Empty)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.ApartmentNotFoundForDoorNumber, nameof(apartmentId));
        }

        if (string.IsNullOrWhiteSpace(ownerName))
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.OwnerNameRequired, nameof(ownerName));
        }

        if (phoneNumber is { Length: > DomainConstants.ApartmentOwner.PhoneMaxLength } ||
            (phoneNumber is { Length: > 0 } && !phoneNumber.Trim().All(ch => char.IsDigit(ch) || " ()+-".Contains(ch))))
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.SakinPhoneInvalid, nameof(phoneNumber));
        }

        if (email is { Length: > DomainConstants.ApartmentOwner.EmailMaxLength } ||
            (email is { Length: > 0 } && (!email.Trim().Contains('@') || email.Trim().Contains(' '))))
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.SakinEmailInvalid, nameof(email));
        }

        if (identityNumber is { Length: > 0 } &&
            (identityNumber.Trim().Length != DomainConstants.ApartmentOwner.IdentityNumberLength ||
             !identityNumber.Trim().All(char.IsDigit)))
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.SakinIdentityNumberInvalid, nameof(identityNumber));
        }

        if (notes is { Length: > DomainConstants.ApartmentOwner.NotesMaxLength })
        {
            throw new ArgumentException(
                $"Sakin notu en fazla {DomainConstants.ApartmentOwner.NotesMaxLength} karakter olabilir.",
                nameof(notes));
        }

        ApartmentId = apartmentId;
        OwnerName = ownerName.Trim();
        ResidentType = residentType;
        PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        IdentityNumber = string.IsNullOrWhiteSpace(identityNumber) ? null : identityNumber.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        ValidFrom = validFrom;
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
