namespace Westalife.Application.Common.Enums;

/// <summary>Tek bir sayacın okunamama nedeni. Kısmi başarı (partial success) modelinin parçasıdır.</summary>
public enum MeterReadErrorCode
{
    /// <summary>Okuma başarılı (hata kodu yok).</summary>
    None = 0,

    /// <summary>Transport açılamadı / bağlantı koptu.</summary>
    ConnectionFailure = 1,

    /// <summary>Cihaz belirtilen sürede yanıt vermedi.</summary>
    Timeout = 2,

    /// <summary>Yanıt frame'i geçersiz (checksum, uzunluk, beklenmeyen C-field...).</summary>
    ProtocolError = 3,

    /// <summary>Cihaz kendisi veri hatası bildirdi (örn. Operating Time -> Data Error kaydı).</summary>
    DeviceError = 4,

    /// <summary>Sınıflandırılamayan hata.</summary>
    Unknown = 99
}
