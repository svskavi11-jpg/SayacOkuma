using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;

namespace Westalife.Application.Features.Payments.Queries.GetCashFlow;

/// <summary>
/// Faz 6.8: Nakit akış raporu.
/// GELİR: Dairelerin ödediği tahsilatlar (Payment.PaymentDate bazlı).
/// GİDER: Binanın doğalgaz firmasına ödediği faturalar (GasInvoice.PaidOn bazlı).
/// Her ikisi de gerçek ödeme tarihine göre dönemlenir (tahakkuk değil, nakit esası).
/// </summary>
public sealed record GetCashFlowQuery(
    DateOnly DateFrom,
    DateOnly DateTo) : IRequest<CashFlowResult>;

public sealed record CashFlowResult(
    DateOnly DateFrom,
    DateOnly DateTo,

    /// <summary>Binanın doğalgaz firmalarına ödediği gider toplam (fatura PaidOn tarihleri)</summary>
    decimal TotalExpense,

    /// <summary>Dairelerden tahsil edilen gelir toplam (Payment.PaymentDate tarihleri)</summary>
    decimal TotalIncome,

    /// <summary>Net = Gelir - Gider (pozitif = fazla tahsilat, negatif = açık)</summary>
    decimal NetBalance,

    IReadOnlyList<CashFlowLine> Lines);

public sealed record CashFlowLine(
    DateOnly Date,
    string Description,
    CashFlowDirection Direction,
    decimal Amount,
    string? Reference);

public enum CashFlowDirection
{
    Income = 1,   // Daireden gelen tahsilat
    Expense = 2   // Doğalgaz firmasına giden ödeme
}

public sealed class GetCashFlowQueryHandler : IRequestHandler<GetCashFlowQuery, CashFlowResult>
{
    private readonly IApplicationDbContext _db;

    public GetCashFlowQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<CashFlowResult> Handle(GetCashFlowQuery request, CancellationToken cancellationToken)
    {
        // ── Gelirler: aktif tahsilatlar ───────────────────────────────────────
        var incomeLines = await _db.Payments
            .AsNoTracking()
            .Include(p => p.Apartment)
            .Where(p => !p.IsCancelled
                && p.PaymentDate >= request.DateFrom
                && p.PaymentDate <= request.DateTo)
            .OrderBy(p => p.PaymentDate)
            .Select(p => new CashFlowLine(
                p.PaymentDate,
                $"Tahsilat – Daire {p.Apartment.DoorNumber} ({p.Apartment.Title})",
                CashFlowDirection.Income,
                p.Amount,
                p.Description))
            .ToListAsync(cancellationToken);

        // ── Giderler: ödenen doğalgaz faturaları ──────────────────────────────
        var expenseLines = await _db.GasInvoices
            .AsNoTracking()
            .Where(i => i.IsPaid
                && i.PaidOn >= request.DateFrom
                && i.PaidOn <= request.DateTo)
            .OrderBy(i => i.PaidOn)
            .Select(i => new CashFlowLine(
                i.PaidOn!.Value,
                $"Doğalgaz Faturası – {i.InvoiceNumber}",
                CashFlowDirection.Expense,
                i.TotalAmount,
                i.InvoiceNumber))
            .ToListAsync(cancellationToken);

        var allLines = incomeLines
            .Concat(expenseLines)
            .OrderBy(l => l.Date)
            .ThenBy(l => l.Direction)
            .ToList();

        var totalIncome = incomeLines.Sum(l => l.Amount);
        var totalExpense = expenseLines.Sum(l => l.Amount);

        return new CashFlowResult(
            request.DateFrom,
            request.DateTo,
            TotalExpense: totalExpense,
            TotalIncome: totalIncome,
            NetBalance: totalIncome - totalExpense,
            Lines: allLines.AsReadOnly());
    }
}
