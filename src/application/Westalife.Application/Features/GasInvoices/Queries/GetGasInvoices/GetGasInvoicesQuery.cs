using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;

namespace Westalife.Application.Features.GasInvoices.Queries.GetGasInvoices;

/// <summary>Fatura listesi (yeniden hesaplama kontrolüyle) — Faz 5 fatura tablosunun veri kaynağı.</summary>
public sealed record GetGasInvoicesQuery : IRequest<IReadOnlyList<GasInvoiceListItem>>;

public sealed record GasInvoiceListItem(
    Guid Id,
    string InvoiceNumber,
    DateTimeOffset PeriodFrom,
    DateTimeOffset PeriodTo,
    decimal TotalAmount,
    bool HasCalculation,
    bool IsLocked,
    DateOnly DueDate,
    bool IsPaid,
    DateOnly? PaidOn);

public sealed class GetGasInvoicesQueryHandler : IRequestHandler<GetGasInvoicesQuery, IReadOnlyList<GasInvoiceListItem>>
{
    private readonly IApplicationDbContext _db;

    public GetGasInvoicesQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<GasInvoiceListItem>> Handle(
        GetGasInvoicesQuery request, CancellationToken cancellationToken)
    {
        var items = await _db.GasInvoices
            .OrderByDescending(i => i.ReadingDateTo)
            .Select(i => new GasInvoiceListItem(
                i.Id,
                i.InvoiceNumber,
                i.ReadingDateFrom,
                i.ReadingDateTo,
                i.TotalAmount,
                _db.ApartmentInvoices.Any(x => x.GasInvoiceId == i.Id && !x.IsSuperseded),
                i.IsLocked,
                i.DueDate,
                i.IsPaid,
                i.PaidOn))
            .ToListAsync(cancellationToken);

        return items;
    }
}
