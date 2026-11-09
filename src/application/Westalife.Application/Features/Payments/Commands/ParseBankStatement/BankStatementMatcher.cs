using System.Text;
using System.Text.RegularExpressions;
using Westalife.Domain.Services.Ledger;

namespace Westalife.Application.Features.Payments.Commands.ParseBankStatement;

/// <summary>
/// Banka ekstresi açıklama metinlerinden Daire No, Sakin ve Hedef Borç Türü (Aidat / Isınma / Diğer)
/// çıkaran deterministik eşleştirme motoru.
/// </summary>
public static partial class BankStatementMatcher
{
    public sealed record ResidentLookupItem(
        Guid ApartmentId,
        string DoorNumber,
        Guid ResidentId,
        string ResidentName,
        string NormalizedResidentName);

    // 1. Maskeleme Regex'leri (IBAN, Tarih, Saat, Yıl, TCKN/Uzun Referans No, Tutar)
    [GeneratedRegex(@"\bTR\s*\d{2}(?:\s*[0-9A-Z]){16,26}\b", RegexOptions.Compiled)]
    private static partial Regex IbanRegex();

    [GeneratedRegex(@"\b\d{1,2}[./\-]\d{1,2}[./\-]\d{2,4}\b|\b\d{4}[./\-]\d{1,2}[./\-]\d{1,2}\b", RegexOptions.Compiled)]
    private static partial Regex FullDateRegex();

    [GeneratedRegex(@"\b(?:0?[1-9]|1[0-2])[./\-]20\d{2}\b|\b20\d{2}[./\-](?:0?[1-9]|1[0-2])\b", RegexOptions.Compiled)]
    private static partial Regex MonthYearRegex();

    [GeneratedRegex(@"\b\d{1,2}:\d{2}(?::\d{2})?\b", RegexOptions.Compiled)]
    private static partial Regex TimeRegex();

    [GeneratedRegex(@"\b\d+(?:[.,]\d+)+\s*(?:TL|TRY)?\b|\b\d+\s*(?:TL|TRY)\b", RegexOptions.Compiled)]
    private static partial Regex CurrencyAmountRegex();

    [GeneratedRegex(@"\b20[1-3]\d\b", RegexOptions.Compiled)]
    private static partial Regex YearRegex();

    [GeneratedRegex(@"\b\d{4,}\b", RegexOptions.Compiled)]
    private static partial Regex LongNumberRegex();

    // 2. Çok Katmanlı Daire No Regex'leri
    // Öncelik 1: Önekli ("DAIRE 14", "DAIRE: 14", "D:5", "D-12", "D.8", "D 22", "KAPI NO 9", "NO: 15", "NO 3")
    [GeneratedRegex(@"\b(?:DAIRE\s*NO|DAIRENO|KAPI\s*NO|KAPINO|DAIRE|KAPI|D\s*NO|D|NO)\s*[:.\-#/]*\s*(\d{1,3})\b", RegexOptions.Compiled)]
    private static partial Regex PrefixDoorRegex();

    // Öncelik 2: Sonekli ("14 NOLU DAIRE", "14 NOLU", "14 NO LU", "25 DAIRE", "7. DAIRE", "12 NO")
    [GeneratedRegex(@"\b(\d{1,3})\s*[.\-#/]*\s*(?:NOLU\s*DAIRE|NO\s*LU\s*DAIRE|NOLU|NO\s*LU|DAIRE|KAPI|NO)\b", RegexOptions.Compiled)]
    private static partial Regex SuffixDoorRegex();

    /// <summary>
    /// Türkçe karakterleri ASCII büyük harf karşılıklarına dönüştürür (İ->I, ı->I, Ş->S, Ğ->G, Ü->U, Ö->O, Ç->C).
    /// </summary>
    public static string NormalizeTurkishUpper(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(input.Length);
        foreach (var ch in input)
        {
            sb.Append(ch switch
            {
                'ı' or 'I' or 'i' or 'İ' => 'I',
                'ş' or 'Ş' or 's' => 'S',
                'ğ' or 'Ğ' or 'g' => 'G',
                'ü' or 'Ü' or 'u' => 'U',
                'ö' or 'Ö' or 'o' => 'O',
                'ç' or 'Ç' or 'c' => 'C',
                _ => char.ToUpperInvariant(ch)
            });
        }

        return sb.ToString();
    }

    /// <summary>
    /// IBAN, tarih (05.11.2026), dönem (11/2026), saat, para tutarı, yıl (2026) ve 4+ haneli
    /// referans/TCKN/hesap numaralarını maskeler; böylece içlerindeki rakamlar daire no sanılmaz.
    /// </summary>
    public static string MaskNoiseTokens(string normalizedUpperText)
    {
        if (string.IsNullOrWhiteSpace(normalizedUpperText))
        {
            return string.Empty;
        }

        var masked = IbanRegex().Replace(normalizedUpperText, " [IBAN] ");
        masked = FullDateRegex().Replace(masked, " [DATE] ");
        masked = MonthYearRegex().Replace(masked, " [PERIOD] ");
        masked = TimeRegex().Replace(masked, " [TIME] ");
        masked = CurrencyAmountRegex().Replace(masked, " [AMT] ");
        masked = YearRegex().Replace(masked, " [YEAR] ");
        masked = LongNumberRegex().Replace(masked, " [REF] ");
        return masked;
    }

