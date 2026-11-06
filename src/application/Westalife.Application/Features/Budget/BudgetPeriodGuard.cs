using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Budget;

internal static class BudgetPeriodGuard
{
    public static async Task EnsureOpen(IApplicationDbContext db, int year, CancellationToken cancellationToken)
    {
        var locked = await db.BudgetPeriods.AsNoTracking()
            .AnyAsync(p => p.Year == year && p.IsLocked, cancellationToken);

        if (locked)
        {
            throw new DomainException(DomainConstants.Budget.YearLocked);
        }
    }
}
