namespace Westalife.Domain.Constants;

/// <summary>
/// Domain genelinde kullanılan tüm sabitler. Magic string/number yasağı nedeniyle
/// her alan uzunluğu, hassasiyet ve doğrulama mesajı burada tanımlanır.
/// </summary>
public static class DomainConstants
{
    /// <summary>Para birimi alanları hassasiyeti (TL, kuruş hassasiyeti).</summary>
    public const int MoneyPrecision = 18;
    public const int MoneyScale = 2;

    /// <summary>Enerji (kWh) alanları hassasiyeti — fatura 4 ondalıklı kWh içerir (7.273,65).</summary>
    public const int EnergyPrecision = 18;
    public const int EnergyScale = 4;

    /// <summary>Hacim (m³) alanları hassasiyeti — fatura 2 ondalık kullanır (693,25).</summary>
    public const int VolumePrecision = 18;
    public const int VolumeScale = 3;

    /// <summary>Oran (FixedCostRatio, CommonAreaRatio) hassasiyeti — 0,30 gibi değerler.</summary>
    public const int RatioPrecision = 9;
    public const int RatioScale = 6;

    /// <summary>Endeks (kümülatif sayaç değeri) hassasiyeti — 140.399 gibi tam sayıya yakın.</summary>
    public const int IndexPrecision = 18;
    public const int IndexScale = 3;

    public static class Building
    {
        public const int NameMaxLength = 200;
        public const int AddressMaxLength = 500;

        /// <summary>Sabit gider varsayılan oranı (%30 → 0.30). Sistem verisi, değiştirilebilir.</summary>
        public const decimal DefaultFixedCostRatio = 0.30m;
    }

    public static class Apartment
    {
        public const int DoorNumberMaxLength = 10;
        public const int TitleMaxLength = 100;
        public const int MinDoorNumber = 1;
    }

    public static class ApartmentDimension
    {
        public const int MaxSquareMeters = 1000;
    }

    public static class Meter
    {
        public const int SerialNumberMaxLength = 50;
        public const int ManufacturerMaxLength = 50;
    }

    public static class MeterReading
    {
        /// <summary>Tek bir okumada ölçülen dönem tüketimi için makul üst sınır (kWh, dönem bazlı).</summary>
        public const decimal MaxPeriodConsumptionKWh = 100_000m;
    }

    /// <summary>Kat maliki SCD Type 2 kayıt kuralları (Etap 6.7).</summary>
    public static class ApartmentOwner
    {
        public const int OwnerNameMaxLength = 100;
    }

    /// <summary>Okuma düzeltme denetim izi kuralları (Etap 6.7).</summary>
    public static class MeterReadingAuditLog
    {
        public const int ReasonMaxLength = 500;
        public const int ChangedByMaxLength = 100;

        /// <summary>Faz 7'ye kadar (JWT yok) düzeltmeyi yapan kullanıcı boş bırakılırsa kullanılır.</summary>
        public const string DefaultChangedBy = "Yönetici";
    }

    public static class GasInvoice
    {
        public const int InvoiceNumberMaxLength = 50;
        public const int SubscriberNumberMaxLength = 20;
        public const int MeterSerialMaxLength = 20;
        public const int NoteMaxLength = 1000;

        /// <summary>Dönem kilidi kuralları (Etap 6.6).</summary>
        public const string PeriodLocked =
            "Dönem kilitli: bu döneme okuma eklenemez. Önce fatura listesinden kilidi açın.";

        public const string RecalculateBlocked =
            "Dönem kilitli: yeniden hesaplama yapılamaz (denetim bütünlüğü). Önce kilidi açın.";

        public const string LockRequiresCalculation =
            "Hesaplanmamış fatura kilitlenemez. Önce 30/70 dağıtımını hesaplayın.";

        public const string InvoiceNotFound = "Belirtilen fatura bulunamadı.";
    }

    /// <summary>Örnek (seed) veri sabitleri. Faz 5'te arayüzden gerçek değerler girilene kadar geçerlidir.</summary>
    public static class SeedData
    {
        /// <summary>Westalife Residence: 72 daire.</summary>
        public const int ApartmentCount = 72;

        /// <summary>Gerçek m² değerleri bilinene kadar kullanılan varsayılan alan.</summary>
        public const decimal DefaultSquareMeters = 100m;

