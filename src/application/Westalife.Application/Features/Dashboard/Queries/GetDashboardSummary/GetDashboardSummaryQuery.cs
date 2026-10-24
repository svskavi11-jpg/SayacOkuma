using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;

namespace Westalife.Application.Features.Dashboard.Queries.GetDashboardSummary;

/// <summary>
/// Yönetim panosu özeti (tek istek): bina, daire/sayaç sayıları, son fatura durumu,
/// kayıp-kaçak yüzdesi (S3) ve okunamayan daire sayısı.
/// </summary>
public sealed record GetDashboardSummaryQuery : IRequest<DashboardSummaryResponse>;

public sealed record DashboardSummaryResponse(
    string BuildingName,
    int ApartmentCount,
    int ActiveMeterCount,
    Guid? LastInvoiceId,
    string? LastInvoiceNumber,
    decimal? LastInvoiceTotalAmount,
    DateTimeOffset? LastInvoicePeriodFrom,
    DateTimeOffset? LastInvoicePeriodTo,
    bool? LastInvoiceReconciled,
    decimal? LastLossPercent,
    int LastCalculationFailedMeterCount);

public sealed class GetDashboardSummaryQueryHandler
    : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryResponse>
{
    private readonly IApplicationDbContext _db;

    public GetDashboardSummaryQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<DashboardSummaryResponse> Handle(
        GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        var building = await _db.Buildings
            .FirstOrDefaultAsync(b => b.IsActive, cancellationToken);

        var apartmentCount = await _db.Apartments.CountAsync(a => a.IsActive, cancellationToken);
        var activeMeterCount = await _db.Meters.CountAsync(m => m.IsActive && m.ApartmentId != null, cancellationToken);

        var lastInvoice = await _db.GasInvoices
            .OrderByDescending(i => i.CreatedOn)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastInvoice is null)
        {
            return new DashboardSummaryResponse(building?.Name ?? string.Empty, apartmentCount, activeMeterCount,
                null, null, null, null, null, null, null, 0);
        }

        var activeRows = await _db.ApartmentInvoices
            .Where(x => x.GasInvoiceId == lastInvoice.Id && !x.IsSuperseded)
            .ToListAsync(cancellationToken);

        var distributed = activeRows.Sum(x => x.TotalAmount);
        var reconciled = distributed == lastInvoice.TotalAmount;
        var failedMeterCount = activeRows.Count(x => x.IsMeterReadFailed);

        // Kayıp-kaçak (S3): faturalanan enerji - dönemsel daire tüketimi toplamı.
        var apartmentConsumption = await
            (from reading in _db.MeterReadings
             join meter in _db.Meters on reading.MeterId equals meter.Id
             where meter.ApartmentId != null
                && reading.ReadingDateUtc >= lastInvoice.ReadingDateFrom
                && reading.ReadingDateUtc < lastInvoice.ReadingDateTo
             select (decimal?)reading.ConsumptionKWh)
            .SumAsync(cancellationToken) ?? 0m;

        var loss = lastInvoice.TotalEnergyKWh - apartmentConsumption;
        var lossPercent = lastInvoice.TotalEnergyKWh == 0m
            ? 0m
            : Math.Round(loss / lastInvoice.TotalEnergyKWh * 100m, 2);

        return new DashboardSummaryResponse(
            building?.Name ?? string.Empty,
            apartmentCount,
            activeMeterCount,
            lastInvoice.Id,
            lastInvoice.InvoiceNumber,
            lastInvoice.TotalAmount,
            lastInvoice.ReadingDateFrom,
            lastInvoice.ReadingDateTo,
            reconciled,
            lossPercent,
            failedMeterCount);
    }
}
