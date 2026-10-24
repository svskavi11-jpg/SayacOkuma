namespace Westalife.Application.Common.Enums;

/// <summary>Okuma bağlantı tipi. Donanım detayları Infrastructure'da kalmaya devam eder; sadece seçim bilgisi.</summary>
public enum MeterReaderConnectionType
{
    /// <summary>USB-RS485 adaptör üzerinden M-Bus (Serial).</summary>
    Usb = 1,

    /// <summary>Izar Center Ethernet/TCP üzerinden M-Bus.</summary>
    Tcp = 2,

    /// <summary>Simülatör — gerçek donanım olmadan test üretimi.</summary>
    Simulator = 3
}
