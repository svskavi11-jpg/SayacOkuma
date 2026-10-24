using Westalife.Domain.Common;
using Westalife.Domain.Constants;

namespace Westalife.Domain.Entities;

/// <summary>
/// Sayaç okuma düzeltme denetim izi (Etap 6.7). Her endeks düzeltmesinde ESKİ değer, YENİ değer,
/// zorunlu "Değişiklik Nedeni", kullanıcı ve zaman kaydedilir. Kayıtlar ASLA silinemez/güncellenemez.
/// </summary>
public class MeterReadingAuditLog : BaseEntity
{
    public Guid MeterReadingId { get; private set; }

    public MeterReading MeterReading { get; private set; } = null!;

    /// <summary>Düzeltme öncesi kümülatif endeks (kWh).</summary>
    public decimal OldIndexValue { get; private set; }

    /// <summary>Düzeltme sonrası kümülatif endeks (kWh).</summary>
    public decimal NewIndexValue { get; private set; }

    /// <summary>Düzeltme öncesi dönem tüketimi (kWh).</summary>
    public decimal OldConsumptionKWh { get; private set; }

    /// <summary>Düzeltme sonrası dönem tüketimi (kWh) — sonraki okumanın tüketimi de zincirleme güncellenir.</summary>
    public decimal NewConsumptionKWh { get; private set; }

    /// <summary>Kullanıcının yazması zorunlu olan değişiklik nedeni.</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>Düzeltmeyi yapan kullanıcı (Faz 7'ye kadar serbest metin; boşsa "Yönetici").</summary>
    public string ChangedBy { get; private set; } = string.Empty;

    /// <summary>Düzeltme zamanı (UTC).</summary>
    public DateTimeOffset ChangedOnUtc { get; private set; }

    private MeterReadingAuditLog() { } // EF Core

    public MeterReadingAuditLog(
        Guid meterReadingId,
        decimal oldIndexValue,
        decimal newIndexValue,
        decimal oldConsumptionKWh,
        decimal newConsumptionKWh,
        string reason,
        string changedBy,
        DateTimeOffset changedOnUtc)
    {
        if (meterReadingId == Guid.Empty)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.ReadingNotFound, nameof(meterReadingId));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.ReadingCorrectionReasonRequired, nameof(reason));
        }

        MeterReadingId = meterReadingId;
        OldIndexValue = oldIndexValue;
        NewIndexValue = newIndexValue;
        OldConsumptionKWh = oldConsumptionKWh;
        NewConsumptionKWh = newConsumptionKWh;
        Reason = reason.Trim();
        ChangedBy = string.IsNullOrWhiteSpace(changedBy)
            ? DomainConstants.MeterReadingAuditLog.DefaultChangedBy
            : changedBy.Trim();
        ChangedOnUtc = changedOnUtc;
    }
}