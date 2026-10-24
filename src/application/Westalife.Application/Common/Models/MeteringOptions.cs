using Westalife.Application.Common.Enums;

namespace Westalife.Application.Common.Models;

/// <summary>
/// Okuma motoru yapılandırması (appsettings "Metering" bölümünden bağlanır).
/// Varsayılanlar geliştirme ortamına uygundur: simülatör devrede, gerçek donanıma dokunulmaz.
/// </summary>
public sealed class MeteringOptions
{
    public const string SectionName = "Metering";

    public MeterReaderConnectionType ConnectionType { get; set; } = MeterReaderConnectionType.Simulator;

    /// <summary>Tek bir M-Bus istemi için yanıt bekleme süresi (ms).</summary>
    public int ResponseTimeoutMs { get; set; } = 3000;

    /// <summary>Geçici hatalarda (timeout/checksum) sayaç başına tekrar sayısı.</summary>
    public int MaxRetryCount { get; set; } = 2;

    /// <summary>Retry exponential backoff taban gecikmesi (ms).</summary>
    public int RetryBaseDelayMs { get; set; } = 500;

    public TcpOptions Tcp { get; set; } = new();

    public UsbOptions Usb { get; set; } = new();

    public SimulationOptions Simulation { get; set; } = new();

    public sealed class TcpOptions
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 2000;
    }

    public sealed class UsbOptions
    {
        public string PortName { get; set; } = string.Empty;
        public int BaudRate { get; set; } = 2400;
    }

    public sealed class SimulationOptions
    {
        /// <summary>Daire başına ortalama aylık tüketim (kWh) — kış profili üst sınırı.</summary>
        public decimal AverageMonthlyKWh { get; set; } = 850m;

        /// <summary>Bir okuma denemesinin "okunamadı" dönme olasılığı (0.05 = %5).</summary>
        public decimal FailureRate { get; set; } = 0.05m;
    }

    /// <summary>
    /// Gerçek okuma için M-Bus birincil adresleri (opsiyonel — Faz 3'te Meters tablosundan geçilecek).
    /// Boşsa MbusMeterReader boş batch + uyarı logu döner.
    /// </summary>
    public List<int> PrimaryAddresses { get; set; } = [];
}

/// <summary>
/// Raporlama yapılandırması ("Reporting" bölümü). Kural #2: dosya yolları relative —
/// mutlak yol yasak; logo dosyaları varsa kullanılır, yoksa metin fallback devreye girer.
/// </summary>
public sealed class ReportingOptions
{
    public const string SectionName = "Reporting";

    /// <summary>Son ödeme tarihi = işlenme tarihi + bu gün sayısı (örnek Danfoss formunda 15).</summary>
    public int DueDateDays { get; set; } = 15;

    /// <summary>Bildirim formundaki "Okuma Hizmet Bededi" satırı (varsayılan 0).</summary>
    public decimal ReadingServiceFeeAmount { get; set; } = 0m;

    /// <summary>Sol üst logo (relative path); dosya yoksa metin fallback kullanılır.</summary>
    public string LogoLeftPath { get; set; } = "docs/assets/logo-left.png";

    /// <summary>Sağ üst logo (relative path).</summary>
    public string LogoRightPath { get; set; } = "docs/assets/logo-right.png";

    /// <summary>Sol üst logo bulunamazsa basılacak metin.</summary>
    public string LogoLeftFallbackText { get; set; } = string.Empty;

    /// <summary>Sağ üst logo bulunamazsa basılacak metin.</summary>
    public string LogoRightFallbackText { get; set; } = string.Empty;
}
