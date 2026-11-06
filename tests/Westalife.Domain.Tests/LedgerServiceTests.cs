using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;
using Westalife.Domain.Services.Ledger;
using Xunit;

namespace Westalife.Domain.Tests;

/// <summary>
/// Cari hesap (Ledger) motoru testleri.
/// <para>
/// Faz 7.3 (Kronolojik Faiz): Faiz, ödeme tarihlerine göre parçalara bölünmüş (piecewise)
/// basit faiz olarak hesaplanır. Her ödeme kendi tarihinde anaparayı düşürür; faiz
/// yalnızca o tarihe kadar işler. Aynı gün borç+tahsilat → faiz = 0.
/// </para>
/// </summary>
public class LedgerServiceTests
{
    private static readonly Guid BuildingId = Guid.NewGuid();
    private static readonly DateOnly DevirTarihi = new(2026, 1, 1);

    private static Apartment CreateApartment(decimal principal, decimal interest, DateOnly balanceDate)
    {
        var apartment = new Apartment(BuildingId, "64", "Daire 64");
        apartment.SetOpeningBalance(principal, interest, balanceDate);
        return apartment;
    }

    private static ApartmentInvoice CreateInvoice(decimal totalAmount, DateTimeOffset calculatedOn)
    {
        var totalKurus = (long)(totalAmount * 100m);
        return new ApartmentInvoice(
            Guid.NewGuid(),
            Guid.NewGuid(),
            100m,
            500m,
            totalKurus / 2,
            totalKurus - (totalKurus / 2),
            false,
            calculatedOn);
    }

    private static Payment CreatePayment(DateOnly date, decimal amount) =>
        new(Guid.NewGuid(), date, amount, "test", "Ops");

    [Fact]
    public void Calculate_SplitsOpeningBalanceIntoTwoDebtRows()
    {
        var apartment = CreateApartment(1000m, 200m, DevirTarihi);

        var result = LedgerService.Calculate(
            apartment, invoices: [], payments: [], annualInterestRate: 0m,
            asOfDate: new DateOnly(2026, 3, 1));

        Assert.Equal(2, result.Lines.Count);

        // FIFO ilk satır: Devir FAİZ (kapalı borç), ikinci satır: Devir ANAPARA.
        var interestLine = result.Lines[0];
        var principalLine = result.Lines[1];

        Assert.Equal(LedgerDebtType.OpeningBalanceInterest, interestLine.Type);
        Assert.Equal("Devreden Geçmiş Faiz", interestLine.Label);
        Assert.Equal(200m, interestLine.PrincipalAmount);
        Assert.Equal($"apt:{apartment.Id}:interest", interestLine.SourceId);

        Assert.Equal(LedgerDebtType.OpeningBalancePrincipal, principalLine.Type);
        Assert.Equal("Devir Anapara", principalLine.Label);
        Assert.Equal(1000m, principalLine.PrincipalAmount);
        Assert.Equal($"apt:{apartment.Id}:principal", principalLine.SourceId);

        // Toplam bakiye = anapara + geçmiş faiz.
        Assert.Equal(1200m, result.TotalBalance);
    }

    [Fact]
    public void Calculate_InterestAccruesOnlyOnPrincipal_NotOnCarriedInterest()
    {
        var apartment = CreateApartment(1000m, 200m, DevirTarihi);
        // 2026-01-01 → 2026-02-01 = 31 gün; faiz = 1000 × %12 × 31/365 = 10,19.
        var asOf = new DateOnly(2026, 2, 1);

        var result = LedgerService.Calculate(
            apartment, invoices: [], payments: [], annualInterestRate: 12m, asOfDate: asOf);

        var interestLine = result.Lines.Single(l => l.Type == LedgerDebtType.OpeningBalanceInterest);
        var principalLine = result.Lines.Single(l => l.Type == LedgerDebtType.OpeningBalancePrincipal);

        // Geçmiş faize tekrar faiz İŞLEMEZ.
        Assert.Equal(0m, interestLine.InterestAccrued);

        // Faiz yalnızca anapara kalanından: 1000 × %12 × 31/365 = 10,19 TL.
        Assert.Equal(10.19m, principalLine.InterestAccrued);
    }

