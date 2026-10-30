using Westalife.Domain.Common;
using Westalife.Domain.Constants;

namespace Westalife.Domain.Entities;

/// <summary>
/// Bina doğalgaz faturası (Akmercan). KDV ve geçişme bedeli dahil TOPLAM tutar 30/70 dağıtımına girer (Kural: S1).
/// Kalem tutarları raporlama/analiz amaçlı ayrı saklanır; mutabakat kuralı ctor içinde doğrulanır:
/// Consumption + ConsumptionVat + Distribution + DistributionVat == Total (kuruş hassasiyetiyle).
/// </summary>
public class GasInvoice : BaseEntity
{
    public string InvoiceNumber { get; private set; } = string.Empty;

    public string SubscriberNumber { get; private set; } = string.Empty;

    public string MasterMeterSerial { get; private set; } = string.Empty;

    /// <summary>İlk okuma tarihi (fatura dönemi başlangıcı — SCD lookup bu dönem üzerinden yapılır).</summary>
    public DateTimeOffset ReadingDateFrom { get; private set; }

    /// <summary>Son okuma tarihi (fatura dönemi bitişi).</summary>
    public DateTimeOffset ReadingDateTo { get; private set; }

    /// <summary>Fatura düzenleme tarihi (dönemden farklıdır).</summary>
    public DateTimeOffset IssuedOn { get; private set; }

    /// <summary>
    /// Dönem kilidi (Etap 6.6): kilitliyken bu döneme okuma eklenemez, okuma içe aktarılamaz
    /// ve dönem yeniden hesaplanamaz — denetim bütünlüğü (audit trail) garantisi.
    /// </summary>
    public bool IsLocked { get; private set; }

    /// <summary>Kilitleme zamanı (denetim izi). Kilidi açan kullanıcı Faz 7'de TenantId ile saklanacaktır.</summary>
    public DateTimeOffset? LockedOn { get; private set; }

    /// <summary>Düzeltilmiş tüketim (m³).</summary>
    public decimal CorrectedVolumeM3 { get; private set; }

    /// <summary>Tüketilen enerji (kWh) — bina ana sayaç toplamı.</summary>
    public decimal TotalEnergyKWh { get; private set; }

    /// <summary>Basınç düzeltme katsayısı (K).</summary>
    public decimal PressureCorrectionFactor { get; private set; }

    /// <summary>Tüketim bedeli (TL, KDV hariç).</summary>
    public decimal ConsumptionAmount { get; private set; }

    /// <summary>Tüketim bedeli KDV'si (TL).</summary>
    public decimal ConsumptionVat { get; private set; }

    /// <summary>Geçişme/dağıtım bedeli (TL, KDV hariç).</summary>
    public decimal DistributionAmount { get; private set; }

    /// <summary>Geçişme bedeli KDV'si (TL).</summary>
    public decimal DistributionVat { get; private set; }

    /// <summary>Ödenecek bedel (G.Toplam) — 30/70 dağıtımının temel aldığı tutar.</summary>
    public decimal TotalAmount { get; private set; }

    /// <summary>Fatura duyuru/not alanı (Etap 6.7) — PDF "Ek Açıklamalar" kutusuna basılır.</summary>
    public string? Note { get; private set; }

    /// <summary>
    /// Faz 6.8: dairelere yansıtılan faturanın SON ÖDEME TARİHİ (vade). Gecikme faizi bu tarihten itibaren
    /// günlük basit faizle işler (Anapara × Oran/100 × Gün/365).
    /// </summary>
    public DateOnly DueDate { get; private set; }

    /// <summary>Faz 6.8: bina, doğalgaz firmasına bu faturayı ödedi mi? (nakit çıkışı kaynağı)</summary>
    public bool IsPaid { get; private set; }

    /// <summary>Faturanın doğalgaz firmasına ödendiği tarih (nakit akışında gider bu tarihte düşer).</summary>
    public DateOnly? PaidOn { get; private set; }

    private GasInvoice() { } // EF Core

