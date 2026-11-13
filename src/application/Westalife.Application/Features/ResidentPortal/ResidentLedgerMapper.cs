using Westalife.Domain.Constants;
using Westalife.Domain.Services.Ledger;

namespace Westalife.Application.Features.ResidentPortal;

public sealed record ResidentLastPayment(DateOnly Date, decimal Amount);

public sealed record ResidentLedgerRow(
    DateOnly Date,
    string Kind,
    string Description,
    decimal Amount,
    decimal? Remaining,
    Guid? PaymentId = null);

public sealed record ResidentLedgerResponse(
    string DoorNumber,
    string ApartmentTitle,
    DateOnly AsOfDate,
    decimal TotalBalance,
    string BalanceStatus,
    bool HasDebt,
    ResidentLastPayment? LastPayment,
    IReadOnlyList<ResidentLedgerRow> Rows);

public sealed record ResidentInvoiceItem(
    Guid Id,
    string Kind,
    string PeriodLabel,
    DateOnly DueDate,
    decimal Amount,
    decimal Paid,
    decimal Remaining,
    string? PdfUrl);

public static class ResidentLedgerMapper
{
    public static ResidentLedgerResponse Map(LedgerResult ledger)
    {
        var rows = new List<ResidentLedgerRow>();
        var payments = new Dictionary<Guid, (DateOnly Date, decimal Amount)>();

        foreach (var line in ledger.Lines)
        {
            if (line.Type == LedgerDebtType.WaiverOffset)
            {
                if (!line.IsCancelled && line.PrincipalAmount > 0m)
                {
                    rows.Add(new ResidentLedgerRow(
                        line.DueDate,
                        "Mahsup",
                        line.Label ?? "Görev mahsubu",
                        line.PrincipalAmount,
                        null));
                }

                continue;
            }

            if (line.PrincipalAmount != 0m || line.IsCancelled)
            {
                rows.Add(new ResidentLedgerRow(
                    line.PeriodStart ?? line.DueDate,
                    KindOf(line.Type),
                    line.Label ?? KindOf(line.Type),
                    line.IsCancelled ? 0m : line.PrincipalAmount,
                    line.RemainingPrincipal));
            }

            if (line.InterestAccrued != 0m)
            {
                rows.Add(new ResidentLedgerRow(
                    ledger.AsOfDate,
                    "Faiz",
                    $"{line.Label ?? KindOf(line.Type)} gecikme faizi",
                    line.InterestAccrued,
                    null));
            }

            foreach (var allocation in line.Allocations)
            {
                AddPayment(payments, allocation.PaymentId, allocation.PaymentDate, allocation.AllocatedAmount);
                if (allocation.PaymentId != Guid.Empty)
                {
                    rows.Add(new ResidentLedgerRow(
                        allocation.PaymentDate,
                        "Tahsilat",
                        "Tahsilat",
                        allocation.AllocatedAmount,
                        null,
                        allocation.PaymentId));
                }
            }
        }

        foreach (var credit in ledger.Credits ?? [])
        {
            AddPayment(payments, credit.PaymentId, credit.PaymentDate, credit.Amount);
            rows.Add(new ResidentLedgerRow(
                credit.PaymentDate,
                "Tahsilat",
                "Tahsilat",
                credit.Amount,
                null,
                credit.PaymentId));
        }

        rows.Sort(static (left, right) =>
        {
            var byDate = left.Date.CompareTo(right.Date);
            return byDate != 0 ? byDate : Rank(left.Kind).CompareTo(Rank(right.Kind));
        });

        ResidentLastPayment? lastPayment = null;
        if (payments.Count > 0)
        {
            var last = payments.Values.OrderByDescending(item => item.Date).ThenByDescending(item => item.Amount).First();
            lastPayment = new ResidentLastPayment(last.Date, last.Amount);
        }

        var balance = ledger.TotalBalance;
        var status = balance > 0m
            ? DomainConstants.Apartment.BalanceDebtor
            : balance < 0m
                ? DomainConstants.Apartment.BalanceCreditor
                : DomainConstants.Apartment.BalanceSettled;

        return new ResidentLedgerResponse(
            ledger.DoorNumber,
            ledger.ApartmentTitle,
            ledger.AsOfDate,
            balance,
            status,
            balance > 0m,
            lastPayment,
            rows);
    }

    public static string HeatingPdfPath(Guid gasInvoiceId) =>
        $"/resident/invoices/{gasInvoiceId}/pdf?kind=heating";

    private static void AddPayment(
        Dictionary<Guid, (DateOnly Date, decimal Amount)> payments,
        Guid paymentId,
        DateOnly date,
        decimal amount)
    {
        if (paymentId == Guid.Empty || amount == 0m)
        {
            return;
        }

        if (payments.TryGetValue(paymentId, out var existing))
        {
            payments[paymentId] = (date > existing.Date ? date : existing.Date, existing.Amount + amount);
            return;
        }

        payments[paymentId] = (date, amount);
    }

    private static string KindOf(LedgerDebtType type) => type switch
    {
        LedgerDebtType.Invoice => "Isınma",
        LedgerDebtType.Aidat => "Aidat",
        LedgerDebtType.OpeningBalanceInterest => "Faiz",
        LedgerDebtType.OpeningBalancePrincipal => "Devir",
        _ => "Diğer"
    };

    private static int Rank(string kind) => kind switch
    {
        "Devir" => 0,
        "Isınma" => 1,
        "Aidat" => 2,
        "Faiz" => 3,
        "Mahsup" => 4,
        "Tahsilat" => 5,
        _ => 6
    };
}