    [Fact]
    public void Calculate_FifoPayment_ClosesCarriedInterestFirst_ThenPrincipal()
    {
        var apartment = CreateApartment(1000m, 200m, DevirTarihi);

        // 700 TL tahsilat: 200 TL Devir Faiz'i kapatır + 500 TL Devir Anapara'dan düşer.
        var payment = CreatePayment(new DateOnly(2026, 2, 1), 700m);

        var result = LedgerService.Calculate(
            apartment, invoices: [], payments: [payment], annualInterestRate: 0m,
            asOfDate: new DateOnly(2026, 3, 1));

        var interestLine = result.Lines.Single(l => l.Type == LedgerDebtType.OpeningBalanceInterest);
        var principalLine = result.Lines.Single(l => l.Type == LedgerDebtType.OpeningBalancePrincipal);

        Assert.Equal(200m, interestLine.PaidAmount);
        Assert.Equal(0m, interestLine.RemainingPrincipal);

        Assert.Equal(500m, principalLine.PaidAmount);
        Assert.Equal(500m, principalLine.RemainingPrincipal);

        // Tahsis hedefleri doğru enum değerlerine yazılır.
        Assert.Equal(AllocationTarget.Interest, interestLine.Allocations.Single().Target);
        Assert.Equal(AllocationTarget.OpeningBalance, principalLine.Allocations.Single().Target);

        Assert.Equal(500m, result.TotalBalance);
    }

    /// <summary>
    /// Faz 7.3 — Kronolojik Faiz: Ödeme vadeden 31 gün sonra yapılmış.
    /// 500 TL ödeme 2026-02-01'de yapılıyor. asOf = 2026-03-01.
    /// Borç: 1000 TL, DueDate: 2026-01-01.
    ///
    /// Beklenen:
    /// - Dönem 1: 2026-01-01 → 2026-02-01 (31 gün) → 1000 × %12 × 31/365 = 10,19
    /// - Ödeme: 500 TL düşer, kalan 500 TL
    /// - Dönem 2: 2026-02-01 → 2026-03-01 (28 gün) → 500 × %12 × 28/365 = 4,60
    /// - Toplam faiz: 10,19 + 4,60 = 14,79
    /// </summary>
    [Fact]
    public void Calculate_PiecewiseInterest_PartialPayment_CorrectlyAccrues()
    {
        var apartment = CreateApartment(1000m, 0m, DevirTarihi);
        var payment = CreatePayment(new DateOnly(2026, 2, 1), 500m);
        var asOf = new DateOnly(2026, 3, 1);

        var result = LedgerService.Calculate(
            apartment, invoices: [], payments: [payment], annualInterestRate: 12m, asOfDate: asOf);

        var principalLine = result.Lines.Single(l => l.Type == LedgerDebtType.OpeningBalancePrincipal);

        // Kalan anapara: 500 TL
        Assert.Equal(500m, principalLine.RemainingPrincipal);

        // Dönem 1: 1000 × 12% × 31/365 = 10.19
        // Dönem 2: 500 × 12% × 28/365 = 4.60
        // Toplam: 14.79
        Assert.Equal(14.79m, principalLine.InterestAccrued);
    }

