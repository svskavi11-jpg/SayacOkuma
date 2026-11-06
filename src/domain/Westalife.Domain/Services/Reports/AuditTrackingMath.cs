namespace Westalife.Domain.Services.Reports;

/// <summary>
/// Daire takip satırının kuruş kimliği.
/// Net = devir + ek aidat + aidat + yakıt + gecikme faizi − aidat tahsilatı − yakıt tahsilatı − görev mahsubu.
/// </summary>
public static class AuditTrackingMath
{
    public static bool InRange(DateOnly date, DateOnly from, DateOnly to) =>
        date >= from && date <= to;

    public static decimal DevirColumn(
        decimal openingBalance,
        decimal openingEntered,
        decimal openingCollected,
        decimal unallocated) =>
        openingBalance + openingEntered - openingCollected - unallocated;

    public static decimal Net(
        decimal devir,
        decimal extraAidat,
        decimal aidatAccrual,
        decimal fuelAccrual,
        decimal interest,
        decimal aidatCollected,
        decimal fuelCollected,
        decimal waiver) =>
        devir + extraAidat + aidatAccrual + fuelAccrual + interest - aidatCollected - fuelCollected - waiver;
}
