using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;
using Westalife.Domain.Services.Budget;

namespace Westalife.Domain.Tests;

public class BudgetFlexibilityTests
{
    [Fact]
    public void Expense_StoresDocumentNumber_AndFund()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var fundId = Guid.NewGuid();
        var expense = new Expense(today, 125.5m, "lamba", "Elektrik", null, "  FTR-15  ", fundId);

        Assert.Equal("FTR-15", expense.DocumentNumber);
        Assert.Equal(fundId, expense.FundAidatInvoiceId);
    }

    [Fact]
    public void Expense_RejectsLongDocumentNumber()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var tooLong = new string('A', DomainConstants.Budget.DocumentNumberMaxLength + 1);
        var ex = Assert.Throws<ArgumentException>(() => new Expense(today, 10m, null, "Tur", null, tooLong));
        Assert.Contains(DomainConstants.Budget.DocumentNumberTooLong, ex.Message);
    }

    [Fact]
    public void SpecialCollection_RequiresTitleAndTarget()
    {
        var invoice = SampleInvoice();
        var missing = Assert.Throws<ArgumentException>(() => invoice.MarkAsSpecialCollection("  ", 100m));
        Assert.Contains(DomainConstants.AidatInvoice.FundTitleRequired, missing.Message);

        invoice.MarkAsSpecialCollection("  Cati Onarimi Fonu  ", 200_000m);
        Assert.Equal(AidatChargeType.OzelToplama, invoice.ChargeType);
        Assert.Equal("Cati Onarimi Fonu", invoice.Title);
        Assert.Equal(200_000m, invoice.TargetAmount);
    }

    [Fact]
    public void BudgetPeriod_LockAndUnlock()
    {
        var period = new BudgetPeriod(2026);
        Assert.False(period.IsLocked);

        period.Lock();
        Assert.True(period.IsLocked);
        Assert.NotNull(period.LockedOn);

        period.Unlock();
        Assert.False(period.IsLocked);
        Assert.Null(period.LockedOn);
    }

    [Fact]
    public void Transfer_RejectsSameItem()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var id = Guid.NewGuid();
        var ex = Assert.Throws<DomainException>(() => new BudgetTransfer(today, 50m, id, id, null));
        Assert.Equal(DomainConstants.Budget.TransferSameItem, ex.Message);
    }

    [Fact]
    public void Balance_IsEstimatedIncomeMinusEstimatedExpense()
    {
        Assert.Equal(25_000m, BudgetMath.Balance(100_000m, 75_000m));
        Assert.Equal(200m, BudgetMath.AdjustedEstimate(1_000m, 300m, 1_100m));
    }

    private static AidatInvoice SampleInvoice() =>
        new(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), new DateOnly(2026, 2, 15), AidatDistributionMethod.Esit);
}
