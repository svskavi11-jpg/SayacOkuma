using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;

namespace Westalife.Application.Features.Buildings.Queries.GetBuildingLossReport;

/// <summary>
/// S3 kararı: "Bina Kayıp-Kaçak / Ortak Alan Tüketimi Raporu".
/// Kayıp = faturalanan ana sayaç enerjisi (kWh) − Σ daire kalorimetre tüketimi (dönemsel).
/// Yalnızca yönetim panosunda gösterilir; kat maliki bildirimine girmez.
/// </summary>
public sealed record GetBuildingLossReportQuery(Guid GasInvoiceId) : IRequest<BuildingLossReportResponse>;

public sealed record BuildingLossReportResponse(
    Guid GasInvoiceId,
    decimal MasterMeterEnergyKWh,
    decimal TotalApartmentConsumptionKWh,
    decimal LossKWh,
    decimal LossPercent);

public sealed class GetBuildingLossReportQueryHandler
    : IRequestHandler<GetBuildingLossReportQuery, BuildingLossReportResponse>
{
    private readonly IApplicationDbContext _db;

    public GetBuildingLossReportQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<BuildingLossReportResponse> Handle(
        GetBuildingLossReportQuery request, CancellationToken cancellationToken)
    {
        var invoice = await _db.GasInvoices
            .FirstOrDefaultAsync(i => i.Id == request.GasInvoiceId, cancellationToken)
            ?? throw new Westalife.Domain.Exceptions.NotFoundException("Fatura bulunamadı.");

        var periodStart = invoice.ReadingDateFrom;
        var periodEnd = invoice.ReadingDateTo;

        var apartmentConsumption = await
            (from reading in _db.MeterReadings
             join meter in _db.Meters on reading.MeterId equals meter.Id
             where meter.ApartmentId != null
                && reading.ReadingDateUtc >= periodStart
                && reading.ReadingDateUtc < periodEnd
             select (decimal?)reading.ConsumptionKWh)
            .SumAsync(cancellationToken) ?? 0m;

        var loss = invoice.TotalEnergyKWh - apartmentConsumption;
        var lossPercent = invoice.TotalEnergyKWh == 0m
            ? 0m
            : Math.Round(loss / invoice.TotalEnergyKWh * 100m, 2);

        return new BuildingLossReportResponse(
            invoice.Id,
            invoice.TotalEnergyKWh,
            apartmentConsumption,
            loss,
            lossPercent);
    }
}
