namespace Westalife.Domain.Enums;

/// <summary>Okumanın kaynağı — donanım detayı Domain'e sızmadan sadece kaynak tipi bilinir.</summary>
public enum ReadingSource
{
    /// <summary>Elle girilen okuma (örn. fatura üzerindeki endeks).</summary>
    ManualEntry = 1,

    /// <summary>M-Bus USB adaptörü üzerinden okuma.</summary>
    MbusUsb = 2,

    /// <summary>M-Bus Ethernet/TCP üzerinden okuma.</summary>
    MbusEthernet = 3,

    /// <summary>İçe aktarma (Excel/CSV).</summary>
    Import = 4,

    /// <summary>Simülatör üretimi (test ortamı).</summary>
    Simulator = 5
}