    /// <summary>
    /// Faz 7.3 — Aynı Gün Kuralı: Borç tarihi ile tahsilat tarihi aynı olduğunda faiz 0 olmalı.
    /// DueDate: 2026-09-15, Ödeme: 2026-09-15 (aynı gün), asOf: 2026-11-03.
    /// Borç tamamen ödendiyse faiz = 0.
    /// </summary>
    [Fact]
    public void Calculate_SameDayPayment_ZeroInterest()
    {
        var devirDate = new DateOnly(2026, 9, 15);
        var apartment = CreateApartment(3258.11m, 0m, devirDate);
        var payment = CreatePayment(new DateOnly(2026, 9, 15), 3258.11m);
        var asOf = new DateOnly(2026, 11, 3);

        var result = LedgerService.Calculate(
            apartment, invoices: [], payments: [payment], annualInterestRate: 24m, asOfDate: asOf);

        var principalLine = result.Lines.Single(l => l.Type == LedgerDebtType.OpeningBalancePrincipal);

        // Borç tamamen ödendi, kalan anapara = 0.
        Assert.Equal(0m, principalLine.RemainingPrincipal);

        // Aynı gün borç + tahsilat → faiz = 0.
        Assert.Equal(0m, principalLine.InterestAccrued);
    }

    /// <summary>
    /// Faz 7.3 — Kronolojik Faiz: Ödeme vadeden ÖNCE yapıldığında vade tarihinden itibaren sayılır.
    /// DueDate: 2026-01-15, Ödeme: 2026-01-01 (vadeden önce), asOf: 2026-02-15.
    /// Ödeme vadeden önce olduğu için vade tarihinde sayılır → faiz dueDate'ten asOf'a kadar.
    /// Ödeme = tam ödeme → kalan 0 → faiz 0 (çünkü ödeme dueDate'e çekildi, dueDate ≥ asOf değil
    /// ama ödeme normalize edildiğinde dueDate → faiz hesabı "dueDate → dueDate" = 0 gün).
    /// </summary>
    [Fact]
    public void Calculate_PaymentBeforeDueDate_NormalizedToDueDate_ZeroInterest()
    {
        var devirDate = new DateOnly(2026, 1, 15);
        var apartment = CreateApartment(1000m, 0m, devirDate);
        // Ödeme vadeden 14 gün önce yapılmış — vadede normalize edilmeli
        var payment = CreatePayment(new DateOnly(2026, 1, 1), 1000m);
        var asOf = new DateOnly(2026, 2, 15);

        var result = LedgerService.Calculate(
            apartment, invoices: [], payments: [payment], annualInterestRate: 12m, asOfDate: asOf);

        var principalLine = result.Lines.Single(l => l.Type == LedgerDebtType.OpeningBalancePrincipal);

        // Ödeme dueDate'e normalize edildiğinde kalan = 0 → faiz = 0.
        Assert.Equal(0m, principalLine.RemainingPrincipal);
        Assert.Equal(0m, principalLine.InterestAccrued);
    }

    /// <summary>
    /// Faz 7.3 — Kısmi ödeme + faiz doğru işliyor:
    /// Eski Calculate_PartialPayment_LeavesPrincipalForInterestCalculation testi güncellendi.
    /// Borç: 1000 TL, DueDate: 2026-01-01.
    /// Ödeme: 500 TL, tarih: 2026-02-01.
    /// asOf: 2026-02-01 (ödeme günü).
    ///
    /// Yeni davranış: Ödeme asOfDate ile AYNI GÜN.
    /// - Dönem: 2026-01-01 → 2026-02-01 = 31 gün, anapara 1000 TL (ödeme asOfDate'te = break)
    /// - Faiz = 1000 × 12% × 31/365 = 10.19
    /// (Aynı gün ödeme olduğu için o ödeme sonraki döneme katılmaz; zaten asOfDate.)
    /// </summary>
    [Fact]
    public void Calculate_PartialPayment_SameDayAsOf_InterestOnFullPrincipal()
    {
        var apartment = CreateApartment(1000m, 0m, DevirTarihi);
        var payment = CreatePayment(new DateOnly(2026, 2, 1), 500m);
        var asOf = new DateOnly(2026, 2, 1);

        var result = LedgerService.Calculate(
            apartment, invoices: [], payments: [payment], annualInterestRate: 12m, asOfDate: asOf);

        var principalLine = result.Lines.Single(l => l.Type == LedgerDebtType.OpeningBalancePrincipal);
        Assert.Equal(500m, principalLine.RemainingPrincipal);
        // asOf günü yapılan ödeme break'e takılır; faiz 1000 TL × 31 gün.
        Assert.Equal(10.19m, principalLine.InterestAccrued);
    }

