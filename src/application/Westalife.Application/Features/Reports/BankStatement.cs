using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Services.Reports;

namespace Westalife.Application.Features.Reports;

internal static class BankStatement
{
    public static BankReconciliationReport Build(
        BankAccount account,
        DateOnly from,
        DateOnly to,
        IEnumerable<Payment> payments,
        IReadOnlyDictionary<Guid, string> doors,
        IEnumerable<Expense> expenses,
        IReadOnlyDictionary<Guid, string> categories,
        IEnumerable<BankTransaction> transactions)
    {
        var label = $"{account.BankName} — {account.AccountName}";
        var opening = Position(account, from.AddDays(-1), payments, expenses, transactions);
        var movements = new List<BankMovementRow>();

        if (AuditTrackingMath.InRange(account.OpeningDate, from, to) && account.OpeningBalance != 0m)
        {
            movements.Add(new BankMovementRow(
                account.OpeningDate,
                DomainConstants.AuditReport.AccountOpening,
                DomainConstants.AuditReport.AccountOpening,
                account.OpeningBalance,
                0m));
        }

        foreach (var payment in payments)
        {
            if (payment.IsCancelled || payment.BankAccountId != account.Id)
            {
                continue;
            }

            if (!AuditTrackingMath.InRange(payment.PaymentDate, from, to))
            {
                continue;
            }

            var door = doors.GetValueOrDefault(payment.ApartmentId);
            var description = string.IsNullOrWhiteSpace(door)
                ? payment.Description ?? DomainConstants.Budget.CollectionTitle
                : $"Daire {door} — {payment.Description ?? DomainConstants.Budget.CollectionTitle}";
            movements.Add(new BankMovementRow(
                payment.PaymentDate, "Tahsilat", description, payment.Amount, 0m));
        }

        foreach (var expense in expenses)
        {
            if (expense.BankAccountId != account.Id || !AuditTrackingMath.InRange(expense.ExpenseDate, from, to))
            {
                continue;
            }

            var category = expense.BudgetItemId is { } itemId
                ? categories.GetValueOrDefault(itemId)
                : null;
            var title = category ?? expense.ExpenseType ?? DomainConstants.Budget.ExpenseLabel;
            var description = string.IsNullOrWhiteSpace(expense.Description) ? title : $"{title} — {expense.Description}";
            movements.Add(new BankMovementRow(expense.ExpenseDate, "Gider", description, 0m, expense.Amount));
        }

        foreach (var movement in transactions)
        {
            if (movement.BankAccountId != account.Id || !AuditTrackingMath.InRange(movement.TransactionDate, from, to))
            {
                continue;
            }

            var incoming = movement.Type is BankTransactionType.BankInterest or BankTransactionType.TransferIn;
            var kind = movement.Type switch
            {
                BankTransactionType.BankInterest => "Faiz",
                BankTransactionType.BankFee => "Masraf",
                BankTransactionType.TransferIn => "Virman giriş",
                _ => "Virman çıkış",
            };
            movements.Add(new BankMovementRow(
                movement.TransactionDate,
                kind,
                movement.Description ?? kind,
                incoming ? movement.Amount : 0m,
                incoming ? 0m : movement.Amount));
        }

        movements.Sort(static (a, b) =>
        {
            var byDate = a.Date.CompareTo(b.Date);
            return byDate != 0 ? byDate : string.Compare(a.Kind, b.Kind, StringComparison.Ordinal);
        });

        var moneyIn = movements.Sum(m => m.MoneyIn);
        var moneyOut = movements.Sum(m => m.MoneyOut);
        return new BankReconciliationReport(
            account.Id,
            label,
            from,
            to,
            opening,
            moneyIn,
            moneyOut,
            opening + moneyIn - moneyOut,
            movements);
    }

    private static decimal Position(
        BankAccount account,
        DateOnly asOf,
        IEnumerable<Payment> payments,
        IEnumerable<Expense> expenses,
        IEnumerable<BankTransaction> transactions)
    {
        var value = account.OpeningDate <= asOf ? account.OpeningBalance : 0m;
        value += payments.Where(p => !p.IsCancelled && p.BankAccountId == account.Id && p.PaymentDate <= asOf).Sum(p => p.Amount);
        value -= expenses.Where(e => e.BankAccountId == account.Id && e.ExpenseDate <= asOf).Sum(e => e.Amount);
        foreach (var movement in transactions.Where(t => t.BankAccountId == account.Id && t.TransactionDate <= asOf))
        {
            if (movement.Type is BankTransactionType.BankInterest or BankTransactionType.TransferIn)
            {
                value += movement.Amount;
            }
            else
            {
                value -= movement.Amount;
            }
        }

        return value;
    }
}

public sealed record BankMovementRow(
    DateOnly Date,
    string Kind,
    string Description,
    decimal MoneyIn,
    decimal MoneyOut);

public sealed record BankReconciliationReport(
    Guid BankAccountId,
    string Label,
    DateOnly From,
    DateOnly To,
    decimal Opening,
    decimal MoneyIn,
    decimal MoneyOut,
    decimal Closing,
    IReadOnlyList<BankMovementRow> Movements);
