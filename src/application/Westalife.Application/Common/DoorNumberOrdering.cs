namespace Westalife.Application.Common;

/// <summary>
/// Kapı numarası DB'de metin (string) tutulur; DB sıralaması 1, 10, 11, 2... verir.
/// Bu yardımcı sayısal sıralar (1, 2, ... 10, 11); sayı olmayanlar sona, kendi aralarında alfabetik gider.
/// </summary>
public static class DoorNumberOrdering
{
    public static IOrderedEnumerable<T> OrderByDoorNumber<T>(this IEnumerable<T> source, Func<T, string> doorNumber) =>
        source
            .OrderBy(x => int.TryParse(doorNumber(x), out var n) ? n : int.MaxValue)
            .ThenBy(x => doorNumber(x), StringComparer.OrdinalIgnoreCase);
}
