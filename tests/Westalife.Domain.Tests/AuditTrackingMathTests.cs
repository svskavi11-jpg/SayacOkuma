using Westalife.Domain.Entities;
using Westalife.Domain.Services.Ledger;
using Westalife.Domain.Services.Reports;

namespace Westalife.Domain.Tests;

public class AuditTrackingMathTests
{
    [Fact]
    public void Net_IncludesInterestAndMatchesTheApprovedFormula()
    {
        var net = AuditTrackingMath.Net(
            devir: 1_000m,
            extraAidat: 100m,
            aidatAccrual: 500m,
            fuelAccrual: 800m,
            interest: 15.50m,
            aidatCollected: 200m,
            fuelCollected: 300m,
            waiver: 250m);

        Assert.Equal(1_665.50m, net);
    }

    [Fact]
    public void DevirColumn_AddsOpeningEnteredAndRemovesCollections()
    {
        var devir = AuditTrackingMath.DevirColumn(
            openingBalance: 400m,
            openingEntered: 1_000m,
            openingCollected: 150m,
            unallocated: 25m);

        Assert.Equal(1_225m, devir);
        Assert.Equal(
            1_225m + 10m + 5.50m - 40m,
            AuditTrackingMath.Net(devir, 0m, 0m, 10m, 5.50m, 0m, 40m, 0m));
    }

    [Fact]
    public void CutOpening_HidesBalanceDatedAfterTheSnapshot()
    {
        var apartment = new Apartment(Guid.NewGuid(), "1", "Daire 1");
        apartment.SetOpeningBalance(1_000m, 50m, new DateOnly(2026, 6, 1));

        var before = LedgerService.Calculate(
            apartment, [], [], 0m, new DateOnly(2026, 5, 31), cutOpeningAtAsOf: true);
        var onDate = LedgerService.Calculate(
            apartment, [], [], 0m, new DateOnly(2026, 6, 1), cutOpeningAtAsOf: true);

        Assert.Equal(0m, before.TotalBalance);
        Assert.Equal(1_050m, onDate.TotalBalance);
    }
}
