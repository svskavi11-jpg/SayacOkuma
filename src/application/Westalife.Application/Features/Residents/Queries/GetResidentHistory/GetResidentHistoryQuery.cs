using System.Globalization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Enums;

namespace Westalife.Application.Features.Residents.Queries.GetResidentHistory;

public sealed record ResidentHistoryItem(
    Guid Id,
    Guid ApartmentId,
    string DoorNumber,
    string OwnerName,
    ResidentType ResidentType,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    string? PhoneNumber,
    DateOnly? LeaseStartDate,
    DateOnly? LeaseEndDate);

public sealed record GetResidentHistoryQuery(
    string? DoorNumber,
    string? Name,
    ResidentType? ResidentType,
    DateOnly? From,
    DateOnly? To) : IRequest<IReadOnlyList<ResidentHistoryItem>>;

public sealed class GetResidentHistoryQueryValidator : AbstractValidator<GetResidentHistoryQuery>
{
    public GetResidentHistoryQueryValidator()
    {
        RuleFor(q => q.To)
            .GreaterThanOrEqualTo(q => q.From)
            .When(q => q.From.HasValue && q.To.HasValue)
            .WithMessage(DomainConstants.AuditReport.PeriodInvalid);
    }
}

public sealed class GetResidentHistoryQueryHandler
    : IRequestHandler<GetResidentHistoryQuery, IReadOnlyList<ResidentHistoryItem>>
{
    private static readonly CompareInfo Turkish = CultureInfo.GetCultureInfo("tr-TR").CompareInfo;
    private static readonly TimeZoneInfo Turkey = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul");

    private readonly IApplicationDbContext _db;

    public GetResidentHistoryQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ResidentHistoryItem>> Handle(
        GetResidentHistoryQuery request, CancellationToken cancellationToken)
    {
        var rows = await _db.ApartmentOwners.AsNoTracking()
            .Select(o => new HistoryRow(
                o.Id,
                o.ApartmentId,
                o.Apartment.DoorNumber,
                o.OwnerName,
                o.ResidentType,
                o.ValidFrom,
                o.ValidTo,
                o.PhoneNumber,
                o.LeaseStartDate,
                o.LeaseEndDate))
            .ToListAsync(cancellationToken);

        var door = request.DoorNumber?.Trim();
        var name = request.Name?.Trim();
        IEnumerable<HistoryRow> filtered = rows;
        if (!string.IsNullOrEmpty(door) && !string.IsNullOrEmpty(name))
        {
            filtered = filtered.Where(r => Contains(r.DoorNumber, door) || Contains(r.OwnerName, name));
        }
        else if (!string.IsNullOrEmpty(door))
        {
            filtered = filtered.Where(r => Contains(r.DoorNumber, door));
        }
        else if (!string.IsNullOrEmpty(name))
        {
            filtered = filtered.Where(r => Contains(r.OwnerName, name));
        }

        if (request.ResidentType is { } residentType)
        {
            filtered = filtered.Where(r => r.ResidentType == residentType);
        }

        if (request.From is { } from)
        {
            var start = StartOfDay(from);
            filtered = filtered.Where(r => r.ValidTo is null || r.ValidTo > start);
        }

        if (request.To is { } to)
        {
            var endExclusive = StartOfDay(to.AddDays(1));
            filtered = filtered.Where(r => r.ValidFrom < endExclusive);
        }

        return filtered
            .OrderBy(r => r.ValidTo.HasValue ? 1 : 0)
            .ThenByDescending(r => r.ValidTo)
            .ThenByDescending(r => r.ValidFrom)
            .Select(r => new ResidentHistoryItem(
                r.Id,
                r.ApartmentId,
                r.DoorNumber,
                r.OwnerName,
                r.ResidentType,
                TurkeyDate(r.ValidFrom),
                r.ValidTo is { } left ? TurkeyDate(left) : null,
                r.PhoneNumber,
                r.LeaseStartDate,
                r.LeaseEndDate))
            .ToList();
    }

    private static bool Contains(string? source, string term) =>
        !string.IsNullOrEmpty(source) && Turkish.IndexOf(source, term, CompareOptions.IgnoreCase) >= 0;

    private static DateTimeOffset StartOfDay(DateOnly date)
    {
        var local = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, Turkey.GetUtcOffset(local));
    }

    private static DateOnly TurkeyDate(DateTimeOffset value) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, Turkey).DateTime);

    private sealed record HistoryRow(
        Guid Id,
        Guid ApartmentId,
        string DoorNumber,
        string OwnerName,
        ResidentType ResidentType,
        DateTimeOffset ValidFrom,
        DateTimeOffset? ValidTo,
        string? PhoneNumber,
        DateOnly? LeaseStartDate,
        DateOnly? LeaseEndDate);
}
