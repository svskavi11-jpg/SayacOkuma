using Westalife.Domain.Entities;
using Westalife.Domain.Services.Ledger;
using Xunit;

namespace Westalife.Domain.Tests;

/// <summary>
/// Cari hesap (Ledger) motoru testleri — Etap 6.8 anapara/faiz ayrımı:
/// devir bakiyesi iki borç satırı olarak üretilir; FIFO sırası Devir Faiz → Devir Anapara → Fatura;
/// geçmiş faize tekrar faiz İŞLEMEZ; sentez SourceId'ler benzersizdir.
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

    [Fact]
    public void Calculate_PartialPayment_LeavesPrincipalForInterestCalculation()
    {
        var apartment = CreateApartment(1000m, 0m, DevirTarihi);
        // 500 TL ödeme → kalan anapara 500; faiz 500 × %12 × 31/365 = 5,10.
        var payment = CreatePayment(new DateOnly(2026, 2, 1), 500m);
        var asOf = new DateOnly(2026, 2, 1);

        var result = LedgerService.Calculate(
            apartment, invoices: [], payments: [payment], annualInterestRate: 12m, asOfDate: asOf);

        var principalLine = result.Lines.Single(l => l.Type == LedgerDebtType.OpeningBalancePrincipal);
        Assert.Equal(500m, principalLine.RemainingPrincipal);
        Assert.Equal(5.10m, principalLine.InterestAccrued);
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
    public void AccruesInterest_OpeningBalanceInterest_Excluded()
    {
        Assert.False(LedgerService.AccruesInterest(LedgerDebtType.OpeningBalanceInterest));
        Assert.True(LedgerService.AccruesInterest(LedgerDebtType.OpeningBalancePrincipal));
        Assert.True(LedgerService.AccruesInterest(LedgerDebtType.Invoice));
    }
}