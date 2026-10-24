using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;
using Westalife.Domain.Services.CostDistribution;
using Xunit;

namespace Westalife.Domain.Tests;

/// <summary>
/// Hesaplama motoru testleri — Kural #1: Σ(dağıtım) == fatura tutarı (kuruşu kuruşuna mutabakat).
/// </summary>
public class CostDistributionServiceTests
{
    private static ApartmentConsumptionInfo Apt(int door, decimal m2, decimal kwh, bool failed = false) =>
        new(Guid.NewGuid(), door, m2, kwh, failed);

    [Fact]
    public void Calculate_ThreeApartmentExample_ProducesExactPennyGapZero()
    {
        // Toplam 5.545,20 TL = 554.520 kuruş; sabit %30 = 166.356 kuruş; tüketim %70 = 388.164 kuruş.
        var apartments = new[]
        {
            Apt(1, 100m, 5000m),
            Apt(2, 120m, 8000m),
            Apt(3, 80m, 3000m),
        };

        var shares = CostDistributionService.Calculate(554_520L, 0.30m, apartments);

        Assert.Equal(554_520L, shares.Sum(s => s.TotalAmountKurus));

        var expectedFixed = 166_356L;
        Assert.Equal(expectedFixed, shares.Sum(s => s.FixedAmountKurus));
        Assert.Equal(554_520L - expectedFixed, shares.Sum(s => s.ConsumptionAmountKurus));

        // Sabit paylar m² oranında: 100/300, 120/300, 80/300 → 55452.0, 66542.4, 44361.6
        // Floor: 55452 + 66542 + 44361 = 166355 → kalan 1 kuruş EN BÜYÜK kesire (0.6 → apt3) gider.
        Assert.Equal(55_452L, shares[0].FixedAmountKurus);
        Assert.Equal(66_542L, shares[1].FixedAmountKurus);
        Assert.Equal(44_362L, shares[2].FixedAmountKurus);
    }

    [Fact]
    public void Calculate_FailedMeter_GetsFixedShareButZeroConsumption()
    {
        var apartments = new[]
        {
            Apt(1, 100m, 0m, failed: true),   // okunamadı
            Apt(2, 100m, 6000m),
        };

        var shares = CostDistributionService.Calculate(100_000L, 0.30m, apartments);

        Assert.Equal(100_000L, shares.Sum(s => s.TotalAmountKurus));
        Assert.True(shares[0].IsMeterReadFailed);
        Assert.Equal(0L, shares[0].ConsumptionAmountKurus); // %70'ten pay alamaz
        Assert.True(shares[0].FixedAmountKurus > 0);        // ama m² payını öder
        Assert.Equal(shares[0].FixedAmountKurus, shares[1].FixedAmountKurus); // eşit m² → eşit sabit pay
    }

    [Fact]
    public void Calculate_TieBreaker_DistributesByDoorNumberOrder()
    {
        // Eşit kesirler → önce büyük ağırlık, sonra küçük indeks (kapı sırası): deterministik.
        var apartments = new[]
        {
            Apt(1, 100m, 1000m),
            Apt(2, 100m, 1000m),
            Apt(3, 100m, 1000m),
        };

        var shares = CostDistributionService.Calculate(1_000L, 0.30m, apartments);

        Assert.Equal(1_000L, shares.Sum(s => s.TotalAmountKurus));
        // Sabit: 300 kuruş / 3 daire → 100'er. Tüketim: 700/3 = 233.33'er → kesirler eşit,
        // ağırlıklar eşit → KÜÇÜK indeks (kapı 1) kalan kuruşu alır: 234, 233, 233.
        Assert.Equal(100L, shares[0].FixedAmountKurus);
        Assert.Equal(100L, shares[1].FixedAmountKurus);
        Assert.Equal(100L, shares[2].FixedAmountKurus);
        Assert.Equal(234L, shares[0].ConsumptionAmountKurus);
        Assert.Equal(233L, shares[1].ConsumptionAmountKurus);
        Assert.Equal(233L, shares[2].ConsumptionAmountKurus);
    }

    [Fact]
    public void Calculate_SeventyTwoApartments_TotalAlwaysMatchesInvoice()
    {
        // 72 dairelik gerçekçi senaryo — m² ve kWh'ler kapı numarasına göre türetilir.
        var random = new Random(2026);
        var apartments = Enumerable.Range(1, 72)
            .Select(i => Apt(i, 80m + i % 40, 500m + random.Next(0, 3000)))
            .ToList();

        var shares = CostDistributionService.Calculate(554_520L, 0.30m, apartments);

        Assert.Equal(72, shares.Count);
        Assert.Equal(554_520L, shares.Sum(s => s.TotalAmountKurus));
        Assert.All(shares, s => Assert.True(s.TotalAmountKurus >= 0));
    }

    [Fact]
    public void Calculate_AllMetersFailed_ThrowsZeroTotalConsumption()
    {
        var apartments = new[]
        {
            Apt(1, 100m, 0m, failed: true),
            Apt(2, 100m, 0m, failed: true),
        };

        var ex = Assert.Throws<DomainException>(() =>
            CostDistributionService.Calculate(100_000L, 0.30m, apartments));

        Assert.Equal(DomainConstants.CostDistribution.ZeroTotalConsumption, ex.Message);
    }

    [Fact]
    public void Calculate_InvalidFixedCostRatio_Throws()
    {
        var apartments = new[] { Apt(1, 100m, 1000m) };

        Assert.Throws<DomainException>(() => CostDistributionService.Calculate(100_000L, 0m, apartments));
        Assert.Throws<DomainException>(() => CostDistributionService.Calculate(100_000L, 1m, apartments));
    }

    [Fact]
    public void Calculate_RoundingOfFixedPortion_UsesAwayFromZero()
    {
        // %30'u tam yarım kuruş olan tutar: 5 kuruş → 1.5 → AwayFromZero ile 2 kuruş sabit; tüketim 3 kuruş.
        var apartments = new[] { Apt(1, 100m, 1000m) };

        var shares = CostDistributionService.Calculate(5L, 0.30m, apartments);

        Assert.Equal(2L, shares[0].FixedAmountKurus);
        Assert.Equal(3L, shares[0].ConsumptionAmountKurus);
        Assert.Equal(5L, shares[0].TotalAmountKurus);
    }
}
