using System.Globalization;
using Westalife.Domain.Constants;

namespace Westalife.Domain.Services;

/// <summary>Site ve yıl bazında makbuz sırası. Biçim: 2026-0001.</summary>
public static class ReceiptNumberSequence
{
    public static string Normalize(string receiptNumber)
    {
        if (string.IsNullOrWhiteSpace(receiptNumber))
        {
            throw new ArgumentException("Makbuz numarası boş olamaz.", nameof(receiptNumber));
        }

        var text = receiptNumber.Trim();
        if (text.Length > DomainConstants.Payment.ReceiptNumberMaxLength)
        {
            throw new ArgumentException(DomainConstants.Payment.ReceiptNumberTooLong, nameof(receiptNumber));
        }

        return text;
    }

    public static string Next(IEnumerable<string> existing, int year)
    {
        var prefix = $"{year}-";
        var max = 0;
        foreach (var value in existing)
        {
            if (!value.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (int.TryParse(value.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
                && number > max)
            {
                max = number;
            }
        }

        var next = max + 1;
        return next < 10_000 ? $"{prefix}{next:0000}" : $"{prefix}{next}";
    }
}
