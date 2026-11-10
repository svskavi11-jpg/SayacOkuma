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

    public static class Tenant
    {
        public const int NameMaxLength = 200;
        public const int AddressMaxLength = 300;
    }

    public static class Auth
    {
        public const int FullNameMaxLength = 150;
        public const string SuperAdmin = "SuperAdmin";
        public const string TenantAdmin = "TenantAdmin";
        public const string Accountant = "Accountant";
        public const string MeterReader = "MeterReader";
        public const string Resident = "Resident";

        /// <summary>Yönetim paneli rolleri. Sakin bu listeye girmez.</summary>
        public const string StaffRoles = SuperAdmin + "," + TenantAdmin + "," + Accountant + "," + MeterReader;

        /// <summary>Sakin portal hesabı açabilen roller.</summary>
        public const string ResidentManagers = SuperAdmin + "," + TenantAdmin;

        public static readonly Guid SuperAdminRoleId = new("00000000-0000-0000-0000-0000000000a1");
        public static readonly Guid TenantAdminRoleId = new("00000000-0000-0000-0000-0000000000a2");
        public static readonly Guid AccountantRoleId = new("00000000-0000-0000-0000-0000000000a3");
        public static readonly Guid MeterReaderRoleId = new("00000000-0000-0000-0000-0000000000a4");
        public static readonly Guid ResidentRoleId = new("00000000-0000-0000-0000-0000000000a5");
        public const string TenantHeader = "X-Tenant-Id";
        public const string UserIdClaim = "UserId";
        public const string TenantIdClaim = "TenantId";
        public const string RoleClaim = "Role";
        public const string FullNameClaim = "FullName";
    }

    public static class Apartment
    {
        public const int DoorNumberMaxLength = 10;
        public const int TitleMaxLength = 100;
        public const int MinDoorNumber = 1;

        /// <summary>Devir (açılış) bakiyesi: + borçlu, - alacaklı (fazla ödeme). Mutlak üst sınır.</summary>
        public const decimal OpeningBalanceMaxAbs = 10_000_000m;
        public const string OpeningBalanceOutOfRange = "Devir bakiyesi izin verilen aralığın dışında.";
        public const string OpeningBalanceInterestCannotBeNegative = "Geçmiş dönem gecikme faizi negatif olamaz.";
        public const string OpeningBalanceDateRequired = "Devir bakiyesi için bakiye tarihi zorunludur.";

        public const string BalanceDebtor = "Borçlu";
        public const string BalanceCreditor = "Alacaklı";
        public const string BalanceSettled = "Borcu Yok";
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

    /// <summary>Kat maliki/sakin SCD Type 2 kayıt kuralları (Etap 6.7 + Faz 7.1).</summary>
    public static class ApartmentOwner
    {
        public const int OwnerNameMaxLength = 100;
        public const int PhoneMaxLength = 20;
        public const int EmailMaxLength = 100;
        public const int IdentityNumberLength = 11;
        public const int NotesMaxLength = 500;
        public const string PeriodOverlap = "Bu rolde girilen tarihler mevcut bir sakin kaydıyla çakışıyor.";
        public const string StartMustFollowCurrent = "Yeni başlangıç, mevcut sakinin girişinden sonra olmalıdır.";
        public const string LeaseEndBeforeStart = "Sözleşme bitişi başlangıçtan önce olamaz.";
        public const string OpeningBalanceOwnerNotFound = "Seçilen sakin bu daireye ait değil.";
    }

    public static class ResidentDocument
    {
        public const int FileNameMaxLength = 200;
        public const int FilePathMaxLength = 300;
        public const int MaxBytes = 10 * 1024 * 1024;
        public const string FileRequired = "Evrak dosyası zorunludur.";
        public const string FileTooLarge = "Evrak en fazla 10 MB olabilir.";
        public const string FileTypeInvalid = "Evrak yalnızca PDF, JPG veya PNG olabilir.";
        public const string NotFound = "Evrak bulunamadı.";
        public const string OwnerNotFound = "Sakin kaydı bulunamadı.";
    }

    /// <summary>Okuma düzeltme denetim izi kuralları (Etap 6.7).</summary>
    public static class MeterReadingAuditLog
    {
        public const int ReasonMaxLength = 500;
        public const int ChangedByMaxLength = 100;

        /// <summary>Faz 7'ye kadar (JWT yok) düzeltmeyi yapan kullanıcı boş bırakılırsa kullanılır.</summary>
        public const string DefaultChangedBy = "Yönetici";
    }

    /// <summary>Aidat tarifesı kuralları (Faz 6.9).</summary>
    public static class AidatTariff
    {
        public const int NameMaxLength = 100;

        /// <summary>Tarife m² değeri üst sınırı (daire m² üst sınırı ile uyumlu).</summary>
        public const decimal SquareMetersMax = 1000m;

        /// <summary>Tek tarife aylık tutarı için mutlak üst sınır (TL).</summary>
        public const decimal AmountMax = 1_000_000m;

        public const int SquareMetersPrecision = 9;
        public const int SquareMetersScale = 2;
    }

    /// <summary>Aidat tahakkuk kuralları (Faz 6.9, özel toplama fonu).</summary>
    public static class AidatInvoice
    {
        public const int NoteMaxLength = 500;
        public const int TitleMaxLength = 120;

        /// <summary>Tahakkuk sonucunda atlanan daireler için rapor gerekçeleri.</summary>
        public const string SkipReasonNoTariff = "Aidat tarifesı atanmamış.";
        public const string SkipReasonNoDimension = "Aktif m² kaydı bulunamadı.";

        public const string FundTitleRequired = "Özel toplama için fon adı zorunludur.";
        public const string FundTitleTooLong = "Fon adı en fazla 120 karakter olabilir.";
        public const string FundTargetRequired = "Özel toplama için hedef tutar sıfırdan büyük olmalıdır.";

        public const string InvoiceNotFound = "Aidat tahakkuku bulunamadı.";
        public const string AlreadyCancelled = "Bu aidat tahakkuku zaten iptal edilmiş.";
        public const string CancelBlockedByLockedPeriod = "İlgili bütçe dönemi kilitli olduğu için aidat tahakkuku iptal edilemez.";
        public const string CancelBlockedByPayments = "Bu tahakkuka ait tahsilat bulunduğu için iptal edilemez. Önce ilgili tahsilatları iptal ediniz.";
        public const string CancelBlockedByExpenses = "Bu özel toplama fonuna bağlı gider kaydı bulunduğu için tahakkuk iptal edilemez.";
    }

    public static class GasInvoice
    {
        public const int InvoiceNumberMaxLength = 50;
        public const int SubscriberNumberMaxLength = 20;
        public const int MeterSerialMaxLength = 20;
        public const int NoteMaxLength = 1000;

        /// <summary>Faz 6.8: son ödeme tarihi / ödendi (nakit çıkışı) kuralları.</summary>
        public const string PaidOnRequired = "Ödendi olarak işaretlemek için ödeme tarihi zorunludur.";
        public const string PaidOnBeforeIssued = "Ödeme tarihi fatura düzenleme tarihinden önce olamaz.";
        public const string DueDateBeforeIssued = "Son ödeme tarihi fatura düzenleme tarihinden önce olamaz.";
        public const string DueDateLocked = "Kilitli dönemin son ödeme tarihi değiştirilemez. Kilidi açıp yeniden kilitleyin.";
        public const string DueDateBeforeNotice = "Son ödeme tarihi bildirim tarihinden önce olamaz.";
        public const string NoticeDateBeforeIssued = "Bildirim tarihi fatura düzenleme tarihinden önce olamaz.";

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

        /// <summary>Tek bina sürümündeki mevcut verinin bağlandığı ilk site.</summary>
        public static readonly Guid DefaultTenantId = new("00000000-0000-0000-0000-00000000aa02");

        public const string DefaultTenantName = "Westalife Residence";

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

    /// <summary>
    /// Faz 6.8: Daire tahsilat (ödeme) kuralları.
    /// </summary>
    public static class Payment
    {
        public const int DescriptionMaxLength = 500;
        public const int ReceivedByMaxLength = 100;
        public const int DocumentNumberMaxLength = 100;

        public const decimal AmountMax = 10_000_000m;
        public const string AmountMustBePositive = "Tahsilat tutarı sıfırdan büyük olmalıdır.";
        public const string AmountTooLarge = "Tahsilat tutarı izin verilen üst sınırı aşıyor.";
        public const string PaymentDateFuture = "Tahsilat tarihi bugünden ileri olamaz.";
        public const string AlreadyCancelled = "Bu tahsilat zaten iptal edilmiş.";
        public const string CancelledCannotBeChanged = "İptal edilmiş tahsilat değiştirilemez.";
        public const string NotFound = "Tahsilat kaydı bulunamadı.";
        public const string CancellationReasonRequired = "İptal gerekçesi zorunludur.";
        public const int CancellationReasonMaxLength = 500;
        public const string DocumentNumberTooLong = "Dekont numarası en fazla 100 karakter olabilir.";
        public const string DocumentNumberDuplicate = "Bu dekont numarasına ait aktif bir tahsilat kaydı zaten mevcut.";

        /// <summary>Faz 7'ye kadar (JWT yok) tahsilatı giren kullanıcı boş bırakılırsa kullanılır.</summary>
        public const string DefaultReceivedBy = "Yönetici";
    }

    /// <summary>Banka hesap kartı. Tahsilat ve gider bu karta bağlanır; bağ boş kalabilir.</summary>
    public static class BankAccount
    {
        public const int BankNameMaxLength = 100;
        public const int AccountNameMaxLength = 100;
        public const int AccountNumberMaxLength = 34;
        public const int IbanLength = 26;
        public const decimal AmountMax = 10_000_000m;

        public const string BankNameRequired = "Banka adı zorunludur.";
        public const string BankNameTooLong = "Banka adı en fazla 100 karakter olabilir.";
        public const string AccountNameRequired = "Hesap adı zorunludur.";
        public const string AccountNameTooLong = "Hesap adı en fazla 100 karakter olabilir.";
        public const string AccountNumberRequired = "Hesap numarası zorunludur.";
        public const string AccountNumberTooLong = "Hesap numarası en fazla 34 karakter olabilir.";
        public const string AccountNumberDuplicate = "Bu hesap numarası zaten kayıtlı.";
        public const string IbanInvalid = "IBAN, TR ile başlayan 26 haneli geçerli bir numara olmalıdır.";
        public const string IbanDuplicate = "Bu IBAN zaten kayıtlı.";
        public const string OpeningBalanceInvalid = "Devir bakiyesi negatif olamaz ve izin verilen üst sınırı aşamaz.";
        public const string OpeningDateFuture = "Devir tarihi bugünden ileri olamaz.";
        public const string NotFound = "Banka hesabı bulunamadı.";
        public const string Inactive = "Pasif banka hesabı yeni kayda bağlanamaz.";
        public const string HasMovements = "Bu hesaba bağlı hareket var. Hesap silinemez; pasife alınabilir.";
    }

    public static class BankTransaction
    {
        public const int DescriptionMaxLength = 200;

        public const string AmountInvalid = "Tutar sıfırdan büyük olmalı ve izin verilen üst sınırı aşmamalıdır.";
        public const string DateFuture = "Hareket tarihi bugünden ileri olamaz.";
        public const string DateBeforeOpening = "Hareket tarihi hesabın devir tarihinden önce olamaz.";
        public const string DescriptionTooLong = "Açıklama en fazla 200 karakter olabilir.";
        public const string TypeInvalid = "Faiz veya masraf seçilmelidir.";
        public const string SameAccount = "Virman aynı hesaba yapılamaz.";
        public const string NotFound = "Banka hareketi bulunamadı.";
    }

    /// <summary>Görev ataması (yönetici / denetçi) ve aidat muafiyeti.</summary>
    public static class RoleAssignment
    {
        public const int PersonNameMaxLength = 150;
        public const string ManagementExpenseCategory = "Yönetim gideri";

        public const string PersonNameRequired = "Görevli adı zorunludur.";
        public const string PersonNameTooLong = "Görevli adı en fazla 150 karakter olabilir.";
        public const string RateInvalid = "Muafiyet oranı 0 ile 1 arasında olmalıdır.";
        public const string DateInvalid = "Görev bitişi başlangıçtan önce olamaz.";
        public const string Overlap = "Bu dairede görev tarihleri çakışıyor.";
        public const string NotFound = "Görev kaydı bulunamadı.";
        public const string AlreadyClosed = "Görev zaten kapatılmış.";
        public const string AlreadyCancelled = "Bu görev ataması zaten iptal edilmiş.";
        public const string CancelBlockedByLockedPeriod = "İlgili bütçe dönemi kilitli olduğu için görev ataması ve mahsupları iptal edilemez.";
        public const string ApartmentNotFound = "Daire bulunamadı.";
        public const string NotCollectionTarget = "Görev mahsubu tahsilat hedefi olamaz.";
        public const string PaymentTypeInvalid = "Geçersiz görev ücreti türü.";
        public const string FixedAmountInvalid = "Sabit görev ücreti sıfırdan büyük olmalıdır.";
        public const string PeriodTypeRequired = "Sabit tutarlı görevlerde periyot (Aylık / Yıllık) seçilmelidir.";
    }

    public static class WaiverOffset
    {
        public const int DescriptionMaxLength = 200;
        public const string ManagementExpenseMissing = "Mahsup için bu yılın Yönetim gideri bütçe kalemi bulunamadı. Önce kalemi açın.";
        public const string ManagementExpenseDuplicate = "Bu yıl için Yönetim gideri bütçe kalemi bir tane olmalıdır.";
        public const string ManagementBudgetItemNotConfigured = "Lütfen Ayarlar sayfasından Görev Mahsup Bütçe Kalemini seçin.";
    }

    public static class AuditReport
    {
        public const string AidatCollection = "Aidat tahsilatı";
        public const string FuelCollection = "Yakıt tahsilatı";
        public const string OpeningCollection = "Devir tahsilatı";
        public const string UnallocatedCollection = "Bağlanmamış tahsilat";
        public const string UnbudgetedExpense = "Bütçe kalemi yok";
        public const string AccountOpening = "Hesap devri";
        public const string BankNotFound = "Banka hesabı bulunamadı.";
        public const string PeriodInvalid = "Bitiş tarihi başlangıçtan önce olamaz.";
    }

    /// <summary>
    /// Faz 6.8: Ödeme-Borç eşleştirme (allocation) kuralları.
    /// </summary>
    public static class PaymentAllocation
    {
        public const string AmountMustBePositive = "Eşleştirme tutarı sıfırdan büyük olmalıdır.";
        public const string AllocationExceedsPayment = "Eşleştirme toplamı tahsilat tutarını aşamaz.";
        public const string AllocationExceedsDebt = "Eşleştirme tutarı borçtan fazla olamaz.";
    }

    public static class AppSettings
    {
        public const int CompanyNameMaxLength = 100;
        public const string CompanyNameRequired = "Şirket adı zorunludur.";
        public const string ThemeTeal = "teal";
        public const string ThemeBlue = "blue";
        public const string ThemeDark = "dark";
        public const string ThemeInvalid = "Tema turkuaz, mavi veya karanlık olmalıdır.";
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

        /// <summary>Faz 6.8: gecikme faizi ayarları. Faiz basit faizdir, günlük işletilir (Anapara × Oran/100 × Gün/365).</summary>
        public const string InterestPeriodDaily = "Daily";
        public const decimal InterestRateMax = 500m;
        public const string InterestRateInvalid = "Yıllık faiz oranı 0 ile 500 arasında olmalıdır.";
        public const string InterestPeriodInvalid = "Faiz dönemi yalnızca 'Daily' (günlük) olabilir.";
    }

    /// <summary>Faz 6.10: Bütçe (işletme projesi) ve gider takibi.</summary>
    public static class Budget
    {
        public const int CategoryMaxLength = 100;
        public const int ExpenseTypeMaxLength = 100;
        public const int DescriptionMaxLength = 500;
        public const int DocumentNumberMaxLength = 50;
        public const int TransferNoteMaxLength = 500;

        public const decimal AmountMax = 10_000_000m;

        public const int YearMin = 2000;
        public const int YearMax = 2100;

        /// <summary>Harcama, tahmini tutarın bu oranına ulaşınca uyarı verilir. Kayıt engellenmez.</summary>
        public const decimal WarningRatio = 0.90m;

        public const string IncomeLabel = "Gelir";
        public const string ExpenseLabel = "Gider";
        public const string TotalLabel = "Toplam";
        public const string CollectionTitle = "Tahsilat";
        public const string EstimatedIncomeLabel = "Tahmini Gelir";
        public const string EstimatedExpenseLabel = "Tahmini Gider";
        public const string BalanceLabel = "Denge";
        public const string TransferLabel = "Virman";

        public const string CategoryRequired = "Bütçe kategorisi zorunludur.";
        public const string CategoryTooLong = "Bütçe kategorisi en fazla 100 karakter olabilir.";
        public const string CategoryAlreadyExists = "Bu yıl için aynı kategoride bir bütçe kalemi zaten kayıtlı.";
        public const string EstimatedAmountInvalid = "Tahmini bütçe tutarı sıfırdan büyük ve izin verilen üst sınırın altında olmalıdır.";
        public const string YearInvalid = "Yıl 2000 ile 2100 arasında olmalıdır.";
        public const string ExpenseAmountInvalid = "Gider tutarı sıfırdan büyük ve izin verilen üst sınırın altında olmalıdır.";
        public const string ExpenseDateFuture = "Gider tarihi bugünden ileri olamaz.";
        public const string ExpenseTypeRequired = "Gider türü zorunludur.";
        public const string ExpenseTypeTooLong = "Gider türü en fazla 100 karakter olabilir.";
        public const string ExpenseTypeNotAllowedWithItem = "Bütçe kalemine bağlı giderde serbest gider türü yazılamaz.";
        public const string DescriptionTooLong = "Gider açıklaması en fazla 500 karakter olabilir.";
        public const string BudgetItemRequired = "Bütçe takibi açıkken gider bir bütçe kalemine bağlanmalıdır.";
        public const string BudgetItemNotAllowed = "Bütçe takibi kapalıyken bütçe kalemi seçilemez. Gider türünü yazın.";
        public const string BudgetItemYearMismatch = "Gider tarihi ile bütçe kaleminin yılı aynı olmalıdır.";
        public const string BudgetItemNotFound = "Bütçe kalemi bulunamadı.";
        public const string ExpenseNotFound = "Gider kaydı bulunamadı.";
        public const string BudgetTrackingDisabled = "Bütçe takibi kapalı. Bütçe kalemi bu durumda eklenemez, değiştirilemez veya raporlanamaz.";
        public const string BudgetItemHasExpenses = "Bu kaleme bağlı giderler olduğu için kalem silinemez.";
        public const string BudgetItemHasTransfers = "Bu kaleme bağlı virman olduğu için kalem silinemez.";
        public const string WarningReachedFormat = "{0} kaleminde harcama, tahmini bütçenin %{1:N2} düzeyine ulaştı veya aştı.";
        public const string DocumentNumberTooLong = "Belge numarası en fazla 50 karakter olabilir.";
        public const string YearLocked = "Bu bütçe yılı kilitli. Yeni kayıt eklenemez; kilidi açın.";
        public const string TransferSameItem = "Virman aynı kalem içinde yapılamaz.";
        public const string TransferYearMismatch = "Virman kalemleri aynı yıla ait olmalıdır.";
        public const string TransferDateYearMismatch = "Virman tarihi, kalemlerin yılı ile aynı olmalıdır.";
        public const string TransferExceedsAvailable = "Virman tutarı kaynak kalemin kalan bütçesini aşamaz.";
        public const string TransferNotFound = "Virman kaydı bulunamadı.";
        public const string IncomeCategoryAlreadyExists = "Bu yıl için aynı gelir kalemi zaten kayıtlı.";
        public const string IncomeItemNotFound = "Gelir kalemi bulunamadı.";
        public const string FundNotFound = "Özel toplama fonu bulunamadı.";
        public const string FundNotSpecial = "Gider yalnızca özel toplama fonuna bağlanabilir.";
    }

    public static class ValidationMessages
    {
        public const string TenantNameRequired = "Site adı zorunludur.";
        public const string TenantNameTooLong = "Site adı en fazla 200 karakter olabilir.";
        public const string TenantAddressTooLong = "Adres en fazla 300 karakter olabilir.";
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

        /// <summary>Faz 7.1: Sakin iletişim doğrulama kuralları.</summary>
        public const string SakinPhoneInvalid = "Telefon en fazla 20 karakter olabilir; yalnızca rakam ve ' ()+-' karakterleri kullanılabilir.";
        public const string SakinEmailInvalid = "Geçersiz e-posta adresi.";
        public const string SakinIdentityNumberInvalid = "TCKN girildiyse 11 haneli ve yalnızca rakamlardan oluşmalıdır.";
        public const string SakinRucuWarning = "Kiracı borcunu ödemeden ayrıldı — bu borç için ev sahibine de rücu edilebilir (KMK m.22: müteselsil sorumluluk).";

        public const string ReadingNotFound = "Okuma kaydı bulunamadı.";

        public const string ReadingCorrectionReasonRequired =
            "Değişiklik nedeni zorunludur — denetim izi için her düzeltmenin gerekçesi kaydedilir.";

        public const string ReadingCorrectionNoChange =
            "Yeni endeks değeri mevcut değer ile aynı — değişiklik yapılmadı.";

        public const string ReadingCorrectionIndexOutsideChain =
            "Yeni endeks değeri, bu okumanın önceki ve sonraki okumalarıyla uyumsuz (zincir bütünlüğü bozulur).";

        /// <summary>Faz 6.9: Aidat (ortak gider) kuralları.</summary>
        public const string AidatTariffNameRequired = "Aidat tarife adı zorunludur.";
        public const string AidatTariffSquareMetersInvalid = "Aidat tarife m² değeri 0'dan büyük ve geçerli aralıkta olmalıdır.";
        public const string AidatTariffAmountMustBePositive = "Aidat tarife tutarı 0'dan büyük ve geçerli aralıkta olmalıdır.";
        public const string AidatPeriodInvalid = "Aidat dönemi geçersiz: dönem bitişi, dönem başlangıcından önce olamaz.";
        public const string AidatInvoiceIdRequired = "Aidat tahakkuk kimliği (AidatInvoiceId) zorunludur.";
        public const string AidatChargeApartmentRequired = "Aidat borcu için daire kimliği (ApartmentId) zorunludur.";
        public const string AidatChargeAmountMustBePositive = "Daire aidat borcu 0'dan büyük olmalıdır.";
        public const string AidatAllocationChargeIdRequired = "Aidat allocation için AidatChargeId zorunludur.";
        public const string AidatInvoiceNoCharges = "Aidat tahakkuku en az bir daire borcu içermelidir.";
        public const string AidatTariffNameAlreadyExists = "Bu ada sahip bir aidat tarifesı zaten kayıtlı.";
        public const string AidatPeriodOverlap = "Aidat dönemleri çakışıyor: seçilen dönem, mevcut bir aidat tahakkuku dönemine denk geliyor.";
        public const string AidatEqualAmountRequired = "Eşit dağıtımda daire başına tutar (EqualAmount) sıfırdan büyük olmalıdır.";
        public const string AidatTariffNotActive = "Pasif durumdaki tarife daireye atanamaz — önce tarifeyi aktif edin.";
    }

    /// <summary>Çok kanallı bildirim (e-posta, SMS, WhatsApp).</summary>
    public static class Notification
    {
        public const int NameMaxLength = 100;
        public const int SubjectMaxLength = 200;
        public const int BodyMaxLength = 4000;
        public const int AddressMaxLength = 200;
        public const int ErrorMaxLength = 1000;
        public const int SecretMaxLength = 500;
        public const int HostMaxLength = 200;
        public const int HeaderMaxLength = 20;
        public const int ProviderMaxLength = 40;
        public const int PhoneNumberIdMaxLength = 40;
        public const int SmtpPortDefault = 587;

        public const string NameRequired = "Şablon adı zorunludur.";
        public const string BodyRequired = "Mesaj metni zorunludur.";
        public const string SubjectTooLong = "E-posta konusu en fazla 200 karakter olabilir.";
        public const string ChannelInvalid = "Bildirim kanalı geçersiz.";
        public const string StatusInvalid = "Bildirim durumu geçersiz.";
        public const string ApartmentsRequired = "En az bir daire seçilmelidir.";
        public const string TemplateOrBodyRequired = "Şablon seçin veya mesaj metni yazın.";
        public const string SmtpPortInvalid = "SMTP portu 1 ile 65535 arasında olmalıdır.";
        public const string RecipientMissing = "Alıcı adresi yok.";
        public const string ApartmentNotFound = "Daire bulunamadı.";
        public const string TemplateNotFound = "Bildirim şablonu bulunamadı.";
        public const string TemplateNameExists = "Bu kanalda aynı adlı bir şablon zaten var.";
        public const string TemplateChannelMismatch = "Seçilen şablon bu kanala ait değil.";
        public const string SettingsNotFound = "Sistem ayarları bulunamadı.";
        public const string SmsProviderNetgsm = "Netgsm";
        public const string DefaultEmailSubject = "Westalife Bildirim";
        public const string SimulatedLog = "Simüle edildi";

        public const string DefaultBody =
            "Sayın [AdSoyad], [DaireNo] numaralı dairenizin [Donem] dönemi bakiyesi [Tutar] tutarındadır. Westalife Residence yönetimi.";

        public const string DefaultAidatBody =
            "Sayın [AdSoyad], [DaireNo] numaralı dairenizin kalan aidat borcu [AidatBorc] tutarındadır. Bu dönem aidatı: [BuDonemAidat]. Westalife Residence yönetimi.";

        public const string DefaultHeatingBody =
            "Sayın [AdSoyad], [DaireNo] numaralı dairenizin kalan ısınma borcu [IsinmaBorc] tutarındadır. Bu dönem ısınma: [BuDonemIsinma]. Westalife Residence yönetimi.";

        public const string DefaultAllDebtorsBody =
            "Sayın [AdSoyad], [DaireNo] numaralı dairenizin toplam borcu [Tutar] tutarındadır (Aidat: [AidatBorc], Isınma: [IsinmaBorc]). Westalife Residence yönetimi.";

        public const string DefaultHeatingWithPdfBody =
            "Sayın [AdSoyad], [DaireNo] numaralı dairenizin [MevcutAy] ısınma bildirimi hazır. Kalan ısınma borcu: [IsinmaBorc]. Faturanız: [PdfLink] Westalife Residence yönetimi.";

        public const string PurposeInvalid = "Bildirim türü geçersiz.";
        public const string NoReadingForPeriod = "Bu döneme ait okuma verisi yok, bildirim gönderilemez.";
        public const string PdfTokenInvalid = "PDF erişim bağlantısı geçersiz veya süresi dolmuş.";
        public const string PdfInvoiceRequired = "PDF eklemek için gaz faturası seçilmelidir.";
        public const int PublicBaseUrlMaxLength = 300;
        public const int PdfTokenDays = 30;
    }
}
