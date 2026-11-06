using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Entities;
using Westalife.Domain.Services.Ledger;

namespace Westalife.Application.Features.Aidat.Queries.GetAidatInvoices;

/// <summary>
/// Faz 6.9: Geçmiş aidat dönemlerini tahsilat oranlarıyla listeler.
/// Tahsilat, <see cref="LedgerService"/> FIFO motoru üzerinden hesaplanır
/// (devir + fatura + aidat tek kronolojik kuyrukta) — cari hesapla birebir tutarlıdır.
/// </summary>
public sealed record GetAidatInvoicesQuery(DateOnly? AsOfDate = null) : IRequest<IReadOnlyList<AidatInvoiceListItem>>;

public sealed record AidatInvoiceListItem(
    Guid Id,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly DueDate,
    AidatDistributionMethod DistributionMethod,
    int ChargeCount,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal RemainingAmount,
    decimal CollectionRatio,
    DateTimeOffset CreatedOn,
    AidatChargeType ChargeType,
    string? Title,
    decimal? TargetAmount,
    decimal FundSpent,
    decimal FundBalance,
    bool IsCancelled = false,
    DateTimeOffset? CancelledAt = null,
    bool IsLocked = false);

public sealed class GetAidatInvoicesQueryHandler : IRequestHandler<GetAidatInvoicesQuery, IReadOnlyList<AidatInvoiceListItem>>
{
    private readonly IApplicationDbContext _db;

    public GetAidatInvoicesQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<AidatInvoiceListItem>> Handle(
        GetAidatInvoicesQuery request, CancellationToken cancellationToken)
    {
        var invoices = await _db.AidatInvoices
            .AsNoTracking()
            .OrderByDescending(i => i.PeriodStart)
            .ThenByDescending(i => i.PeriodEnd)
            .ToListAsync(cancellationToken);

        if (invoices.Count == 0)
        {
            return [];
        }

        var invoiceIds = invoices.Select(i => i.Id).ToList();

        // Dönemin daire borç satırları (vade için AidatInvoice navigation ile)
        var charges = await _db.AidatCharges
            .AsNoTracking()
            .Include(c => c.AidatInvoice)
            .Where(c => invoiceIds.Contains(c.AidatInvoiceId))
            .ToListAsync(cancellationToken);

        var chargesByInvoice = charges
            .GroupBy(c => c.AidatInvoiceId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var apartmentIds = charges.Select(c => c.ApartmentId).Distinct().ToList();

        // FIFO hesabı için daire + fatura + ödeme verileri
        var apartments = await _db.Apartments
            .AsNoTracking()
            .Where(a => apartmentIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var apartmentInvoices = await _db.ApartmentInvoices
            .AsNoTracking()
            .Include(i => i.GasInvoice)
            .Where(i => !i.IsSuperseded && apartmentIds.Contains(i.ApartmentId))
            .ToListAsync(cancellationToken);

        var payments = await _db.Payments
            .AsNoTracking()
            .Where(p => apartmentIds.Contains(p.ApartmentId))
            .ToListAsync(cancellationToken);

        var settings = await _db.AppSettings
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var annualRate = settings?.InterestRate ?? 0m;
        var asOf = request.AsOfDate ?? DateOnly.FromDateTime(DateTime.Today);

        // Daire bazında FIFO çalıştır; her aidat satırının ödenen tutarını topla.
        // SourceId = AidatCharge.Id (string) — LedgerService ile aynı sözleşme.
        var chargeIds = charges.Select(c => c.Id).ToList();
        var waivers = await _db.WaiverOffsets.AsNoTracking()
            .Where(w => chargeIds.Contains(w.AidatChargeId))
            .ToListAsync(cancellationToken);
        var waivedByCharge = waivers
            .Where(w => !w.IsCancelled)
            .GroupBy(w => w.AidatChargeId)
            .ToDictionary(g => g.Key, g => g.Sum(w => w.Amount));

        var paidByCharge = new Dictionary<string, decimal>();

        foreach (var apartmentGroup in charges.GroupBy(c => c.ApartmentId))
        {
            if (!apartments.TryGetValue(apartmentGroup.Key, out var apartment))
            {
                continue;
            }

            var chargesForLedger = apartmentGroup.ToList();

            var result = LedgerService.Calculate(
                apartment,
                apartmentInvoices.Where(i => i.ApartmentId == apartmentGroup.Key),
                payments.Where(p => p.ApartmentId == apartmentGroup.Key),
                annualRate,
                asOf,
                chargesForLedger,
                waivers.Where(w => w.ApartmentId == apartmentGroup.Key));

            foreach (var line in result.Lines.Where(l => l.Type == LedgerDebtType.Aidat))
            {
                paidByCharge[line.SourceId] = paidByCharge.GetValueOrDefault(line.SourceId) + line.PaidAmount;
            }
        }

        var fundExpenseRows = await _db.Expenses.AsNoTracking()
            .Where(e => e.FundAidatInvoiceId != null && invoiceIds.Contains(e.FundAidatInvoiceId.Value))
            .Select(e => new { FundId = e.FundAidatInvoiceId!.Value, e.Amount })
            .ToListAsync(cancellationToken);
        var spentByFund = fundExpenseRows
            .GroupBy(e => e.FundId)
            .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

        var years = invoices.SelectMany(i => new[] { i.PeriodStart.Year, i.PeriodEnd.Year }).Distinct().ToList();
        var lockedYears = (await _db.BudgetPeriods.AsNoTracking()
                .Where(p => years.Contains(p.Year) && p.IsLocked)
                .Select(p => p.Year)
                .ToListAsync(cancellationToken))
            .ToHashSet();

        var items = new List<AidatInvoiceListItem>(invoices.Count);

        foreach (var invoice in invoices)
        {
            var invoiceCharges = chargesByInvoice.GetValueOrDefault(invoice.Id) ?? [];
            var total = invoice.IsCancelled ? 0m : invoiceCharges.Sum(c => c.Amount);
            var paid = invoice.IsCancelled ? 0m : invoiceCharges.Sum(c => paidByCharge.GetValueOrDefault(c.Id.ToString()));
            var waived = invoice.IsCancelled ? 0m : invoiceCharges.Sum(c => waivedByCharge.GetValueOrDefault(c.Id));
            var remaining = Math.Max(0m, total - paid - waived);
            var progressBase = invoice.ChargeType == AidatChargeType.OzelToplama && invoice.TargetAmount is > 0m
                ? invoice.TargetAmount.Value
                : total;
            var ratio = progressBase > 0m ? Math.Min(100m, decimal.Round(paid / progressBase * 100m, 1)) : 0m;
            var spent = spentByFund.GetValueOrDefault(invoice.Id);
            var isPeriodLocked = lockedYears.Contains(invoice.PeriodStart.Year) || lockedYears.Contains(invoice.PeriodEnd.Year);

            items.Add(new AidatInvoiceListItem(
                invoice.Id,
                invoice.PeriodStart,
                invoice.PeriodEnd,
                invoice.DueDate,
                invoice.DistributionMethod,
                invoiceCharges.Count,
                invoice.IsCancelled ? invoice.TotalAmount : total,
                paid,
                remaining,
                ratio,
                invoice.CreatedOn,
                invoice.ChargeType,
                invoice.Title,
                invoice.TargetAmount,
                spent,
                decimal.Round(paid - spent, 2),
                invoice.IsCancelled,
                invoice.CancelledAt,
                isPeriodLocked));
        }

        return items;
    }
}
