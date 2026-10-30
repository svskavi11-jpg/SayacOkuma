using Westalife.Domain.Common;
using Westalife.Domain.Constants;

namespace Westalife.Domain.Entities;

/// <summary>
/// Aidat tarifesı (Faz 6.9). Daire tipine göre aylık aidat tutarını tanımlar.
/// Örnek: "1+1 C" → 32,1 m² → 2.000,00 TL/ay. Tarife-bazlı dağıtımda dairenin
/// borcu, daireye atanmış tarifenin tutarından hesaplanır.
/// </summary>
public class AidatTariff : BaseEntity
{
    /// <summary>Tarife adı (örn: "1+1 C", "2+1 A"). Bina içinde tekil olmalıdır.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Daire büyüklüğü (m²) — bilgilendirme amaçlı; m² dağıtımında
    /// ApartmentDimension (SCD) üzerinden gerçek m² kullanılır.</summary>
    public decimal SquareMeters { get; private set; }

    /// <summary>Aylık aidat tutarı (TL, kuruş hassasiyetiyle).</summary>
    public decimal Amount { get; private set; }

    /// <summary>Pasif tarife yeni tahakkukta seçilemez; geçmiş kayıtlar etkilenmez.</summary>
    public bool IsActive { get; private set; } = true;

    private AidatTariff() { } // EF Core

    public AidatTariff(string name, decimal squareMeters, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.AidatTariffNameRequired, nameof(name));
        }

        if (squareMeters <= 0m || squareMeters > DomainConstants.AidatTariff.SquareMetersMax)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.AidatTariffSquareMetersInvalid, nameof(squareMeters));
        }

        if (amount <= 0m || amount > DomainConstants.AidatTariff.AmountMax)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.AidatTariffAmountMustBePositive, nameof(amount));
        }

        Name = name.Trim();
        SquareMeters = decimal.Round(squareMeters, 2);
        Amount = decimal.Round(amount, 2);
    }

    /// <summary>Tarife bilgilerini günceller (mevcut tahakkuk satırları etkilenmez).</summary>
    public void Update(string name, decimal squareMeters, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.AidatTariffNameRequired, nameof(name));
        }

        if (squareMeters <= 0m || squareMeters > DomainConstants.AidatTariff.SquareMetersMax)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.AidatTariffSquareMetersInvalid, nameof(squareMeters));
        }

        if (amount <= 0m || amount > DomainConstants.AidatTariff.AmountMax)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.AidatTariffAmountMustBePositive, nameof(amount));
        }

        Name = name.Trim();
        SquareMeters = decimal.Round(squareMeters, 2);
        Amount = decimal.Round(amount, 2);
        LastModifiedOn = DateTimeOffset.UtcNow;
    }

    /// <summary>Tarifeyi pasif eder — yeni tahakkuklarda seçilemez.</summary>
    public void Deactivate()
    {
        IsActive = false;
        LastModifiedOn = DateTimeOffset.UtcNow;
    }

    /// <summary>Pasif tarife yeniden aktif edilir.</summary>
    public void Activate()
    {
        IsActive = true;
        LastModifiedOn = DateTimeOffset.UtcNow;
    }
}
