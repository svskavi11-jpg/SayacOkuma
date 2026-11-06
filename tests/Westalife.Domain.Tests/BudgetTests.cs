using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;
using Westalife.Domain.Services.Budget;

namespace Westalife.Domain.Tests;

/// <summary>
/// Faz 6.10: Bütçe aritmetiği (kalan = tahmini − gerçekleşen, %90 uyarı) ve gider kuralları.
/// </summary>
public class BudgetTests
{
    [Fact]
    public void Remaining_IsEstimatedMinusActual_AtKurusPrecision()
    {
        Assert.Equal(100m, BudgetMath.Remaining(1_000m, 900m));
        Assert.Equal(-100.50m, BudgetMath.Remaining(1_000m, 1_100.50m));
    }

    [Fact]
    public void UtilizationPercent_RoundsHalfAwayFromZero()
    {
        Assert.Equal(90m, BudgetMath.UtilizationPercent(1_000m, 900m));
        Assert.Equal(110m, BudgetMath.UtilizationPercent(1_000m, 1_100m));
        Assert.Equal(0m, BudgetMath.UtilizationPercent(0m, 50m));
    }

    [Fact]
    public void Warning_StartsAtNinetyPercent_AndDoesNotBlockOverrun()
    {
        Assert.False(BudgetMath.ReachesWarning(1_000m, 899.99m));
        Assert.True(BudgetMath.ReachesWarning(1_000m, 900m));
        Assert.True(BudgetMath.ReachesWarning(1_000m, 1_500m));
    }

    [Fact]
    public void WarningThreshold_RoundsEstimatedRatioToKurus()
    {
        // 333,33 × 0,90 = 299,997 → eşik 300,00 TL.
        Assert.False(BudgetMath.ReachesWarning(333.33m, 299.99m));
        Assert.True(BudgetMath.ReachesWarning(333.33m, 300m));
    }

    [Fact]
    public void BudgetItem_RejectsEmptyCategory_AndNonPositiveAmount()
    {
        Assert.Throws<ArgumentException>(() => new BudgetItem(2026, "  ", 100m));
        Assert.Throws<ArgumentException>(() => new BudgetItem(2026, "Asansör", 0m));
        Assert.Throws<ArgumentException>(() => new BudgetItem(1999, "Asansör", 100m));
    }

    [Fact]
    public void BudgetItem_RoundsEstimatedAmountToKurus()
    {
        var item = new BudgetItem(2026, "  Temizlik  ", 1500.456m);
        Assert.Equal("Temizlik", item.Category);
        Assert.Equal(1500.46m, item.EstimatedAmount);
    }

    [Fact]
    public void Expense_RequiresEitherBudgetItemOrFreeTextType()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        var missing = Assert.Throws<DomainException>(() => new Expense(today, 10m, null, null, null));
        Assert.Equal(DomainConstants.Budget.ExpenseTypeRequired, missing.Message);

        var both = Assert.Throws<DomainException>(() =>
            new Expense(today, 10m, null, "Tamir", Guid.NewGuid()));
        Assert.Equal(DomainConstants.Budget.ExpenseTypeNotAllowedWithItem, both.Message);
    }

    [Fact]
    public void Expense_RejectsFutureDate_AndStoresFreeTextType()
    {
        var tomorrow = DateOnly.FromDateTime(DateTime.Today).AddDays(1);
        Assert.Throws<ArgumentException>(() => new Expense(tomorrow, 25m, null, "Tamir", null));

        var today = DateOnly.FromDateTime(DateTime.Today);
        var expense = new Expense(today, 25.555m, "  lamba  ", "  Elektrik  ", null);
        Assert.Equal(25.56m, expense.Amount);
        Assert.Equal("lamba", expense.Description);
        Assert.Equal("Elektrik", expense.ExpenseType);
        Assert.Null(expense.BudgetItemId);
    }

    [Fact]
    public void Expense_LinkedToBudgetItem_ClearsFreeTextType()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var itemId = Guid.NewGuid();
        var expense = new Expense(today, 40m, null, "   ", itemId);
        Assert.Equal(itemId, expense.BudgetItemId);
        Assert.Null(expense.ExpenseType);
    }
}