        public const decimal DefaultCommonAreaRatio = 0m;

        /// <summary>Seed binanın sabit Id'si (deterministik — migration'lar tekrar üretilebilir olmalı).</summary>
        public static readonly Guid BuildingId = new("00000000-0000-0000-0000-000000000001");

        /// <summary>Seed geçerlilik başlangıcı ve kayıt zamanı (deterministik).</summary>
        public static readonly DateTimeOffset SeedTimestamp = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    }

    public static class CostDistribution
    {
        public const string InvalidFixedCostRatio = "Sabit gider oranı 0 ile 1 arasında olmalıdır.";
        public const string InvalidTotalAmount = "Dağıtılacak tutar pozitif olmalıdır.";
        public const string NoPositiveWeight = "Dağıtım anahtarı toplamı sıfır olamaz (en az bir dairenin payı pozitif olmalıdır).";
        public const string ZeroTotalConsumption =
            "Dönemde hiç tüketim verisi yok: %70'lik tüketim gideri dağıtılamaz. Önce okuma girilmelidir.";
        public const string TotalAllocationMismatch =
            "Kalan kuruşlar dağıtıldıktan sonra toplam eşleşmedi (beklenmeyen durum).";
    }

    public static class ApartmentInvoice
    {
        /// <summary>Kalemlerin toplamı genel toplama kuruşu kuruşuna eşit olmalı (Kural #1'in satır bazlı yansıması).</summary>
        public const string TotalMustEqualParts =
            "Daire borç satırı tutarları tutarsız: Sabit + Tüketim, Genel Toplam ile uyuşmuyor.";

        public const string AmountMustBeNonNegative = "Daire borç satırı tutarları negatif olamaz.";
    }

    public static class AppSettings
    {
        public const int CompanyNameMaxLength = 100;
        public const string CompanyNameRequired = "Şirket adı zorunludur.";
        public const string BuildingNameRequired = "Bina adı zorunludur.";
        public const string RatiosMustSumToOne = "Sabit gider oranı + Tüketim oranı tam %100 olmalıdır.";
        public const string RatiosMustBePositive = "Oranlar sıfırdan büyük olmalıdır.";

        /// <summary>M-Bus bağlantı tipleri (DB'de string olarak saklanır).</summary>
        public const string ConnectionSimulator = "Simulator";
        public const string ConnectionTcp = "Tcp";
        public const string ConnectionUsb = "Usb";
        public const string MeterConnectionTypeInvalid = "Bağlantı tipi Simulator, Tcp veya Usb olmalıdır.";
        public const string TcpHostRequired = "TCP bağlantı tipi için Izar Center IP adresi (host) zorunludur.";
        public const string UsbPortNameRequired = "USB bağlantı tipi için COM portu zorunludur.";
        public const int TcpPortMin = 1;
        public const int TcpPortMax = 65535;

        /// <summary>Tcp/Usb dışı tipte (Simulator) alanlar nötr varsayılanlara çekilir (Etap 6.7).</summary>
        public const int TcpPortDefault = 2000;
        public const int UsbBaudRateDefault = 2400;

        public const int TcpHostMaxLength = 100;
        public const int UsbPortNameMaxLength = 20;

        /// <summary>Bireysel Gider Bildirimi form ayarları.</summary>
        public const int NoticeTitleMaxLength = 100;
        public const int NoticeNoteMaxLength = 1000;
        public const int SubscriberNumberMaxLength = 20;
        public const int DueDateDaysDefault = 15;
        public const int DueDateDaysMax = 120;
        public const int LogoMaxBytes = 1_048_576; // 1 MB
        public const int LogoHeightMmMin = 8;
        public const int LogoHeightMmMax = 40;
        public const int LogoHeightMmDefault = 17;
        public const string LogoHeightInvalid = "Logo yüksekliği 8 ile 40 mm arasında olmalıdır.";
        public const string LogoSideLeft = "left";
        public const string LogoSideRight = "right";
        public const string LogoTooLarge = "Logo dosyası en fazla 1 MB olabilir.";
        public const string LogoTypeInvalid = "Logo yalnızca PNG veya JPG olabilir.";
        public const string LogoSideInvalid = "Logo tarafı 'left' veya 'right' olmalıdır.";
        public const string DueDateDaysInvalid = "Son ödeme gün sayısı 0 ile 120 arasında olmalıdır.";
        public const string ReadingServiceFeeInvalid = "Okuma hizmet bedeli negatif olamaz.";
    }

