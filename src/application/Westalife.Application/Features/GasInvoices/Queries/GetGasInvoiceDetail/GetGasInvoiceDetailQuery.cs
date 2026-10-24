using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.GasInvoices.Queries.GetGasInvoiceDetail;

/// <summary>
/// Tek faturanın tüm düzenlenebilir alanları (Etap 6.7 — "Düzenle" Modal'ının veri kaynağı).
/// </summary>
public sealed record GetGasInvoiceDetailQuery(Guid InvoiceId) : IRequest<GasInvoiceDetailItem>;

public sealed record GasInvoiceDetailItem(
    Guid Id,
    string InvoiceNumber,
    string SubscriberNumber,
    string MasterMeterSerial,
    DateTimeOffset ReadingDateFrom,
    DateTimeOffset ReadingDateTo,
    DateTimeOffset IssuedOn,
    decimal CorrectedVolumeM3,
    decimal TotalEnergyKWh,
    decimal PressureCorrectionFactor,
    decimal ConsumptionAmount,
    decimal ConsumptionVat,
    decimal DistributionAmount,
    decimal DistributionVat,
    decimal TotalAmount,
    string? Note,
    bool HasCalculation,
    bool IsLocked);

public sealed class GetGasInvoiceDetailQueryHandler
    : IRequestHandler<GetGasInvoiceDetailQuery, GasInvoiceDetailItem>
{
    private readonly IApplicationDbContext _db;

    public GetGasInvoiceDetailQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<GasInvoiceDetailItem> Handle(
        GetGasInvoiceDetailQuery request, CancellationToken cancellationToken)
    {
        var invoice = await _db.GasInvoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken)
            ?? throw new DomainException(DomainConstants.GasInvoice.InvoiceNotFound);

        var hasCalculation = await _db.ApartmentInvoices
            .AnyAsync(x => x.GasInvoiceId == invoice.Id && !x.IsSuperseded, cancellationToken);

        return new GasInvoiceDetailItem(
            invoice.Id,
            invoice.InvoiceNumber,
            invoice.SubscriberNumber,
            invoice.MasterMeterSerial,
            invoice.ReadingDateFrom,
            invoice.ReadingDateTo,
            invoice.IssuedOn,
            invoice.CorrectedVolumeM3,
            invoice.TotalEnergyKWh,
            invoice.PressureCorrectionFactor,
            invoice.ConsumptionAmount,
            invoice.ConsumptionVat,
            invoice.DistributionAmount,
            invoice.DistributionVat,
            invoice.TotalAmount,
            invoice.Note,
            hasCalculation,
            invoice.IsLocked);
    }
}