using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;

namespace Westalife.Domain.Tests;

public class BankTransactionTests
{
    private static readonly DateOnly Opening = new(2026, 1, 1);
    private static readonly DateOnly Day = new(2026, 6, 1);

    [Fact]
    public void CreateInterest_StoresPositiveAmountWithoutCounterAccount()
    {
        var interest = BankTransaction.CreateInterest(Guid.NewGuid(), Opening, 150.5m, Day, "  Haziran faizi  ");

        Assert.Equal(BankTransactionType.BankInterest, interest.Type);
        Assert.Equal(150.5m, interest.Amount);
        Assert.Equal("Haziran faizi", interest.Description);
        Assert.Null(interest.TargetBankAccountId);
        Assert.Null(interest.TransferGroupId);
    }

    [Fact]
    public void CreateFee_StoresFee()
    {
        var fee = BankTransaction.CreateFee(Guid.NewGuid(), Opening, 25m, Day, null);

        Assert.Equal(BankTransactionType.BankFee, fee.Type);
        Assert.Null(fee.Description);
    }

    [Fact]
    public void CreateTransfer_WritesBothLegsWithSameGroup()
    {
        var source = Guid.NewGuid();
        var target = Guid.NewGuid();

        var (outgoing, incoming) = BankTransaction.CreateTransfer(source, Opening, target, Opening, 1000m, Day, "POS aktarım");

        Assert.Equal(BankTransactionType.TransferOut, outgoing.Type);
        Assert.Equal(source, outgoing.BankAccountId);
        Assert.Equal(target, outgoing.TargetBankAccountId);
        Assert.Equal(BankTransactionType.TransferIn, incoming.Type);
        Assert.Equal(target, incoming.BankAccountId);
        Assert.Equal(source, incoming.TargetBankAccountId);
        Assert.Equal(outgoing.TransferGroupId, incoming.TransferGroupId);
        Assert.NotNull(outgoing.TransferGroupId);
        Assert.Equal(1000m, incoming.Amount);
    }

    [Fact]
    public void CreateTransfer_SameAccount_Throws()
    {
        var account = Guid.NewGuid();

        var error = Assert.Throws<DomainException>(() =>
            BankTransaction.CreateTransfer(account, Opening, account, Opening, 10m, Day, null));

        Assert.Equal(DomainConstants.BankTransaction.SameAccount, error.Message);
    }

    [Fact]
    public void Create_ZeroAmount_Throws()
    {
        var error = Assert.Throws<DomainException>(() =>
            BankTransaction.CreateFee(Guid.NewGuid(), Opening, 0m, Day, null));

        Assert.Equal(DomainConstants.BankTransaction.AmountInvalid, error.Message);
    }

    [Fact]
    public void Create_FutureDate_Throws()
    {
        var tomorrow = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        var error = Assert.Throws<DomainException>(() =>
            BankTransaction.CreateInterest(Guid.NewGuid(), Opening, 1m, tomorrow, null));

        Assert.Equal(DomainConstants.BankTransaction.DateFuture, error.Message);
    }

    [Fact]
    public void Create_BeforeOpening_Throws()
    {
        var error = Assert.Throws<DomainException>(() =>
            BankTransaction.CreateFee(Guid.NewGuid(), new DateOnly(2026, 6, 1), 1m, new DateOnly(2026, 5, 31), null));

        Assert.Equal(DomainConstants.BankTransaction.DateBeforeOpening, error.Message);
    }
}
