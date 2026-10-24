using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Domain.Services.CostDistribution;

/// <summary>
/// En Büyük Kalanlar Yöntemi (Largest Remainder Method / Hamilton) çekirdeği.
/// Kural #1: Σ(dağıtım) == totalKurus KESİNLİKLE sağlanır (penny gap = 0).
/// Tüm aritmetik kuruş bazında `long` ve `decimal` ile yapılır; floating-point YASAKTIR.
///
/// Adımlar:
/// 1. Her payda: kuruş payı = totalKurus × ağırlık / Σağırlık → TABAN'a (floor) yuvarlanır.
/// 2. Kalan kuruşlar: kesir kısmı EN BÜYÜK paydalardan başlanarak 1'er kuruş dağıtılır.
/// 3. Eşitlik (tie) durumunda: önce büyük ağırlık, sonra küçük indeks (kapı no sırası) — deterministik.
/// </summary>
public static class LargestRemainderDistributor
{
    /// <param name="totalKurus">Dağıtılacak toplam (kuruş).</param>
    /// <param name="weights">Pay anahtarları (m² veya kWh); indeks sırası kapı numarası sırasıdır.</param>
    /// <returns>Her dairenin kuruş payı — toplamı totalKurus'a birebir eşittir.</returns>
    public static long[] Distribute(long totalKurus, IReadOnlyList<decimal> weights)
    {
        if (totalKurus <= 0)
        {
            throw new DomainException(DomainConstants.CostDistribution.InvalidTotalAmount);
        }

        var totalWeight = weights.Sum();
        if (totalWeight <= 0m)
        {
            throw new DomainException(DomainConstants.CostDistribution.NoPositiveWeight);
        }

        // Ondalıklı ham paylar (kuruş cinsinden).
        var rawShares = new decimal[weights.Count];
        var floors = new long[weights.Count];
        for (var i = 0; i < weights.Count; i++)
        {
            if (weights[i] < 0m)
            {
                throw new DomainException(DomainConstants.CostDistribution.NoPositiveWeight);
            }

            rawShares[i] = (decimal)totalKurus * weights[i] / totalWeight;
            floors[i] = (long)Math.Floor(rawShares[i]);
        }

        var remaining = totalKurus - floors.Sum();

        // Kalan kuruşlar: en büyük kesir → en büyük ağırlık → en küçük indeks önceliğiyle.
        var order = Enumerable.Range(0, weights.Count)
            .OrderByDescending(i => rawShares[i] - floors[i])
            .ThenByDescending(i => weights[i])
            .ThenBy(i => i);

        foreach (var index in order)
        {
            if (remaining <= 0)
            {
                break;
            }

            floors[index]++;
            remaining--;
        }

        // Floor kullanıldığı için remaining asla negatif olamaz; savunmacı kontrol.
        if (remaining != 0)
        {
            throw new DomainException(DomainConstants.CostDistribution.TotalAllocationMismatch);
        }

        return floors;
    }
}