    public GasInvoice(
        string invoiceNumber,
        string subscriberNumber,
        string masterMeterSerial,
        DateTimeOffset readingDateFrom,
        DateTimeOffset readingDateTo,
        DateTimeOffset issuedOn,
        decimal correctedVolumeM3,
        decimal totalEnergyKWh,
        decimal pressureCorrectionFactor,
        decimal consumptionAmount,
        decimal consumptionVat,
        decimal distributionAmount,
        decimal distributionVat,
        decimal totalAmount,
        string? note = null,
        DateOnly? dueDate = null)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.InvoiceNumberRequired, nameof(invoiceNumber));
        }

        if (readingDateTo <= readingDateFrom)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.InvoicePeriodInvalid, nameof(readingDateTo));
        }

        if (consumptionAmount < 0m || consumptionVat < 0m || distributionAmount < 0m || distributionVat < 0m)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.InvoiceAmountMustBeNonNegative, nameof(consumptionAmount));
        }

        // Gerçek Akmercan faturası yapısı: Geçişme Bedeli KDV DAHİL brüt gösterilir (529,46 içinde 105,89),
        // bu yüzden kalem toplamı genel toplamı vermeyebilir. Mutabakatın temeli kullanıcı tarafından
        // girilen Ödenecek Bedel'dir (S1 kararı: "Toplam Ödenecek Tutar girecek, ekstra ayrıştırma yok").
        if (totalAmount <= 0m)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.InvoiceTotalAmountMustBePositive, nameof(totalAmount));
        }

        InvoiceNumber = invoiceNumber.Trim();
        SubscriberNumber = subscriberNumber.Trim();
        MasterMeterSerial = masterMeterSerial.Trim();
        ReadingDateFrom = readingDateFrom;
        ReadingDateTo = readingDateTo;
        IssuedOn = issuedOn;
        CorrectedVolumeM3 = correctedVolumeM3;
        TotalEnergyKWh = totalEnergyKWh;
        PressureCorrectionFactor = pressureCorrectionFactor;
        ConsumptionAmount = consumptionAmount;
        ConsumptionVat = consumptionVat;
        DistributionAmount = distributionAmount;
        DistributionVat = distributionVat;
        TotalAmount = decimal.Round(totalAmount, 2);
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        DueDate = dueDate ?? DateOnly.FromDateTime(issuedOn.DateTime);
    }

    // Saat dilimi kaymasi (UTC/yerel gece yarisi) yuzunden 1 gunluk tolerans: DB'den UTC gelen tarih bir gun geri gorunebilir.
    private DateOnly IssuedDateTolerantFloor() => DateOnly.FromDateTime(IssuedOn.DateTime).AddDays(-1);

    /// <summary>Son ödeme tarihini (vade) ayarlar. Düzenleme tarihinden önce olamaz. Dönem kilidinden bağımsızdır.</summary>
    public void SetDueDate(DateOnly dueDate)
    {
        if (dueDate < IssuedDateTolerantFloor())
        {
            throw new ArgumentException(DomainConstants.GasInvoice.DueDateBeforeIssued, nameof(dueDate));
        }

        DueDate = dueDate;
        LastModifiedOn = DateTimeOffset.UtcNow;
    }

    /// <summary>Faturayı doğalgaz firmasına ödendi olarak işaretler (nakit akışı gideri).</summary>
    public void MarkPaid(DateOnly paidOn)
    {
        if (paidOn < IssuedDateTolerantFloor())
        {
            throw new ArgumentException(DomainConstants.GasInvoice.PaidOnBeforeIssued, nameof(paidOn));
        }

        IsPaid = true;
        PaidOn = paidOn;
        LastModifiedOn = DateTimeOffset.UtcNow;
    }

    /// <summary>Ödendi işaretini geri alır.</summary>
    public void MarkUnpaid()
    {
        IsPaid = false;
        PaidOn = null;
        LastModifiedOn = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Fatura düzenleme (Etap 6.7). Kilitli dönemde REDDEDİLİR (DomainException).
    /// Tüm alanlar geçerlilikten geçirilir; TotalAmount kuruşa yuvarlanır; LastModifiedOn güncellenir.
    /// Hesaplaması olan faturalarda Application katmanı kullanıcıyı "yeniden hesaplama" konusunda uyarır.
    /// </summary>
    public void Update(
        string invoiceNumber,
        string subscriberNumber,
        string masterMeterSerial,
        DateTimeOffset readingDateFrom,
        DateTimeOffset readingDateTo,
        DateTimeOffset issuedOn,
        decimal correctedVolumeM3,
        decimal totalEnergyKWh,
        decimal pressureCorrectionFactor,
        decimal consumptionAmount,
        decimal consumptionVat,
        decimal distributionAmount,
        decimal distributionVat,
        decimal totalAmount,
        string? note)
    {
        if (IsLocked)
        {
            throw new Westalife.Domain.Exceptions.DomainException(DomainConstants.ValidationMessages.InvoiceLockedForEdit);
        }

        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.InvoiceNumberRequired, nameof(invoiceNumber));
        }

        if (readingDateTo <= readingDateFrom)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.InvoicePeriodInvalid, nameof(readingDateTo));
        }

        if (consumptionAmount < 0m || consumptionVat < 0m || distributionAmount < 0m || distributionVat < 0m)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.InvoiceAmountMustBeNonNegative, nameof(consumptionAmount));
        }

        if (totalAmount <= 0m)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.InvoiceTotalAmountMustBePositive, nameof(totalAmount));
        }

        if (note is { Length: > DomainConstants.GasInvoice.NoteMaxLength })
        {
            throw new ArgumentException($"Duyuru en fazla {DomainConstants.GasInvoice.NoteMaxLength} karakter olabilir.", nameof(note));
        }

        InvoiceNumber = invoiceNumber.Trim();
        SubscriberNumber = subscriberNumber.Trim();
        MasterMeterSerial = masterMeterSerial.Trim();
        ReadingDateFrom = readingDateFrom;
        ReadingDateTo = readingDateTo;
        IssuedOn = issuedOn;
        CorrectedVolumeM3 = correctedVolumeM3;
        TotalEnergyKWh = totalEnergyKWh;
        PressureCorrectionFactor = pressureCorrectionFactor;
        ConsumptionAmount = consumptionAmount;
        ConsumptionVat = consumptionVat;
        DistributionAmount = distributionAmount;
        DistributionVat = distributionVat;
        TotalAmount = decimal.Round(totalAmount, 2);
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        LastModifiedOn = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Dönemi kilitler (Etap 6.6). Yalnız hesaplaması olan faturalarda çağrılabilir;
    /// ön koşul kontrolü Application katmanında (LockPeriodCommandHandler) yapılır.
    /// </summary>
    public void Lock()
    {
        IsLocked = true;
        LockedOn = DateTimeOffset.UtcNow;
        LastModifiedOn = DateTimeOffset.UtcNow;
    }

    /// <summary>Dönem kilidini açar. Faz 7'de yetki kontrolü TenantAdmin rolüyle yapılacaktır.</summary>
    public void Unlock()
    {
        IsLocked = false;
        LockedOn = null;
        LastModifiedOn = DateTimeOffset.UtcNow;
    }
}
