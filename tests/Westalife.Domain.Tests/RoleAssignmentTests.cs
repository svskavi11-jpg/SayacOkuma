using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;
using Westalife.Domain.Services.Ledger;

namespace Westalife.Domain.Tests;

public class RoleAssignmentTests
{
    private static readonly DateOnly Start = new(2026, 1, 1);
    private static readonly Guid ApartmentId = Guid.NewGuid();

    [Fact]
    public void Place_ClosesOpenAssignmentTheDayBeforeTheNewOne()
    {
        var open = RoleAssignment.Create(ApartmentId, "Ayşe Yılmaz", RoleType.Yonetici, Start, null, 0.5m, null);
        var next = RoleAssignment.Create(ApartmentId, "Mehmet Demir", RoleType.Yonetici, new DateOnly(2026, 6, 1), null, 0.5m, null);

        RoleAssignment.Place([open], next);

        Assert.Equal(new DateOnly(2026, 5, 31), open.EndDate);
        Assert.Null(next.EndDate);
    }

    [Fact]
    public void Place_RejectsOverlapWithClosedHistory()
    {
        var closed = RoleAssignment.Create(
            ApartmentId, "Ayşe Yılmaz", RoleType.Yonetici, Start, new DateOnly(2026, 6, 30), 0.5m, null);
        var overlapping = RoleAssignment.Create(
            ApartmentId, "Mehmet Demir", RoleType.Denetci, new DateOnly(2026, 6, 1), null, 0m, null);

        var error = Assert.Throws<DomainException>(() => RoleAssignment.Place([closed], overlapping));

        Assert.Equal(DomainConstants.RoleAssignment.Overlap, error.Message);
    }

    [Fact]
    public void AmountFor_HalfRate_IsHalfTheCharge()
    {
        Assert.Equal(500m, WaiverOffset.AmountFor(1000m, 0.5m));
        Assert.Equal(0m, WaiverOffset.AmountFor(1000m, 0m));
    }

    [Fact]
    public void Create_RejectsRateAboveOne()
    {
        var error = Assert.Throws<DomainException>(() =>
            RoleAssignment.Create(ApartmentId, "Ayşe", RoleType.Yonetici, Start, null, 1.5m, null));

        Assert.Equal(DomainConstants.RoleAssignment.RateInvalid, error.Message);
    }