    public static class ValidationMessages
    {
        public const string BuildingNameRequired = "Bina adı zorunludur.";
        public const string BuildingAddressRequired = "Bina adresi zorunludur.";
        public const string ApartmentBuildingIdRequired = "Dairenin bağlı olduğu bina kimliği (BuildingId) zorunludur.";
        public const string ApartmentDoorNumberRequired = "Daire kapı numarası zorunludur.";
        public const string SquareMetersMustBePositive = "Daire alanı (m²) sıfırdan büyük olmalıdır.";
        public const string CommonAreaRatioMustBeNonNegative = "Ortak alan oranı negatif olamaz.";
        public const string SerialNumberRequired = "Sayaç seri numarası zorunludur.";
        public const string MeterPrimaryAddressInvalid = "M-Bus birincil adresi 1–250 arasında olmalıdır.";
        public const string MeterSerialAlreadyRegistered = "Bu seri numarasına sahip sayaç zaten kayıtlı.";
        public const string ApartmentNotFoundForDoorNumber = "Girilen kapı numarasıyla eşleşen daire bulunamadı.";
        public const string ValidFromMustBeBeforeValidTo = "ValidFrom değeri ValidTo değerinden önce olmalıdır.";
        public const string InvoiceNumberRequired = "Fatura numarası zorunludur.";
        public const string InvoiceAmountMustBeNonNegative = "Fatura kalem tutarları negatif olamaz.";
        public const string InvoiceTotalAmountMustBePositive = "Fatura toplam tutarı sıfırdan büyük olmalıdır.";
        public const string InvoicePeriodInvalid = "Fatura dönemi geçersiz: bitiş tarihi başlangıç tarihinden önce olamaz.";
        public const string MeterIndexValueMustBeNonNegative = "Sayaç endeks değeri negatif olamaz.";
        public const string MeterConsumptionMustBeValid = "Dönem tüketim değeri geçersiz aralıkta.";
        public const string MeterApartmentRequiredForCalorimeter = "Kalorimetre bir daireye bağlı olmalıdır.";
        public const string MeterIdRequired = "Sayaç kimliği (meterId) zorunludur.";

        /// <summary>Manuel okuma kuralları (Etap 6.6).</summary>
        public const string ManualReadingMeterNotFound = "Sayaç bulunamadı veya aktif değil.";
        public const string ManualReadingMeterNotInApartment = "Belirtilen sayaç bu daireye ait değil.";
        public const string ManualReadingFutureDate = "Okuma tarihi bugünden ileri olamaz.";
        public const string ManualReadingDuplicate = "Bu sayaç için aynı okuma tarihine ait kayıt zaten mevcut.";
        public const string ManualReadingIndexRegression =
            "Endeks gerilemesi: girilen değer, bu sayacın son kayıtlı okumasından küçük olamaz.";

        /// <summary>Excel okuma içe aktarma kuralları (Etap 6.6).</summary>
        public const string ReadingImportNoActiveMeter =
            "Bu daireye bağlı aktif kalorimetre bulunamadı.";

        /// <summary>Etap 6.7: Fatura düzenleme, kat maliki ve okuma düzeltme kuralları.</summary>
        public const string InvoiceLockedForEdit =
            "Dönem kilitli: fatura düzenlenemez. Önce fatura listesinden kilidi açın.";

        public const string OwnerNameRequired = "Kat maliki adı soyadı zorunludur.";

        public const string ReadingNotFound = "Okuma kaydı bulunamadı.";

        public const string ReadingCorrectionReasonRequired =
            "Değişiklik nedeni zorunludur — denetim izi için her düzeltmenin gerekçesi kaydedilir.";

        public const string ReadingCorrectionNoChange =
            "Yeni endeks değeri mevcut değer ile aynı — değişiklik yapılmadı.";

        public const string ReadingCorrectionIndexOutsideChain =
            "Yeni endeks değeri, bu okumanın önceki ve sonraki okumalarıyla uyumsuz (zincir bütünlüğü bozulur).";
    }
}