    [Fact]
    public void Calculate_WithoutOpeningBalance_ProducesNoDevirRows()
    {
        var apartment = CreateApartment(0m, 0m, DevirTarihi); // 0/0 → devir temizlenir (Date null)

        var result = LedgerService.Calculate(
            apartment, invoices: [], payments: [], annualInterestRate: 12m,
            asOfDate: new DateOnly(2026, 3, 1));

        Assert.Empty(result.Lines);
        Assert.Equal(0m, result.TotalBalance);
    }

    [Fact]
    public void SetOpeningBalance_BothZero_ClearsBalanceDate()
    {
        var apartment = CreateApartment(1000m, 200m, DevirTarihi);
        apartment.SetOpeningBalance(0m, 0m, null);

        Assert.Equal(0m, apartment.OpeningBalancePrincipal);
        Assert.Equal(0m, apartment.OpeningBalanceInterest);
        Assert.Null(apartment.OpeningBalanceDate);
    }

    [Fact]
    public void SetOpeningBalance_NonZeroWithoutDate_Throws()
    {
        var apartment = new Apartment(BuildingId, "64");

        Assert.Throws<ArgumentException>(() =>
            apartment.SetOpeningBalance(1000m, 0m, null));
        Assert.Throws<ArgumentException>(() =>
            apartment.SetOpeningBalance(0m, 200m, null));
    }

    [Fact]
    public void Calculate_AidatTarget_DoesNotTouchOlderGasInvoice()
    {
        var apartment = CreateApartment(0m, 0m, DevirTarihi);
        // Daha eski doğalgaz faturası (5.000 TL) + aidat (3.385 TL).
        var invoice = CreateInvoice(5_000m, new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero));
        var aidat = new AidatInvoice(
            new DateOnly(2026, 3, 1),
            new DateOnly(2026, 3, 31),
            new DateOnly(2026, 4, 10),
            AidatDistributionMethod.Esit);
        aidat.AddCharge(apartment.Id, 3_385m);

        var payment = new Payment(
            apartment.Id,
            new DateOnly(2026, 10, 1),
            3_385m,
            "Aidat tahsilatı",
            "Ops",
            targetDebtType: LedgerDebtType.Aidat);

        var result = LedgerService.Calculate(
            apartment,
            invoices: [invoice],
            payments: [payment],
            annualInterestRate: 0m,
            asOfDate: new DateOnly(2026, 10, 1),
            aidatCharges: aidat.Charges);

        var invoiceLine = result.Lines.Single(l => l.Type == LedgerDebtType.Invoice);
        var aidatLine = result.Lines.Single(l => l.Type == LedgerDebtType.Aidat);

        Assert.Equal(0m, invoiceLine.PaidAmount);
        Assert.Equal(5_000m, invoiceLine.RemainingPrincipal);
        Assert.Empty(invoiceLine.Allocations);

