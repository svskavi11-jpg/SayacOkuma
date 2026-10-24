using Westalife.Domain.Common;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Domain.Entities;

/// <summary>
/// Sistem genelinde tek satırlık ayar kaydı (singleton). Bina adı, şirket adı (PDF başlığı)
/// ve 30/70 oranları burada yönetilir; PUT işleminde Building tablosuna aynalanır.
/// </summary>
public class AppSettings : BaseEntity
{
    public string CompanyName { get; private set; } = string.Empty;

    public string BuildingName { get; private set; } = string.Empty;

    public decimal FixedCostRatio { get; private set; }

    public decimal ConsumptionRatio { get; private set; }

    /// <summary>M-Bus baglanti tipi: "Simulator" / "Tcp" / "Usb".</summary>
    public string MeterConnectionType { get; private set; } = DomainConstants.AppSettings.ConnectionSimulator;

    /// <summary>Izar Center IP/host (Tcp tipinde kullanilir).</summary>
    public string? TcpHost { get; private set; }

    public int TcpPort { get; private set; } = 2000;

    /// <summary>COM port adi (Usb tipinde kullanilir, ornek: COM3).</summary>
    public string? UsbPortName { get; private set; }

    public int UsbBaudRate { get; private set; } = 2400;

    private AppSettings() { } // EF Core

    public AppSettings(string companyName, string buildingName, decimal fixedCostRatio, decimal consumptionRatio)
    {
        Update(companyName, buildingName, fixedCostRatio, consumptionRatio);
    }

    /// <summary>
    /// Ayarları günceller. Kural: Sabit + Tüketim tam %100 olmalıdır (kuruş hassasiyetiyle).
    /// </summary>
    public void Update(string companyName, string buildingName, decimal fixedCostRatio, decimal consumptionRatio)
    {
        if (string.IsNullOrWhiteSpace(companyName))
        {
            throw new ArgumentException(DomainConstants.AppSettings.CompanyNameRequired, nameof(companyName));
        }

        if (string.IsNullOrWhiteSpace(buildingName))
        {
            throw new ArgumentException(DomainConstants.AppSettings.BuildingNameRequired, nameof(buildingName));
        }

        if (fixedCostRatio <= 0m || consumptionRatio <= 0m)
        {
            throw new ArgumentException(DomainConstants.AppSettings.RatiosMustBePositive);
        }

        if (decimal.Round(fixedCostRatio + consumptionRatio, 2) != 1m)
        {
            throw new DomainException(DomainConstants.AppSettings.RatiosMustSumToOne);
        }

        CompanyName = companyName.Trim();
        BuildingName = buildingName.Trim();
        FixedCostRatio = fixedCostRatio;
        ConsumptionRatio = consumptionRatio;
        LastModifiedOn = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// M-Bus donanim baglanti ayarlarini gunceller. Bağlanti tipine göre zorunlu alanlar doğrulanır.
    /// </summary>
    public void UpdateMeterConnection(
        string meterConnectionType, string? tcpHost, int tcpPort, string? usbPortName, int usbBaudRate)
    {
        if (meterConnectionType != DomainConstants.AppSettings.ConnectionSimulator &&
            meterConnectionType != DomainConstants.AppSettings.ConnectionTcp &&
            meterConnectionType != DomainConstants.AppSettings.ConnectionUsb)
        {
            throw new ArgumentException(DomainConstants.AppSettings.MeterConnectionTypeInvalid, nameof(meterConnectionType));
        }

        if (meterConnectionType == DomainConstants.AppSettings.ConnectionTcp && string.IsNullOrWhiteSpace(tcpHost))
        {
            throw new ArgumentException(DomainConstants.AppSettings.TcpHostRequired, nameof(tcpHost));
        }

        if (meterConnectionType == DomainConstants.AppSettings.ConnectionUsb && string.IsNullOrWhiteSpace(usbPortName))
        {
            throw new ArgumentException(DomainConstants.AppSettings.UsbPortNameRequired, nameof(usbPortName));
        }

        // Etap 6.7 bug fix: TcpPort/UsbBaudRate aralıkları yalnız İLGİLİ bağlantı tipinde doğrulanır —
        // "Simulator" seçiliyken bu alanlar (genelde 0 gelir) validasyon hatası ÜRETMEZ.
        if (meterConnectionType == DomainConstants.AppSettings.ConnectionTcp
            && (tcpPort < DomainConstants.AppSettings.TcpPortMin || tcpPort > DomainConstants.AppSettings.TcpPortMax))
        {
            throw new ArgumentException(DomainConstants.AppSettings.MeterConnectionTypeInvalid, nameof(tcpPort));
        }

        if (meterConnectionType == DomainConstants.AppSettings.ConnectionUsb && usbBaudRate <= 0)
        {
            throw new ArgumentException(DomainConstants.AppSettings.MeterConnectionTypeInvalid, nameof(usbBaudRate));
        }

        MeterConnectionType = meterConnectionType;
        TcpHost = meterConnectionType == DomainConstants.AppSettings.ConnectionTcp ? tcpHost?.Trim() : null;
        TcpPort = meterConnectionType == DomainConstants.AppSettings.ConnectionTcp
            ? tcpPort
            : DomainConstants.AppSettings.TcpPortDefault;
        UsbPortName = meterConnectionType == DomainConstants.AppSettings.ConnectionUsb ? usbPortName?.Trim() : null;
        UsbBaudRate = meterConnectionType == DomainConstants.AppSettings.ConnectionUsb
            ? usbBaudRate
            : DomainConstants.AppSettings.UsbBaudRateDefault;
        LastModifiedOn = DateTimeOffset.UtcNow;
    }
}
