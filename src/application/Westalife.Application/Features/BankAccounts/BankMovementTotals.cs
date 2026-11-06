using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Entities;

namespace Westalife.Application.Features.BankAccounts;

/// <summary>
/// Güncel bakiye = devir + tahsilat + faiz + virman giriş − gider − masraf − virman çıkış.
/// </summary>
internal static class BankMovementTotals
{
    public readonly record struct Flow(decimal Incoming, decimal Outgoing);

    public static async Task<Dictionary<Guid, Flow>> ByAccountAsync(
        IApplicationDbContext db,
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken cancellationToken)
    {
        var incoming = new Dictionary<Guid, decimal>();
        var outgoing = new Dictionary<Guid, decimal>();
        if (accountIds.Count == 0)
        {
            return new Dictionary<Guid, Flow>();
        }

        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.BankAccountId != null && accountIds.Contains(p.BankAccountId.Value) && !p.IsCancelled)
            .GroupBy(p => p.BankAccountId)
            .Select(g => new { Id = g.Key, Total = g.Sum(p => p.Amount) })
            .ToListAsync(cancellationToken);
        foreach (var row in payments)
        {
            Add(incoming, row.Id!.Value, row.Total);
        }

        var expenses = await db.Expenses.AsNoTracking()
            .Where(e => e.BankAccountId != null && accountIds.Contains(e.BankAccountId.Value))
            .GroupBy(e => e.BankAccountId)
            .Select(g => new { Id = g.Key, Total = g.Sum(e => e.Amount) })
            .ToListAsync(cancellationToken);
        foreach (var row in expenses)
        {
            Add(outgoing, row.Id!.Value, row.Total);
        }

        var transactions = await db.BankTransactions.AsNoTracking()
            .Where(t => accountIds.Contains(t.BankAccountId))
            .GroupBy(t => new { t.BankAccountId, t.Type })
            .Select(g => new { g.Key.BankAccountId, g.Key.Type, Total = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken);
        foreach (var row in transactions)
        {
            if (row.Type is BankTransactionType.BankInterest or BankTransactionType.TransferIn)
            {
                Add(incoming, row.BankAccountId, row.Total);
            }
            else
            {
                Add(outgoing, row.BankAccountId, row.Total);
            }
        }

        return Merge(incoming, outgoing);
    }

    public static async Task<Dictionary<int, Flow>> ByMonthAsync(
        IApplicationDbContext db,
        int year,
        CancellationToken cancellationToken)
    {
        var incoming = new Dictionary<int, decimal>();
        var outgoing = new Dictionary<int, decimal>();

        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.BankAccountId != null && !p.IsCancelled && p.PaymentDate.Year == year)
            .GroupBy(p => p.PaymentDate.Month)
            .Select(g => new { Month = g.Key, Total = g.Sum(p => p.Amount) })
            .ToListAsync(cancellationToken);
        foreach (var row in payments)
        {
            Add(incoming, row.Month, row.Total);
        }

        var expenses = await db.Expenses.AsNoTracking()
            .Where(e => e.BankAccountId != null && e.ExpenseDate.Year == year)
            .GroupBy(e => e.ExpenseDate.Month)
            .Select(g => new { Month = g.Key, Total = g.Sum(e => e.Amount) })
            .ToListAsync(cancellationToken);
        foreach (var row in expenses)
        {
            Add(outgoing, row.Month, row.Total);
        }

        var transactions = await db.BankTransactions.AsNoTracking()
            .Where(t => t.TransactionDate.Year == year)
            .GroupBy(t => new { t.TransactionDate.Month, t.Type })
            .Select(g => new { g.Key.Month, g.Key.Type, Total = g.Sum(t => t.Amount) })
            .ToListAsync(cancellationToken);
        foreach (var row in transactions)
        {
            if (row.Type is BankTransactionType.BankInterest or BankTransactionType.TransferIn)
            {
                Add(incoming, row.Month, row.Total);
            }
            else
            {
                Add(outgoing, row.Month, row.Total);
            }
        }

        return Merge(incoming, outgoing);
    }

    private static void Add<TKey>(Dictionary<TKey, decimal> totals, TKey key, decimal amount)
        where TKey : notnull
    {
        totals[key] = totals.GetValueOrDefault(key) + amount;
    }

    private static Dictionary<TKey, Flow> Merge<TKey>(Dictionary<TKey, decimal> incoming, Dictionary<TKey, decimal> outgoing)
        where TKey : notnull
    {
        var result = new Dictionary<TKey, Flow>();
        foreach (var key in incoming.Keys.Union(outgoing.Keys))
        {
            result[key] = new Flow(incoming.GetValueOrDefault(key), outgoing.GetValueOrDefault(key));
        }

        return result;
    }
}
