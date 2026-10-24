using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Domain.Services.CostDistribution;

/// <summary>Hesaplamaya giren tek dairenin dönemsel bilgisi (SCD çözümü + tüketim birleştirilmiş halde).</summary>
public sealed record ApartmentConsumptionInfo(
    Guid ApartmentId,
    int DoorNumber,
    decimal SquareMeters,
    decimal ConsumptionKWh,
    bool IsMeterReadFailed);

/// <summary>Tek dairenin hesaplanan borç payı (kuruş bazında).</summary>
public sealed record ApartmentCostShare(
    Guid ApartmentId,
    long FixedAmountKurus,
    long ConsumptionAmountKurus,
    decimal SquareMetersUsed,
    decimal ConsumptionKWhUsed,
    bool IsMeterReadFailed)
{
    public long TotalAmountKurus => FixedAmountKurus + ConsumptionAmountKurus;
}

/// <summary>
/// 30/70 gider dağıtım motoru (Domain Service — saf C#, EF/IO bağımlılığı yok).
/// Kurallar:
///  - %30 sabit gider → m² (SCD Type 2'den çözülmüş dönemsel değer)
///  - %70 tüketim gideri → kWh (payda YALNIZCA okunabilen daireler — S2 kararı)
///  - Okunamayan daire: tüketim 0 kabul edilir, m² payını yine öder (onaylı strateji §3)
///  - %70 dilimi ayrı yuvarlanmaz: Toplam − Sabit ile bulunur (çift yuvarlama hatası olmaz)
///  - En Büyük Kalanlar Yöntemi ile Σ(dağıtım) == totalAmountKurus (kuruşu kuruşuna mutabakat)
/// </summary>
public static class CostDistributionService
{
    public static IReadOnlyList<ApartmentCostShare> Calculate(
        long totalAmountKurus,
        decimal fixedCostRatio,
        IReadOnlyList<ApartmentConsumptionInfo> apartments)
    {
        if (fixedCostRatio is <= 0m or >= 1m)
        {
            throw new DomainException(DomainConstants.CostDistribution.InvalidFixedCostRatio);
        }

        if (totalAmountKurus <= 0)
        {
            throw new DomainException(DomainConstants.CostDistribution.InvalidTotalAmount);
        }

        if (apartments.Count == 0)
        {
            throw new DomainException(DomainConstants.CostDistribution.NoPositiveWeight);
        }

        // 1) Sabit gider dilimi (%30) — ayrı yuvarlanır (tek yuvarlama noktası).
        var fixedKurus = (long)Math.Round(totalAmountKurus * fixedCostRatio, MidpointRounding.AwayFromZero);
        var consumptionKurus = totalAmountKurus - fixedKurus;

        // 2) Sabit gider dağıtımı: m² ağırlıklı (tüm daireler dahil).
        var fixedShares = LargestRemainderDistributor.Distribute(
            fixedKurus,
            apartments.Select(a => a.SquareMeters).ToList());

        // 3) Tüketim gideri dağıtımı: kWh ağırlıklı (okunamayan dairelerin ağırlığı 0).
        var consumptionWeights = apartments
            .Select(a => a.IsMeterReadFailed ? 0m : a.ConsumptionKWh)
            .ToList();

        if (consumptionWeights.All(w => w == 0m))
        {
            throw new DomainException(DomainConstants.CostDistribution.ZeroTotalConsumption);
        }

        var consumptionShares = LargestRemainderDistributor.Distribute(consumptionKurus, consumptionWeights);

        // 4) Sonuç satırları + zorunlu mutabakat kontrolü (Kural #1).
        var shares = new List<ApartmentCostShare>(apartments.Count);
        for (var i = 0; i < apartments.Count; i++)
        {
            shares.Add(new ApartmentCostShare(
                apartments[i].ApartmentId,
                fixedShares[i],
                consumptionShares[i],
                apartments[i].SquareMeters,
                apartments[i].IsMeterReadFailed ? 0m : apartments[i].ConsumptionKWh,
                apartments[i].IsMeterReadFailed));
        }

        var allocatedTotal = shares.Sum(s => s.TotalAmountKurus);
        if (allocatedTotal != totalAmountKurus)
        {
            throw new DomainException(DomainConstants.CostDistribution.TotalAllocationMismatch);
        }

        return shares;
    }
}
