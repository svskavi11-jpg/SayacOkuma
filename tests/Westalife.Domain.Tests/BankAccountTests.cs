using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Domain.Tests;

public class BankAccountTests
{
    [Fact]
    public void Create_ValidTurkishIban_StoresCompactIban()
    {
        var account = new BankAccount(
            "Garanti",
            "Vadesiz",
            "1234567",
            "TR33 0006 1005 1978 6457 8413 26",
            1000m,
            new DateOnly(2026, 1, 1));

        Assert.Equal("TR330006100519786457841326", account.Iban);
        Assert.Equal(1000m, account.OpeningBalance);
        Assert.True(account.IsActive);
    }

    [Fact]
    public void Create_BlankIban_StaysEmpty()
    {
        var account = new BankAccount("TEB", "POS", "9988", "  ", 0m, new DateOnly(2026, 1, 1));

        Assert.Null(account.Iban);
    }

    [Fact]
    public void Create_InvalidIban_Throws()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            new BankAccount("TEB", "Vadesiz", "1", "TR000000000000000000000000", 0m, new DateOnly(2026, 1, 1)));

        Assert.StartsWith(DomainConstants.BankAccount.IbanInvalid, error.Message);
    }

    [Fact]
    public void Create_FutureOpeningDate_Throws()
    {
        var tomorrow = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        var error = Assert.Throws<ArgumentException>(() =>
            new BankAccount("TEB", "Vadesiz", "1", null, 0m, tomorrow));

        Assert.StartsWith(DomainConstants.BankAccount.OpeningDateFuture, error.Message);
    }

    [Fact]
    public void Create_NegativeOpeningBalance_Throws()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            new BankAccount("TEB", "Vadesiz", "1", null, -1m, new DateOnly(2026, 1, 1)));

        Assert.StartsWith(DomainConstants.BankAccount.OpeningBalanceInvalid, error.Message);
    }
}
