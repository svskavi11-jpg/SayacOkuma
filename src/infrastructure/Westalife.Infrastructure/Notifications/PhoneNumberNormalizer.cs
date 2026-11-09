namespace Westalife.Infrastructure.Notifications;

/// <summary>Yerel telefonu Netgsm ve WhatsApp'ın beklediği 90XXXXXXXXXX biçimine çevirir.</summary>
public static class PhoneNumberNormalizer
{
    public static string? ToTurkeyGsm(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var digits = new string(raw.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("00", StringComparison.Ordinal))
        {
            digits = digits[2..];
        }

        if (digits.Length == 11 && digits.StartsWith('0'))
        {
            digits = "9" + digits;
        }
        else if (digits.Length == 10)
        {
            digits = "90" + digits;
        }

        return digits.Length is >= 11 and <= 15 ? digits : null;
    }
}
