using System.Text;
using Westalife.Domain.Common;
using Westalife.Domain.Constants;

namespace Westalife.Domain.Entities;

/// <summary>
/// Sitenin banka hesabı. Bir bankada birden fazla hesap olabilir.
/// Tahsilat ve gider bu karta bağlanır. Devir bakiyesi hesabın açılış parasıdır.
/// </summary>
public class BankAccount : BaseEntity, ITenantEntity
{
    /// <summary>Kaydın ait olduğu site.</summary>
    public Guid TenantId { get; private set; }

    public void AssignTenant(Guid tenantId) => TenantId = tenantId;

    public string BankName { get; private set; } = string.Empty;

    public string AccountName { get; private set; } = string.Empty;

    public string AccountNumber { get; private set; } = string.Empty;

    public string? Iban { get; private set; }

    public decimal OpeningBalance { get; private set; }

    public DateOnly OpeningDate { get; private set; }

    public bool IsActive { get; private set; }

    private BankAccount() { }

    public BankAccount(
        string bankName,
        string accountName,
        string accountNumber,
        string? iban,
        decimal openingBalance,
        DateOnly openingDate)
    {
        Apply(bankName, accountName, accountNumber, iban, openingBalance, openingDate);
        IsActive = true;
    }

    public void Update(
        string bankName,
        string accountName,
        string accountNumber,
        string? iban,
        decimal openingBalance,
        DateOnly openingDate)
    {
        Apply(bankName, accountName, accountNumber, iban, openingBalance, openingDate);
        LastModifiedOn = DateTimeOffset.UtcNow;
    }

    public void SetActive(bool isActive)
    {
        if (IsActive == isActive)
        {
            return;
        }

        IsActive = isActive;
        LastModifiedOn = DateTimeOffset.UtcNow;
    }

    public static bool IsValidIban(string? iban) => TryNormalizeIban(iban, out _);

    public static string NormalizeAccountNumber(string accountNumber)
    {
        var number = accountNumber.Trim();
        if (number.Length == 0)
        {
            throw new ArgumentException(DomainConstants.BankAccount.AccountNumberRequired, nameof(accountNumber));
        }

        if (number.Length > DomainConstants.BankAccount.AccountNumberMaxLength)
        {
            throw new ArgumentException(DomainConstants.BankAccount.AccountNumberTooLong, nameof(accountNumber));
        }

        return number;
    }

    public static string? NormalizeIban(string? iban)
    {
        if (!TryNormalizeIban(iban, out var normalized))
        {
            throw new ArgumentException(DomainConstants.BankAccount.IbanInvalid, nameof(iban));
        }

        return normalized;
    }

    private void Apply(
        string bankName,
        string accountName,
        string accountNumber,
        string? iban,
        decimal openingBalance,
        DateOnly openingDate)
    {
        var bank = RequireText(bankName, DomainConstants.BankAccount.BankNameRequired, DomainConstants.BankAccount.BankNameTooLong, DomainConstants.BankAccount.BankNameMaxLength, nameof(bankName));
        var name = RequireText(accountName, DomainConstants.BankAccount.AccountNameRequired, DomainConstants.BankAccount.AccountNameTooLong, DomainConstants.BankAccount.AccountNameMaxLength, nameof(accountName));

        if (openingBalance < 0m || openingBalance > DomainConstants.BankAccount.AmountMax)
        {
            throw new ArgumentException(DomainConstants.BankAccount.OpeningBalanceInvalid, nameof(openingBalance));
        }

        if (openingDate > DateOnly.FromDateTime(DateTime.Today))
        {
            throw new ArgumentException(DomainConstants.BankAccount.OpeningDateFuture, nameof(openingDate));
        }

        BankName = bank;
        AccountName = name;
        AccountNumber = NormalizeAccountNumber(accountNumber);
        Iban = NormalizeIban(iban);
        OpeningBalance = decimal.Round(openingBalance, DomainConstants.MoneyScale);
        OpeningDate = openingDate;
    }

    private static string RequireText(string value, string required, string tooLong, int max, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(required, paramName);
        }

        var trimmed = value.Trim();
        if (trimmed.Length > max)
        {
            throw new ArgumentException(tooLong, paramName);
        }

        return trimmed;
    }

    private static bool TryNormalizeIban(string? iban, out string? normalized)
    {
        if (string.IsNullOrWhiteSpace(iban))
        {
            normalized = null;
            return true;
        }

        var compact = iban.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        if (compact.Length != DomainConstants.BankAccount.IbanLength || !compact.StartsWith("TR", StringComparison.Ordinal))
        {
            normalized = null;
            return false;
        }

        for (var i = 2; i < compact.Length; i++)
        {
            if (!char.IsDigit(compact[i]))
            {
                normalized = null;
                return false;
            }
        }

        if (Mod97(compact) != 1)
        {
            normalized = null;
            return false;
        }

        normalized = compact;
        return true;
    }

    private static int Mod97(string iban)
    {
        var rearranged = iban[4..] + iban[..4];
        var digits = new StringBuilder(rearranged.Length + 8);
        foreach (var character in rearranged)
        {
            if (char.IsDigit(character))
            {
                digits.Append(character);
            }
            else
            {
                digits.Append(character - 'A' + 10);
            }
        }

        var remainder = 0;
        foreach (var character in digits.ToString())
        {
            remainder = (remainder * 10 + (character - '0')) % 97;
        }

        return remainder;
    }
}
