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

    // --- Bireysel Gider Bildirimi (PDF) ayarları ---

    /// <summary>Sol üst başlık metni (hizmet firması); sol logo yoksa metin olarak basılır.</summary>
    public string? NoticeLeftTitle { get; private set; }

    /// <summary>Sağ üst başlık metni (örn. Danfoss); sağ logo yoksa metin olarak basılır.</summary>
    public string? NoticeRightTitle { get; private set; }

    public byte[]? LogoLeft { get; private set; }

    public string? LogoLeftContentType { get; private set; }

    public byte[]? LogoRight { get; private set; }

    public string? LogoRightContentType { get; private set; }

    /// <summary>Sol logonun PDF'teki yüksekliği (mm); genişlik oranla otomatik belirlenir.</summary>
    public int LogoLeftHeightMm { get; private set; } = DomainConstants.AppSettings.LogoHeightMmDefault;

    /// <summary>Sağ logonun PDF'teki yüksekliği (mm).</summary>
    public int LogoRightHeightMm { get; private set; } = DomainConstants.AppSettings.LogoHeightMmDefault;

    /// <summary>Son ödeme tarihi = oluşturma tarihi + bu gün sayısı.</summary>
    public int DueDateDays { get; private set; } = DomainConstants.AppSettings.DueDateDaysDefault;

    /// <summary>"Okuma Hizmet Bedeli" satırı tutarı (varsayılan 0).</summary>
    public decimal ReadingServiceFee { get; private set; }

    /// <summary>Faturada özel not yoksa "Ek Açıklamalar" bölümünde basılacak varsayılan metin.</summary>
    public string? DefaultNoticeNote { get; private set; }

    /// <summary>Faturada abone no boşsa kullanılacak varsayılan abone numarası.</summary>
    public string? DefaultSubscriberNumber { get; private set; }

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

    /// <summary>Bildirim formu metin/sayısal ayarlarını günceller (logolar ayrı metotlarla yönetilir).</summary>
    public void UpdateNoticeSettings(
        string? leftTitle, string? rightTitle, int dueDateDays, decimal readingServiceFee,
        string? defaultNote, string? defaultSubscriberNumber)
    {
        if (dueDateDays < 0 || dueDateDays > DomainConstants.AppSettings.DueDateDaysMax)
        {
            throw new ArgumentException(DomainConstants.AppSettings.DueDateDaysInvalid, nameof(dueDateDays));
        }

        if (readingServiceFee < 0m)
        {
            throw new ArgumentException(DomainConstants.AppSettings.ReadingServiceFeeInvalid, nameof(readingServiceFee));
        }

        NoticeLeftTitle = Normalize(leftTitle);
        NoticeRightTitle = Normalize(rightTitle);
        DueDateDays = dueDateDays;
        ReadingServiceFee = decimal.Round(readingServiceFee, 2);
        DefaultNoticeNote = Normalize(defaultNote);
        DefaultSubscriberNumber = Normalize(defaultSubscriberNumber);
        LastModifiedOn = DateTimeOffset.UtcNow;
    }

    /// <summary>Sol/sağ logo yüksekliklerini (mm) ayarlar.</summary>
    public void SetLogoSizes(int leftHeightMm, int rightHeightMm)
    {
        if (leftHeightMm < DomainConstants.AppSettings.LogoHeightMmMin
            || leftHeightMm > DomainConstants.AppSettings.LogoHeightMmMax
            || rightHeightMm < DomainConstants.AppSettings.LogoHeightMmMin
            || rightHeightMm > DomainConstants.AppSettings.LogoHeightMmMax)
        {
            throw new ArgumentException(DomainConstants.AppSettings.LogoHeightInvalid);
        }

        LogoLeftHeightMm = leftHeightMm;
        LogoRightHeightMm = rightHeightMm;
        LastModifiedOn = DateTimeOffset.UtcNow;
    }

    /// <summary>Sol veya sağ logoyu ayarlar (PNG/JPG, en fazla 1 MB).</summary>
    public void SetLogo(string side, byte[] content, string contentType)
    {
        if (content.Length == 0 || content.Length > DomainConstants.AppSettings.LogoMaxBytes)
        {
            throw new DomainException(DomainConstants.AppSettings.LogoTooLarge);
        }

        if (contentType != "image/png" && contentType != "image/jpeg")
        {
            throw new DomainException(DomainConstants.AppSettings.LogoTypeInvalid);
        }

        var isPng = content.Length > 4 && content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47;
        var isJpeg = content.Length > 3 && content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF;
        if (!isPng && !isJpeg)
        {
            throw new DomainException(DomainConstants.AppSettings.LogoTypeInvalid);
        }

        // İçeriğin gerçek biçimi kaydedilen content-type'ı belirler.
        contentType = isPng ? "image/png" : "image/jpeg";

        switch (side)
        {
            case DomainConstants.AppSettings.LogoSideLeft:
                LogoLeft = content;
                LogoLeftContentType = contentType;
                break;
            case DomainConstants.AppSettings.LogoSideRight:
                LogoRight = content;
                LogoRightContentType = contentType;
                break;
            default:
                throw new DomainException(DomainConstants.AppSettings.LogoSideInvalid);
        }

        LastModifiedOn = DateTimeOffset.UtcNow;
    }

    /// <summary>Sol veya sağ logoyu kaldırır (metin başlığa dönülür).</summary>
    public void RemoveLogo(string side)
    {
        switch (side)
        {
            case DomainConstants.AppSettings.LogoSideLeft:
                LogoLeft = null;
                LogoLeftContentType = null;
                break;
            case DomainConstants.AppSettings.LogoSideRight:
                LogoRight = null;
                LogoRightContentType = null;
                break;
            default:
                throw new DomainException(DomainConstants.AppSettings.LogoSideInvalid);
        }

        LastModifiedOn = DateTimeOffset.UtcNow;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