        Assert.Equal(3_385m, aidatLine.PaidAmount);
        Assert.Equal(0m, aidatLine.RemainingPrincipal);
        Assert.Equal(AllocationTarget.Aidat, aidatLine.Allocations.Single().Target);
        Assert.Equal(new DateOnly(2026, 3, 1), aidatLine.PeriodStart);
        Assert.Equal(new DateOnly(2026, 4, 10), aidatLine.DueDate);
    }

    [Fact]
    public void Calculate_AidatPaymentWithoutCharge_StaysCreditAndDoesNotPayInvoice()
    {
        var apartment = CreateApartment(0m, 0m, DevirTarihi);
        var invoice = CreateInvoice(2_819.55m, new DateTimeOffset(2026, 5, 23, 0, 0, 0, TimeSpan.Zero));
        var payment = new Payment(
            apartment.Id,
            new DateOnly(2026, 10, 21),
            3_385m,
            "Aidat tahsilatı",
            "Ops",
            targetDebtType: LedgerDebtType.Aidat);

        var result = LedgerService.Calculate(
            apartment,
            invoices: [invoice],
            payments: [payment],
            annualInterestRate: 0m,
            asOfDate: new DateOnly(2026, 11, 2));

        var invoiceLine = result.Lines.Single(l => l.Type == LedgerDebtType.Invoice);
        Assert.Equal(0m, invoiceLine.PaidAmount);
        Assert.Equal(2_819.55m, invoiceLine.RemainingPrincipal);
        Assert.Equal(3_385m, result.Credits!.Single().Amount);
        Assert.Equal(2_819.55m - 3_385m, result.TotalBalance);
    }

    [Fact]
    public void Calculate_WithoutTarget_FifoPaysOlderGasInvoiceBeforeAidat()
    {
        var apartment = CreateApartment(0m, 0m, DevirTarihi);
        var invoice = CreateInvoice(5_000m, new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero));
        var aidat = new AidatInvoice(
            new DateOnly(2026, 3, 1),
            new DateOnly(2026, 3, 31),
            new DateOnly(2026, 4, 10),
            AidatDistributionMethod.Esit);
        aidat.AddCharge(apartment.Id, 3_385m);

        var payment = CreatePayment(new DateOnly(2026, 10, 1), 3_385m);

        var result = LedgerService.Calculate(
            apartment,
            invoices: [invoice],
            payments: [payment],
            annualInterestRate: 0m,
            asOfDate: new DateOnly(2026, 10, 1),
            aidatCharges: aidat.Charges);

        var invoiceLine = result.Lines.Single(l => l.Type == LedgerDebtType.Invoice);
        var aidatLine = result.Lines.Single(l => l.Type == LedgerDebtType.Aidat);

        Assert.Equal(3_385m, invoiceLine.PaidAmount);
        Assert.Equal(0m, aidatLine.PaidAmount);
    }

    [Fact]
    public void Calculate_HeatingTarget_DoesNotTouchAidat()
    {
        var apartment = CreateApartment(0m, 0m, DevirTarihi);
        var invoice = CreateInvoice(5_000m, new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero));
        var aidat = new AidatInvoice(
            new DateOnly(2026, 3, 1),
            new DateOnly(2026, 3, 31),
            new DateOnly(2026, 4, 10),
            AidatDistributionMethod.Esit);
        aidat.AddCharge(apartment.Id, 3_385m);

        var payment = new Payment(
            apartment.Id,
            new DateOnly(2026, 10, 1),
            1_000m,
            "Isınma tahsilatı",
            "Ops",
            targetDebtType: LedgerDebtType.Invoice);

        var result = LedgerService.Calculate(
            apartment,
            invoices: [invoice],
            payments: [payment],
            annualInterestRate: 0m,
            asOfDate: new DateOnly(2026, 10, 1),
            aidatCharges: aidat.Charges);

        Assert.Equal(1_000m, result.Lines.Single(l => l.Type == LedgerDebtType.Invoice).PaidAmount);
        Assert.Equal(0m, result.Lines.Single(l => l.Type == LedgerDebtType.Aidat).PaidAmount);
    }

    [Fact]
    public void Calculate_OpeningBalanceTarget_ClosesBothDevirRows_AndSkipsInvoice()
    {
        var apartment = CreateApartment(1_000m, 200m, DevirTarihi);
        var invoice = CreateInvoice(5_000m, new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));

        var payment = new Payment(
            apartment.Id,
            new DateOnly(2026, 10, 1),
            1_200m,
            "Devir tahsilatı",
            "Ops",
            targetDebtType: LedgerDebtType.OpeningBalancePrincipal);

        var result = LedgerService.Calculate(
            apartment,
            invoices: [invoice],
            payments: [payment],
            annualInterestRate: 0m,
            asOfDate: new DateOnly(2026, 10, 1));

        var interestLine = result.Lines.Single(l => l.Type == LedgerDebtType.OpeningBalanceInterest);
        var principalLine = result.Lines.Single(l => l.Type == LedgerDebtType.OpeningBalancePrincipal);
        var invoiceLine = result.Lines.Single(l => l.Type == LedgerDebtType.Invoice);

        Assert.Equal(200m, interestLine.PaidAmount);
        Assert.Equal(0m, interestLine.RemainingPrincipal);
        Assert.Equal(1_000m, principalLine.PaidAmount);
        Assert.Equal(0m, principalLine.RemainingPrincipal);
        Assert.Equal(0m, invoiceLine.PaidAmount);
        Assert.Equal(5_000m, invoiceLine.RemainingPrincipal);
    }

    [Fact]
    public void AccruesInterest_OpeningBalanceInterest_Excluded()
    {
        Assert.False(LedgerService.AccruesInterest(LedgerDebtType.OpeningBalanceInterest));
        Assert.True(LedgerService.AccruesInterest(LedgerDebtType.OpeningBalancePrincipal));
        Assert.True(LedgerService.AccruesInterest(LedgerDebtType.Invoice));
    }

    [Fact]
    public void Calculate_InvoiceWithoutLock_DoesNotAccrueInterest()
    {
        var apartment = CreateApartment(0m, 0m, DevirTarihi);
        var invoice = CreateInvoice(1_000m, new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero));

        var result = LedgerService.Calculate(
            apartment,
            invoices: [invoice],
            payments: [],
            annualInterestRate: 12m,
            asOfDate: new DateOnly(2026, 3, 1));

        Assert.Equal(0m, result.Lines.Single(l => l.Type == LedgerDebtType.Invoice).InterestAccrued);
    }

    [Fact]
    public void Lock_SetsDueDate_AndRejectsLaterChange()
    {
        var issued = new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero);
        var invoice = new GasInvoice(
            "F-1", "1", "M1",
            issued, issued.AddDays(30), issued,
            1m, 1m, 1m,
            100m, 20m, 50m, 10m, 180m);

        invoice.Lock(new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 16));

        Assert.True(invoice.IsLocked);
        Assert.Equal(new DateOnly(2026, 2, 16), invoice.DueDate);
        Assert.Throws<DomainException>(() => invoice.SetDueDate(new DateOnly(2026, 3, 1)));
    }

    [Fact]
    public void Lock_DueDateBeforeNotice_Throws()
    {
        var issued = new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero);
        var invoice = new GasInvoice(
            "F-1", "1", "M1",
            issued, issued.AddDays(30), issued,
            1m, 1m, 1m,
            100m, 20m, 50m, 10m, 180m);

        Assert.Throws<DomainException>(() =>
            invoice.Lock(new DateOnly(2026, 2, 20), new DateOnly(2026, 2, 1)));
    }

    /// <summary>
    /// CalculatePiecewiseInterest — ödemeleri olmayan basit durum, eski CalculateSimpleInterest ile örtüşür.
    /// </summary>
    [Fact]
    public void CalculatePiecewiseInterest_NoPayments_MatchesSimpleInterest()
    {
        var dueDate = new DateOnly(2026, 1, 1);
        var asOf = new DateOnly(2026, 4, 1); // 90 gün
        var expected = LedgerService.CalculateSimpleInterest(1000m, 12m, dueDate, asOf);
        var actual = LedgerService.CalculatePiecewiseInterest(1000m, 12m, dueDate, asOf, []);

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// CalculatePiecewiseInterest — tam ödeme borcun üzerindeyse faiz sıfıra düşer.
    /// </summary>
    [Fact]
    public void CalculatePiecewiseInterest_FullPaymentAtStart_ZeroRemainingInterest()
    {
        var dueDate = new DateOnly(2026, 1, 1);
        var payDate = new DateOnly(2026, 1, 1); // Vade günü ödeme
        var asOf = new DateOnly(2026, 4, 1);

        var allocations = new List<AllocationDetail>
        {
            new(Guid.NewGuid(), payDate, 1000m, AllocationTarget.OpeningBalance)
        };

        var actual = LedgerService.CalculatePiecewiseInterest(1000m, 12m, dueDate, asOf, allocations);

        // Vade günü yapılan tam ödeme → asOfDate'e kadar faiz = 0.
        Assert.Equal(0m, actual);
    }

    [Fact]
    public void PiecewiseInterest_StopsAtThePaymentDateAllocatedToThatDebt()
    {
        // Rapor edilen senaryo: Tem borcu (vade 31.07) 31.08 tahsilatıyla, Ağu borcu (vade 31.08)
        // 29.09 tahsilatıyla kapanır (FIFO). Faiz her borçta KENDİ tahsilat tarihinde durur.
        var asOf = new DateOnly(2026, 11, 4);
        var tem = LedgerService.CalculatePiecewiseInterest(
            3385m, 60m, new DateOnly(2026, 7, 31), asOf,
            [new AllocationDetail(Guid.NewGuid(), new DateOnly(2026, 8, 31), 3385m, AllocationTarget.Aidat)]);
        var agu = LedgerService.CalculatePiecewiseInterest(
            3385m, 60m, new DateOnly(2026, 8, 31), asOf,
            [new AllocationDetail(Guid.NewGuid(), new DateOnly(2026, 9, 29), 3385m, AllocationTarget.Aidat)]);

        Assert.Equal(decimal.Round(3385m * 0.60m * 31m / 365m, 2), tem); // 31 gün
        Assert.Equal(decimal.Round(3385m * 0.60m * 29m / 365m, 2), agu); // 29 gün, 29.09'da durur
    }

    [Fact]
    public void CancelledAidatInvoice_ShowsZeroBalanceAuditLine_DoesNotConsumePaymentsOrAccrueInterest()
    {
        var apartment = new Apartment(BuildingId, "10", "Daire 10");
        var cancelledInvoice = new AidatInvoice(
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30),
            new DateOnly(2026, 9, 30),
            AidatDistributionMethod.Esit);
        var cancelledCharge = cancelledInvoice.AddCharge(apartment.Id, 3385m);
        cancelledInvoice.Cancel();

        var activeInvoice = new AidatInvoice(
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 10, 31),
            new DateOnly(2026, 10, 31),
            AidatDistributionMethod.Esit);
        var activeCharge = activeInvoice.AddCharge(apartment.Id, 3385m);

        var payment = CreatePayment(new DateOnly(2026, 10, 15), 3385m);

        var result = LedgerService.Calculate(
            apartment,
            invoices: [],
            payments: [payment],
            annualInterestRate: 60m,
            asOfDate: new DateOnly(2026, 11, 5),
            aidatCharges: [cancelledCharge, activeCharge]);

        var cancelledLine = Assert.Single(result.Lines, l => l.SourceId == cancelledCharge.Id.ToString());
        Assert.True(cancelledLine.IsCancelled);
        Assert.Equal(0m, cancelledLine.PrincipalAmount);
        Assert.Equal(3385m, cancelledLine.OriginalPrincipalAmount);
        Assert.Equal(0m, cancelledLine.PaidAmount);
        Assert.Equal(0m, cancelledLine.RemainingPrincipal);
        Assert.Equal(0m, cancelledLine.InterestAccrued);
        Assert.Equal(0m, cancelledLine.LineBalance);
        Assert.Contains("İptal Edilmiş Aidat Tahakkuku", cancelledLine.Label);

        // Tahsilat iptal edilen borca değil, aktif Ekim borcuna mahsup edilmiş olmalı.
        var activeLine = Assert.Single(result.Lines, l => l.SourceId == activeCharge.Id.ToString());
        Assert.False(activeLine.IsCancelled);
        Assert.Equal(3385m, activeLine.PaidAmount);
        Assert.Equal(0m, activeLine.RemainingPrincipal);
        Assert.Equal(0m, result.TotalBalance);
    }

    [Fact]
    public void CancelledWaiverOffset_RestoresDebtAndShowsZeroBalanceAuditLine()
    {
        var apartment = new Apartment(BuildingId, "12", "Daire 12");
        var invoice = new AidatInvoice(
            new DateOnly(2026, 9, 1),
            new DateOnly(2026, 9, 30),
            new DateOnly(2026, 9, 30),
            AidatDistributionMethod.Esit);
        var charge = invoice.AddCharge(apartment.Id, 2000m);

        var waiver = WaiverOffset.Create(
            charge.Id,
            Guid.NewGuid(),
            apartment.Id,
            Guid.NewGuid(),
            1000m,
            new DateOnly(2026, 9, 1),
            "Görev Mahsubu — Yönetici Test (%50)");
        waiver.Cancel();

        var result = LedgerService.Calculate(
            apartment,
            invoices: [],
            payments: [],
            annualInterestRate: 0m,
            asOfDate: new DateOnly(2026, 10, 1),
            aidatCharges: [charge],
            waivers: [waiver]);

        var aidatLine = Assert.Single(result.Lines, l => l.Type == LedgerDebtType.Aidat);
        Assert.Equal(2000m, aidatLine.RemainingPrincipal);
        Assert.Equal(2000m, result.TotalBalance);

        var waiverLine = Assert.Single(result.Lines, l => l.Type == LedgerDebtType.WaiverOffset);
        Assert.True(waiverLine.IsCancelled);
        Assert.Equal(0m, waiverLine.PrincipalAmount);
        Assert.Equal(1000m, waiverLine.OriginalPrincipalAmount);
        Assert.Contains("İptal Edilmiş Görev Mahsubu", waiverLine.Label);
    }

    [Fact]
    public void NegativeOpeningBalance_ActsAsCreditAndOffsetsNewDebtsFirst()
    {
        var apartment = CreateApartment(-1500m, 0m, new DateOnly(2026, 1, 1));
        var invoice = new AidatInvoice(
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 2, 28),
            new DateOnly(2026, 2, 28),
            AidatDistributionMethod.Esit);
        var charge = invoice.AddCharge(apartment.Id, 1000m);

        var result = LedgerService.Calculate(
            apartment,
            invoices: [],
            payments: [],
            annualInterestRate: 60m,
            asOfDate: new DateOnly(2026, 4, 1),
            aidatCharges: [charge]);

        var openingLine = Assert.Single(result.Lines, l => l.Type == LedgerDebtType.OpeningBalancePrincipal);
        Assert.Equal("Devir Alacağı", openingLine.Label);
        Assert.Equal(-1500m, openingLine.PrincipalAmount);
        Assert.Equal(1000m, openingLine.PaidAmount); // 1000 TL borca kullanıldı
        Assert.Equal(-500m, openingLine.RemainingPrincipal); // 500 TL alacak kaldı
        Assert.Equal(0m, openingLine.InterestAccrued);

        var aidatLine = Assert.Single(result.Lines, l => l.Type == LedgerDebtType.Aidat);
        Assert.Equal(1000m, aidatLine.PaidAmount);
        Assert.Equal(0m, aidatLine.RemainingPrincipal);
        Assert.Equal(0m, aidatLine.InterestAccrued); // Devir alacağı ile kapandığı için faiz işlemez

        Assert.Equal(-500m, result.TotalBalance); // Net 500 TL alacaklı
    }

    [Fact]
    public void SetOpeningBalance_NegativeInterest_ThrowsArgumentException()
    {
        var apartment = new Apartment(BuildingId, "15", "Daire 15");
        Assert.Throws<ArgumentException>(() =>
            apartment.SetOpeningBalance(0m, -100m, new DateOnly(2026, 1, 1)));
    }
}