    /// <summary>
    /// Açıklama metninden geçerli bir Daire Kapı Numarası çıkarır.
    /// Önce Önekli Regex, sonra Sonekli Regex, en son Sakin Ad-Soyad eşleşmesi (Fallback) çalıştırılır.
    /// </summary>
    public static string? ExtractDoorNumber(
        string? rawDescription,
        IReadOnlySet<string> validDoorNumbers,
        IReadOnlyList<ResidentLookupItem>? activeResidents = null)
    {
        if (string.IsNullOrWhiteSpace(rawDescription) || validDoorNumbers.Count == 0)
        {
            return null;
        }

        var normalized = NormalizeTurkishUpper(rawDescription);
        var masked = MaskNoiseTokens(normalized);

        // 1. Önekli Desenler: DAIRE 14, D-5, KAPI NO: 8, NO: 12 vb.
        foreach (Match match in PrefixDoorRegex().Matches(masked))
        {
            var candidate = NormalizeDoorCandidate(match.Groups[1].Value);
            if (candidate is not null && validDoorNumbers.Contains(candidate))
            {
                return candidate;
            }
        }

        // 2. Sonekli Desenler: 14 NOLU DAIRE, 14 NOLU, 25 DAIRE vb.
        foreach (Match match in SuffixDoorRegex().Matches(masked))
        {
            var candidate = NormalizeDoorCandidate(match.Groups[1].Value);
            if (candidate is not null && validDoorNumbers.Contains(candidate))
            {
                return candidate;
            }
        }

        // 3. Sakin Adı / Soyadı Yedek Eşleştirmesi (Fallback)
        if (activeResidents is { Count: > 0 })
        {
            var matchedDoors = activeResidents
                .Where(r => r.NormalizedResidentName.Length >= 5 &&
                            ContainsWholePhrase(normalized, r.NormalizedResidentName))
                .Select(r => r.DoorNumber)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (matchedDoors.Count == 1 && validDoorNumbers.Contains(matchedDoors[0]))
            {
                return matchedDoors[0];
            }
        }

        return null;
    }

    /// <summary>
    /// Açıklama metninden hedef borç türünü tahmin eder:
    /// - "AIDAT" / "ORTAK GIDER" -> LedgerDebtType.Aidat
    /// - "ISINMA" / "DOGALGAZ" / "YAKIT" / "KALORIMETRE" -> LedgerDebtType.Invoice
    /// - "DEVIR" / "GECMIS DONEM" -> LedgerDebtType.OpeningBalancePrincipal
    /// - "DEMIRBAS" veya diğer / karma -> null (Genel / Diğer - Otomatik FIFO)
    /// </summary>
    public static LedgerDebtType? InferTargetDebtType(string? rawDescription)
    {
        if (string.IsNullOrWhiteSpace(rawDescription))
        {
            return null;
        }

        var normalized = NormalizeTurkishUpper(rawDescription);

        // Demirbaş açıkça belirtildiyse "Diğer / Genel Mahsup" (null) olarak bırakılır.
        if (normalized.Contains("DEMIRBAS", StringComparison.Ordinal))
        {
            return null;
        }

        var hasAidat = normalized.Contains("AIDAT", StringComparison.Ordinal) ||
                       normalized.Contains("ORTAK GIDER", StringComparison.Ordinal);

        var hasHeating = normalized.Contains("ISINMA", StringComparison.Ordinal) ||
                         normalized.Contains("DOGALGAZ", StringComparison.Ordinal) ||
                         normalized.Contains("DOGAL GAZ", StringComparison.Ordinal) ||
                         normalized.Contains("YAKIT", StringComparison.Ordinal) ||
                         normalized.Contains("KALORIMETRE", StringComparison.Ordinal) ||
                         normalized.Contains("PAY OLCER", StringComparison.Ordinal) ||
                         normalized.Contains("PAYOLCER", StringComparison.Ordinal);

        // Hem Aidat hem Isınma aynı açıklamada geçiyorsa genel FIFO'ya (null) bırakılır.
        if (hasAidat && hasHeating)
        {
            return null;
        }

        if (hasAidat)
        {
            return LedgerDebtType.Aidat;
        }

        if (hasHeating)
        {
            return LedgerDebtType.Invoice;
        }

        if (normalized.Contains("DEVIR", StringComparison.Ordinal) ||
            normalized.Contains("GECMIS DONEM", StringComparison.Ordinal))
        {
            return LedgerDebtType.OpeningBalancePrincipal;
        }

        return null;
    }

    private static string? NormalizeDoorCandidate(string digits)
    {
        if (!int.TryParse(digits, out var num) || num <= 0)
        {
            return null;
        }

        return num.ToString();
    }

    private static bool ContainsWholePhrase(string haystack, string needle)
    {
        var idx = haystack.IndexOf(needle, StringComparison.Ordinal);
        while (idx >= 0)
        {
            var beforeOk = idx == 0 || !char.IsLetterOrDigit(haystack[idx - 1]);
            var afterIdx = idx + needle.Length;
            var afterOk = afterIdx >= haystack.Length || !char.IsLetterOrDigit(haystack[afterIdx]);
            if (beforeOk && afterOk)
            {
                return true;
            }

            idx = haystack.IndexOf(needle, idx + 1, StringComparison.Ordinal);
        }

        return false;
    }
}

