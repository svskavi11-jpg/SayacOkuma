using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.MeterReadings.Queries.GetMeterReadingAuditLog;

/// <summary>Tek okumanın düzeltme (denetim) geçmişi (Etap 6.7).</summary>
public sealed record GetMeterReadingAuditLogQuery(Guid MeterReadingId)
    : IRequest<IReadOnlyList<MeterReadingAuditLogItem>>;

public sealed record MeterReadingAuditLogItem(
    Guid Id,
    decimal OldIndexValue,
    decimal NewIndexValue,
    decimal OldConsumptionKWh,
    decimal NewConsumptionKWh,
    string Reason,
    string ChangedBy,
    DateTimeOffset ChangedOnUtc);

public sealed class GetMeterReadingAuditLogQueryHandler
    : IRequestHandler<GetMeterReadingAuditLogQuery, IReadOnlyList<MeterReadingAuditLogItem>>
{
    private readonly IApplicationDbContext _db;

    public GetMeterReadingAuditLogQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<MeterReadingAuditLogItem>> Handle(
        GetMeterReadingAuditLogQuery request, CancellationToken cancellationToken)
    {
        var items = await _db.MeterReadingAuditLogs
            .AsNoTracking()
            .Where(l => l.MeterReadingId == request.MeterReadingId)
            .OrderByDescending(l => l.ChangedOnUtc)
            .Select(l => new MeterReadingAuditLogItem(
                l.Id,
                l.OldIndexValue,
                l.NewIndexValue,
                l.OldConsumptionKWh,
                l.NewConsumptionKWh,
                l.Reason,
                l.ChangedBy,
                l.ChangedOnUtc))
            .ToListAsync(cancellationToken);

        return items;
    }
}