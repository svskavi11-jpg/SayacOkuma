namespace Westalife.Application.Common.Models;

/// <summary>
/// Tek bir kalorimetreden okunan anlık görüntü.
/// Alanlar gerçek Izar Center veri kayıt sırasına (00–09) karşılık gelir:
/// 00 Energy kWh, 01 Volume m³, 02 Volume Flow, 03 Power, 04/05 sıcaklıklar,
/// 06 tarife-2 enerji, 07 Operating Time → Data Error bayrağı.
/// </summary>
public sealed record MeterSnapshot
{
    /// <summary>İkincil adres — 8 haneli üretim numarası (örn. 55238358).</summary>
    public required string SerialNumber { get; init; }

    /// <summary>M-Bus birincil adresi (örn. 58).</summary>
    public byte PrimaryAddress { get; init; }

    public required DateTimeOffset ReadingTimestampUtc { get; init; }

    /// <summary>Kümülatif enerji (kWh) — kayıt 00.</summary>
    public decimal CumulativeEnergyKWh { get; init; }

    /// <summary>Kümülatif hacim (m³) — kayıt 01.</summary>
    public decimal CumulativeVolumeM3 { get; init; }

    /// <summary>Anlık hacim akışı (m³/h) — kayıt 02.</summary>
    public decimal VolumeFlowM3PerHour { get; init; }

    /// <summary>Anlık güç (kW) — kayıt 03.</summary>
    public decimal PowerKw { get; init; }

    /// <summary>Gidiş sıcaklığı (°C) — kayıt 04.</summary>
    public decimal? FlowTemperatureC { get; init; }

    /// <summary>Dönüş sıcaklığı (°C) — kayıt 05.</summary>
    public decimal? ReturnTemperatureC { get; init; }

    /// <summary>Tarife-2 enerji (kWh) — kayıt 06 (genelde 0, opsiyonel).</summary>
    public decimal? Tariff2EnergyKWh { get; init; }

    /// <summary>Cihaz veri hatası bildiriyor mu? — kayıt 07 (Operating Time -> Data Error).</summary>
    public bool HasDataError { get; init; }

    /// <summary>Gelen ham telegram (hex) — cihaz firmware farklılığına karşı adaptasyon ve denetim amaçlı.</summary>
    public string? RawTelegramHex { get; init; }
}
