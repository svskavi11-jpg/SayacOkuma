using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Services.Budget;

namespace Westalife.Application.Features.Budget;

internal static class BudgetAvailability
{
    public static async Task<decimal> Available(
        IApplicationDbContext db, Guid itemId, decimal estimatedAmount, CancellationToken cancellationToken)
    {
        var spent = await db.Expenses.AsNoTracking()
            .Where(e => e.BudgetItemId == itemId)
            .SumAsync(e => (decimal?)e.Amount, cancellationToken) ?? 0m;
        var transferIn = await db.BudgetTransfers.AsNoTracking()
            .Where(t => t.ToBudgetItemId == itemId)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;
        var transferOut = await db.BudgetTransfers.AsNoTracking()
            .Where(t => t.FromBudgetItemId == itemId)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;

        return BudgetMath.Remaining(
            BudgetMath.AdjustedEstimate(estimatedAmount, transferIn, transferOut),
            spent);
    }
}
