using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common;
using Westalife.Application.Common.Interfaces;

namespace Westalife.Application.Features.GasInvoices.Queries.GetCalculationResult;

/// <summary>Bir faturanın güncel (geçerli) daire borç listesi — Faz 4 raporlama ekranının veri kaynağı.</summary>
public sealed record GetCalculationResultQuery(Guid GasInvoiceId) : IRequest<GetCalculationResultResponse>;

public sealed record GetCalculationResultResponse(
    Guid GasInvoiceId,
    string InvoiceNumber,
    decimal InvoiceTotalAmount,
    decimal TotalDistributedAmount,
    bool Reconciled,
    IReadOnlyList<CalculationRow> Rows);

public sealed record CalculationRow(
    Guid ApartmentInvoiceId,
    Guid ApartmentId,
    string DoorNumber,
    string ApartmentTitle,
    decimal SquareMetersUsed,
    decimal ConsumptionKWhUsed,
    decimal FixedAmount,
    decimal ConsumptionAmount,
    decimal TotalAmount,
    bool IsMeterReadFailed);

public sealed class GetCalculationResultQueryHandler
    : IRequestHandler<GetCalculationResultQuery, GetCalculationResultResponse>
{
    private readonly IApplicationDbContext _db;

    public GetCalculationResultQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<GetCalculationResultResponse> Handle(
        GetCalculationResultQuery request, CancellationToken cancellationToken)
    {
        var invoice = await _db.GasInvoices
            .FirstOrDefaultAsync(i => i.Id == request.GasInvoiceId, cancellationToken)
            ?? throw new Westalife.Domain.Exceptions.NotFoundException("Fatura bulunamadı.");

        var rawRows = await
            (from row in _db.ApartmentInvoices
             join apartment in _db.Apartments on row.ApartmentId equals apartment.Id
             where row.GasInvoiceId == invoice.Id && !row.IsSuperseded
             // Sıralama bellekte sayısal yapılır (aşağıda).
             select new CalculationRow(
                 row.Id,
                 row.ApartmentId,
                 apartment.DoorNumber,
                 apartment.Title,
                 row.SquareMetersUsed,
                 row.ConsumptionKWhUsed,
                 row.FixedAmount,
                 row.ConsumptionAmount,
                 row.TotalAmount,
                 row.IsMeterReadFailed))
            .ToListAsync(cancellationToken);

        var rows = rawRows.OrderByDoorNumber(r => r.DoorNumber).ToList();

        var total = rows.Sum(r => r.TotalAmount);

        return new GetCalculationResultResponse(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.TotalAmount,
            total,
            total == invoice.TotalAmount,
            rows);
    }
}
