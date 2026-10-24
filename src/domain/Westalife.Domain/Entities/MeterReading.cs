using Westalife.Domain.Common;
using Westalife.Domain.Constants;
using Westalife.Domain.Enums;

namespace Westalife.Domain.Entities;

/// <summary>
/// Tek bir sayaç okuma kaydı.
/// IndexValue kümülatif endekstir; ConsumptionKWh ise ölçülen cihazın bildirdiği dönem tüketimidir
/// (M-Bus kalorimetreler kümülatif kWh + dönem tüketimini ayrı register'larda bildirir).
/// MeterId + ReadingDateUtc unique'tir (aynı okuma iki kez kaydedilemez — idempotency).
/// </summary>
public class MeterReading : BaseEntity
{
    public Guid MeterId { get; private set; }

    public Meter Meter { get; private set; } = null!;

    /// <summary>Kümülatif endeks değeri (kalorimetre: kWh, ana doğalgaz sayacı: m³).</summary>
    public decimal IndexValue { get; private set; }

    /// <summary>Ölçülen cihazın bildirdiği dönem tüketimi (kWh). Ana sayaç okumalarında 0 olabilir.</summary>
    public decimal ConsumptionKWh { get; private set; }

    public DateTimeOffset ReadingDateUtc { get; private set; }

    public ReadingSource Source { get; private set; }

    /// <summary>Okuma faturalandırma döngüsünde işlendi mi?</summary>
    public bool IsProcessed { get; private set; }

    /// <summary>
    /// İlk kurulum kaydı mı? (Sayacın ilk okuması — tüketim değildir, sonraki okumaların
    /// fark hesabı bu endeksten başlar.)
    /// </summary>
    public bool IsInitialReading { get; private set; }

    private MeterReading() { } // EF Core

    public MeterReading(Guid meterId, decimal indexValue, decimal consumptionKWh, DateTimeOffset readingDateUtc, ReadingSource source, bool isInitialReading = false)
    {
        if (meterId == Guid.Empty)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.SerialNumberRequired, nameof(meterId));
        }

        if (indexValue < 0m)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.MeterIndexValueMustBeNonNegative, nameof(indexValue));
        }

        if (consumptionKWh < 0m || consumptionKWh > DomainConstants.MeterReading.MaxPeriodConsumptionKWh)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.MeterConsumptionMustBeValid, nameof(consumptionKWh));
        }

        MeterId = meterId;
        IndexValue = indexValue;
        ConsumptionKWh = consumptionKWh;
        ReadingDateUtc = readingDateUtc;
        Source = source;
        IsInitialReading = isInitialReading;
    }

    public void MarkAsProcessed()
    {
        IsProcessed = true;
        LastModifiedOn = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Okuma düzeltmesi (Etap 6.7): endeks ve tüketim alanlarını günceller.
    /// Zincir bütünlüğü (önceki/sonraki okumaya göre tüketim) Application katmanında hesaplanır;
    /// bu metot yalnızca domain doğrulamalarıyla alanları yazar.
    /// </summary>
    public void SetIndex(decimal newIndexValue, decimal newConsumptionKWh, bool newIsInitialReading)
    {
        if (newIndexValue < 0m)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.MeterIndexValueMustBeNonNegative, nameof(newIndexValue));
        }

        if (newConsumptionKWh < 0m || newConsumptionKWh > DomainConstants.MeterReading.MaxPeriodConsumptionKWh)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.MeterConsumptionMustBeValid, nameof(newConsumptionKWh));
        }

        IndexValue = newIndexValue;
        ConsumptionKWh = newConsumptionKWh;
        IsInitialReading = newIsInitialReading;
        LastModifiedOn = DateTimeOffset.UtcNow;
    }
}
