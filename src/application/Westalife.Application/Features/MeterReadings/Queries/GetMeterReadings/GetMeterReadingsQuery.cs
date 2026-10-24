using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.MeterReadings.Queries.GetMeterReadings;

/// <summary>
/// Sayacın okuma listesi (Etap 6.7 — okuma düzeltme Modal'ının veri kaynağı).
/// Her satır: kilitli dönem durumu ve denetim kaydı sayısıyla birlikte döner.
/// </summary>
public sealed record GetMeterReadingsQuery(Guid MeterId)
    : IRequest<IReadOnlyList<MeterReadingListItem>>;

/// <summary>MeterId zorunlu: boş Guid ile sessiz boş liste yerine 400 döner.</summary>
public sealed class GetMeterReadingsQueryValidator : AbstractValidator<GetMeterReadingsQuery>
{
    public GetMeterReadingsQueryValidator()
    {
        RuleFor(x => x.MeterId)
            .NotEmpty()
            .WithMessage(DomainConstants.ValidationMessages.MeterIdRequired);
    }
}

public sealed record MeterReadingListItem(
    Guid Id,
    DateTimeOffset ReadingDateUtc,
    decimal IndexValue,
    decimal ConsumptionKWh,
    string Source,
    bool IsInitialReading,
    bool IsLockedPeriod,
    int AuditLogCount);

public sealed class GetMeterReadingsQueryHandler
    : IRequestHandler<GetMeterReadingsQuery, IReadOnlyList<MeterReadingListItem>>
{
    private readonly IApplicationDbContext _db;

    public GetMeterReadingsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<MeterReadingListItem>> Handle(
        GetMeterReadingsQuery request, CancellationToken cancellationToken)
    {
        var meter = await _db.Meters
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.MeterId, cancellationToken)
            ?? throw new DomainException(DomainConstants.ValidationMessages.ManualReadingMeterNotFound);

        var lockedPeriods = await _db.GasInvoices
            .Where(i => i.IsLocked)
            .Select(i => new { i.ReadingDateFrom, i.ReadingDateTo })
            .ToListAsync(cancellationToken);

        var auditCounts = await _db.MeterReadingAuditLogs
            .Where(l => l.MeterReading.MeterId == meter.Id)
            .GroupBy(l => l.MeterReadingId)
            .Select(g => new { ReadingId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ReadingId, x => x.Count, cancellationToken);

        var readings = await _db.MeterReadings
            .AsNoTracking()
            .Where(r => r.MeterId == meter.Id)
            .OrderByDescending(r => r.ReadingDateUtc)
            .Select(r => new
            {
                r.Id,
                r.ReadingDateUtc,
                r.IndexValue,
                r.ConsumptionKWh,
                r.Source,
                r.IsInitialReading
            })
            .ToListAsync(cancellationToken);

        return readings
            .Select(r => new MeterReadingListItem(
                r.Id,
                r.ReadingDateUtc,
                r.IndexValue,
                r.ConsumptionKWh,
                r.Source.ToString(),
                r.IsInitialReading,
                lockedPeriods.Any(p => p.ReadingDateFrom <= r.ReadingDateUtc && r.ReadingDateUtc < p.ReadingDateTo),
                auditCounts.TryGetValue(r.Id, out var count) ? count : 0))
            .ToList();
    }
}