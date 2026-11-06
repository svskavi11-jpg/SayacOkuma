using Westalife.Domain.Constants;

namespace Westalife.Domain.Services.Budget;

/// <summary>
/// Bütçe aritmetiği. Tahmini tutar değişmez; kalan = tahmini − gerçekleşen.
/// Tutarlar kuruşa (2 hane), yüzde 2 haneye yuvarlanır. Yarıma yuvarlama yukarı (AwayFromZero).
/// </summary>
public static class BudgetMath
{
    public static decimal AdjustedEstimate(decimal estimated, decimal transferIn, decimal transferOut) =>
        decimal.Round(estimated + transferIn - transferOut, DomainConstants.MoneyScale, MidpointRounding.AwayFromZero);

    public static decimal Balance(decimal estimatedIncome, decimal estimatedExpense) =>
        decimal.Round(estimatedIncome - estimatedExpense, DomainConstants.MoneyScale, MidpointRounding.AwayFromZero);
    public static decimal Remaining(decimal estimated, decimal actual) =>
        decimal.Round(estimated - actual, DomainConstants.MoneyScale, MidpointRounding.AwayFromZero);

    public static decimal UtilizationPercent(decimal estimated, decimal actual)
    {
        if (estimated <= 0m)
        {
            return 0m;
        }

        return decimal.Round(
            actual / estimated * 100m,
            DomainConstants.MoneyScale,
            MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Gerçekleşen tutar, tahmini tutarın %90'ına (kuruş hassasiyetiyle) ulaştıysa veya aştıysa true.
    /// Uyarıdır; kaydı engellemez.
    /// </summary>
    public static bool ReachesWarning(decimal estimated, decimal actual)
    {
        if (estimated <= 0m)
        {
            return false;
        }

        var threshold = decimal.Round(
            estimated * DomainConstants.Budget.WarningRatio,
            DomainConstants.MoneyScale,
            MidpointRounding.AwayFromZero);

        return actual >= threshold;
    }
}
