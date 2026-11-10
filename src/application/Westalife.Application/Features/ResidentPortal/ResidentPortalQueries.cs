using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Payments.Queries.GetLedger;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;
using Westalife.Domain.Services.Ledger;

namespace Westalife.Application.Features.ResidentPortal;

public sealed record GetResidentLedgerQuery : IRequest<ResidentLedgerResponse>;

public sealed class GetResidentLedgerQueryHandler : IRequestHandler<GetResidentLedgerQuery, ResidentLedgerResponse>
{
    private readonly IResidentAccess _access;
    private readonly IMediator _mediator;

    public GetResidentLedgerQueryHandler(IResidentAccess access, IMediator mediator)
    {
        _access = access;
        _mediator = mediator;
    }

    public async Task<ResidentLedgerResponse> Handle(GetResidentLedgerQuery request, CancellationToken cancellationToken)
    {
        var scope = await _access.RequireAsync(cancellationToken);
        var ledger = await _mediator.Send(new GetLedgerQuery(scope.ApartmentId), cancellationToken);
        return ResidentLedgerMapper.Map(ledger);
    }
}

public sealed record GetResidentInvoicesQuery : IRequest<IReadOnlyList<ResidentInvoiceItem>>;

public sealed class GetResidentInvoicesQueryHandler : IRequestHandler<GetResidentInvoicesQuery, IReadOnlyList<ResidentInvoiceItem>>
{
    private readonly IResidentAccess _access;
    private readonly IApplicationDbContext _db;
    private readonly IMediator _mediator;

    public GetResidentInvoicesQueryHandler(IResidentAccess access, IApplicationDbContext db, IMediator mediator)
    {
        _access = access;
        _db = db;
        _mediator = mediator;
    }

    public async Task<IReadOnlyList<ResidentInvoiceItem>> Handle(
        GetResidentInvoicesQuery request,
        CancellationToken cancellationToken)
    {
        var scope = await _access.RequireAsync(cancellationToken);
        var ledger = await _mediator.Send(new GetLedgerQuery(scope.ApartmentId), cancellationToken);
        var lines = ledger.Lines.ToDictionary(line => line.SourceId);

        var heating = await _db.ApartmentInvoices
            .AsNoTracking()
            .Include(invoice => invoice.GasInvoice)
            .Where(invoice => invoice.ApartmentId == scope.ApartmentId && !invoice.IsSuperseded)
            .ToListAsync(cancellationToken);

        var aidat = await _db.AidatCharges
            .AsNoTracking()
            .Include(charge => charge.AidatInvoice)
            .Where(charge => charge.ApartmentId == scope.ApartmentId && charge.AidatInvoice.IsCancelled == false)
            .ToListAsync(cancellationToken);

        var items = new List<ResidentInvoiceItem>(heating.Count + aidat.Count);
        items.AddRange(heating.Select(invoice => MapHeating(invoice, lines)));
        items.AddRange(aidat.Select(charge => MapAidat(charge, lines)));
        return items.OrderByDescending(item => item.DueDate).ThenBy(item => item.Kind).ToList();
    }

    private static ResidentInvoiceItem MapHeating(
        ApartmentInvoice invoice,
        Dictionary<string, LedgerLine> lines)
    {
        lines.TryGetValue(invoice.Id.ToString(), out var line);
        var gas = invoice.GasInvoice;
        var due = gas?.DueDate ?? DateOnly.FromDateTime(invoice.CalculatedOnUtc.DateTime);
        return new ResidentInvoiceItem(
            invoice.GasInvoiceId,
            "Heating",
            line?.Label ?? gas?.InvoiceNumber ?? "Isınma",
            due,
            invoice.TotalAmount,
            line?.PaidAmount ?? 0m,
            (line?.RemainingPrincipal ?? invoice.TotalAmount) + (line?.InterestAccrued ?? 0m),
            ResidentLedgerMapper.HeatingPdfPath(invoice.GasInvoiceId));
    }

    private static ResidentInvoiceItem MapAidat(AidatCharge charge, Dictionary<string, LedgerLine> lines)
    {
        lines.TryGetValue(charge.Id.ToString(), out var line);
        var invoice = charge.AidatInvoice;
        var due = invoice?.DueDate ?? DateOnly.FromDateTime(charge.CreatedOn.UtcDateTime);
        var label = line?.Label;
        if (string.IsNullOrWhiteSpace(label))
        {
            label = string.IsNullOrWhiteSpace(invoice?.Title) ? "Aidat" : invoice.Title;
        }

        return new ResidentInvoiceItem(
            charge.Id,
            "Aidat",
            label,
            due,
            charge.Amount,
            line?.PaidAmount ?? 0m,
            (line?.RemainingPrincipal ?? charge.Amount) + (line?.InterestAccrued ?? 0m),
            null);
    }
}

public sealed record ResidentPdfFile(string FileName, byte[] Content);

public sealed record GetResidentHeatingPdfQuery(Guid GasInvoiceId) : IRequest<ResidentPdfFile>;

public sealed class GetResidentHeatingPdfQueryHandler : IRequestHandler<GetResidentHeatingPdfQuery, ResidentPdfFile>
{
    private readonly IResidentAccess _access;
    private readonly IApplicationDbContext _db;
    private readonly INoticePdfGenerator _pdf;

    public GetResidentHeatingPdfQueryHandler(
        IResidentAccess access,
        IApplicationDbContext db,
        INoticePdfGenerator pdf)
    {
        _access = access;
        _db = db;
        _pdf = pdf;
    }

    public async Task<ResidentPdfFile> Handle(GetResidentHeatingPdfQuery request, CancellationToken cancellationToken)
    {
        var scope = await _access.RequireAsync(cancellationToken);
        var owns = await _db.ApartmentInvoices
            .AsNoTracking()
            .AnyAsync(
                invoice => invoice.GasInvoiceId == request.GasInvoiceId
                    && invoice.ApartmentId == scope.ApartmentId
                    && !invoice.IsSuperseded,
                cancellationToken);

        if (!owns)
        {
            throw new NotFoundException("Isınma faturası", request.GasInvoiceId);
        }

        var gas = await _db.GasInvoices
            .AsNoTracking()
            .FirstAsync(invoice => invoice.Id == request.GasInvoiceId, cancellationToken);

        var from = gas.ReadingDateFrom.ToString("yyyyMM");
        var to = gas.ReadingDateTo.ToString("yyyyMM");
        var period = from == to ? from : $"{from}-{to}";
        var content = await _pdf.GenerateApartmentNoticeAsync(gas.Id, scope.ApartmentId, cancellationToken);
        return new ResidentPdfFile($"Isinma-Bildirimi-{period}.pdf", content);
    }
}
