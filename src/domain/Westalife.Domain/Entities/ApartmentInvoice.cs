using Westalife.Domain.Common;
using Westalife.Domain.Constants;

namespace Westalife.Domain.Entities;

/// <summary>
/// Bir fatura dönemi için tek dairenin hesaplanan borcu (hesaplama motorunun çıktısı).
/// Yeniden hesaplamada eski satırlar SİLİNMEZ; IsSuperseded = true ile pasifleşir (audit trail).
/// Tutarlar hesaplama motorunda kuruş bazında (long) üretilip decimal(18,2) olarak saklanır.
/// </summary>
public class ApartmentInvoice : BaseEntity
{
    public Guid GasInvoiceId { get; private set; }

    public GasInvoice GasInvoice { get; private set; } = null!;

    public Guid ApartmentId { get; private set; }

    public Apartment Apartment { get; private set; } = null!;

    /// <summary>Hesaplamada kullanılan dönemsel m² (SCD çözümü) — denetim için saklanır.</summary>
    public decimal SquareMetersUsed { get; private set; }

    /// <summary>Hesaplamada kullanılan dönemsel tüketim (kWh).</summary>
    public decimal ConsumptionKWhUsed { get; private set; }

    /// <summary>%30 sabit gider payı (kuruş hassasiyetiyle).</summary>
    public decimal FixedAmount { get; private set; }

    /// <summary>%70 tüketim gideri payı (kuruş hassasiyetiyle).</summary>
    public decimal ConsumptionAmount { get; private set; }

    /// <summary>Toplam borç = Fixed + Consumption (ctor'da kuruş hassasiyetiyle doğrulanır).</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>Sayaç okunamadıysa true — tüketim 0 kabul edildi, raporda işaretlenir (onaylı strateji §3).</summary>
    public bool IsMeterReadFailed { get; private set; }

    public DateTimeOffset CalculatedOnUtc { get; private set; }

    /// <summary>Yeniden hesaplamada eski satır pasifleşir; fiziksel silme YASAKTIR.</summary>
    public bool IsSuperseded { get; private set; }

    private ApartmentInvoice() { } // EF Core

    public ApartmentInvoice(
        Guid gasInvoiceId,
        Guid apartmentId,
        decimal squareMetersUsed,
        decimal consumptionKWhUsed,
        long fixedAmountKurus,
        long consumptionAmountKurus,
        bool isMeterReadFailed,
        DateTimeOffset calculatedOnUtc)
    {
        if (fixedAmountKurus < 0 || consumptionAmountKurus < 0)
        {
            throw new ArgumentException(DomainConstants.ApartmentInvoice.AmountMustBeNonNegative);
        }

        var total = fixedAmountKurus + consumptionAmountKurus;

        GasInvoiceId = gasInvoiceId;
        ApartmentId = apartmentId;
        SquareMetersUsed = squareMetersUsed;
        ConsumptionKWhUsed = consumptionKWhUsed;
        FixedAmount = fixedAmountKurus / 100m;
        ConsumptionAmount = consumptionAmountKurus / 100m;
        TotalAmount = total / 100m;
        IsMeterReadFailed = isMeterReadFailed;
        CalculatedOnUtc = calculatedOnUtc;
    }

    /// <summary>Yeniden hesaplama sonrası eski satırı pasifleştirir (fiziksel silme yasak).</summary>
    public void Supersede()
    {
        IsSuperseded = true;
        LastModifiedOn = DateTimeOffset.UtcNow;
    }
}