    [Fact]
    public void HalfExemption_ReducesAidatBeforeInterest_AndIsNotACollection()
    {
        var apartment = new Apartment(Guid.NewGuid(), "1", "Daire 1");
        var aidat = new AidatInvoice(
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            new DateOnly(2026, 7, 10),
            AidatDistributionMethod.Esit);
        var charge = aidat.AddCharge(apartment.Id, 1000m);
        var heating = new ApartmentInvoice(
            Guid.NewGuid(),
            apartment.Id,
            100m,
            500m,
            400_000,
            400_000,
            false,
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
        var role = RoleAssignment.Create(apartment.Id, "Ayşe Yılmaz", RoleType.Yonetici, Start, null, 0.5m, null);
        var waiver = WaiverOffset.Create(
            charge.Id,
            role.Id,
            apartment.Id,
            Guid.NewGuid(),
            WaiverOffset.AmountFor(charge.Amount, role.ExemptionRate),
            aidat.PeriodStart,
            WaiverOffset.Describe(role.RoleType, role.PersonName, role.ExemptionRate));

        var result = LedgerService.Calculate(
            apartment,
            [heating],
            [],
            annualInterestRate: 36.5m,
            asOfDate: new DateOnly(2026, 8, 10),
            aidat.Charges,
            [waiver]);

        var aidatLine = Assert.Single(result.Lines, l => l.Type == LedgerDebtType.Aidat);
        var heatingLine = Assert.Single(result.Lines, l => l.Type == LedgerDebtType.Invoice);
        var waiverLine = Assert.Single(result.Lines, l => l.Type == LedgerDebtType.WaiverOffset);

        Assert.Equal(8, (int)LedgerDebtType.WaiverOffset);
        Assert.Equal(1000m, aidatLine.PrincipalAmount);
        Assert.Equal(0m, aidatLine.PaidAmount);
        Assert.Empty(aidatLine.Allocations);
        Assert.Equal(500m, aidatLine.RemainingPrincipal);
        Assert.Equal(15.50m, aidatLine.InterestAccrued);
        Assert.Equal(8000m, heatingLine.RemainingPrincipal);
        Assert.Equal(0m, heatingLine.PaidAmount);
        Assert.Equal(500m, waiverLine.PrincipalAmount);
        Assert.Equal(0m, waiverLine.LineBalance);
        Assert.Equal($"waiver:{charge.Id}", waiverLine.SourceId);
        Assert.Equal(500m + 15.50m + 8000m, result.TotalBalance);
        Assert.Equal(500m, result.Summary!.AidatPrincipalDue);
    }

    [Fact]
    public void SmartWaiverOffset_DeductsAidatFirst_ThenHeating_AndWritesRemainderAsCredit()
    {
        // Senaryo: Dairenin 2.500 TL Aidat borcu ve 3.704,55 TL Isınma borcu var (Toplam borç = 6.204,55 TL).
        // Denetçi / Yöneticiye Yıllık 10.000 TL sabit Huzur Hakkı tanımlandığında:
        // 1) Önce 2.500 TL Aidat borcu sıfırlanır.
        // 2) Kalan 7.500 TL'den 3.704,55 TL Isınma borcu sıfırlanır.
        // 3) Hala artan 3.795,45 TL dairenin Cari Hesabına "Alacak" (-3.795,45 TL bakiye) olarak yazılır.
        var apartment = new Apartment(Guid.NewGuid(), "25", "Daire 25");
        var aidat = new AidatInvoice(
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30),
            new DateOnly(2026, 6, 30),
            AidatDistributionMethod.Esit);
        var charge = aidat.AddCharge(apartment.Id, 2500m);

        var heating = new ApartmentInvoice(
            Guid.NewGuid(),
            apartment.Id,
            100m,
            500m,
            185_227, // 1852.27
            185_228, // 1852.28 -> toplam 3704.55 TL
            false,
            new DateTimeOffset(2026, 6, 15, 0, 0, 0, TimeSpan.Zero));

        var role = RoleAssignment.Create(
            apartment.Id,
            "Ali Denetçi",
            RoleType.Denetci,
            new DateOnly(2026, 6, 1),
            null,
            0m,
            null,
            RolePaymentType.SabitTutar,
            10_000m,
            RolePeriodType.Yillik);

        var waiver = WaiverOffset.Create(
            charge.Id,
            role.Id,
            apartment.Id,
            Guid.NewGuid(),
            WaiverOffset.AmountFor(charge.Amount, role),
            aidat.PeriodStart,
            WaiverOffset.Describe(role));

        Assert.Equal(10_000m, waiver.Amount);

        var result = LedgerService.Calculate(
            apartment,
            [heating],
            [],
            annualInterestRate: 60m,
            asOfDate: new DateOnly(2026, 8, 1),
            aidat.Charges,
            [waiver]);

        var aidatLine = Assert.Single(result.Lines, l => l.Type == LedgerDebtType.Aidat);
        var heatingLine = Assert.Single(result.Lines, l => l.Type == LedgerDebtType.Invoice);
        var waiverLine = Assert.Single(result.Lines, l => l.Type == LedgerDebtType.WaiverOffset);

        Assert.Equal(0m, aidatLine.RemainingPrincipal);
        Assert.Equal(0m, aidatLine.InterestAccrued);
        Assert.Equal(0m, heatingLine.RemainingPrincipal);
        Assert.Equal(0m, heatingLine.InterestAccrued);

        Assert.Equal(10_000m, waiverLine.PrincipalAmount);
        Assert.Equal(6_204.55m, waiverLine.PaidAmount); // 2500 + 3704.55 borca mahsup edildi
        Assert.Equal(-3_795.45m, waiverLine.RemainingPrincipal); // Kalan 3.795,45 TL alacak
        Assert.Equal(-3_795.45m, waiverLine.LineBalance);
        Assert.Equal(-3_795.45m, result.TotalBalance);
    }

    [Fact]
    public void OpenEndedRole_CoversAllSubsequentAidatPeriods_ForDaire64Scenario()
    {
        // Daire 64 senaryosu: 01.06.2026 başlangıçlı, bitiş tarihi boş (Devam ediyor), %100 Yönetici muafiyeti
        var role = RoleAssignment.Create(
            ApartmentId,
            "Daire 64 Yönetici",
            RoleType.Yonetici,
            new DateOnly(2026, 6, 1),
            null,
            1.00m,
            null);

        Assert.True(role.HasCompensation);
        Assert.False(role.CoversPeriod(new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31)));
        Assert.True(role.CoversPeriod(new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30)));
        Assert.True(role.CoversPeriod(new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 31)));
        Assert.True(role.CoversPeriod(new DateOnly(2026, 11, 1), new DateOnly(2026, 11, 30)));
        Assert.Equal(3385m, WaiverOffset.AmountFor(3385m, role));
    }

    [Fact]
    public void RebindBudgetItem_MovesWaiverToNewSelectedBudgetItem()
    {
        var oldBudgetItemId = Guid.NewGuid();
        var newBudgetItemId = Guid.NewGuid();
        var waiver = WaiverOffset.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            ApartmentId,
            oldBudgetItemId,
            3385m,
            new DateOnly(2026, 6, 1),
            "Görev Mahsubu — Yönetici (%100)");

        Assert.Equal(oldBudgetItemId, waiver.BudgetItemId);

        waiver.RebindBudgetItem(newBudgetItemId);

        Assert.Equal(newBudgetItemId, waiver.BudgetItemId);
        Assert.NotNull(waiver.LastModifiedOn);
    }
}
