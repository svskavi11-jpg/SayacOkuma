using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Apartments.Queries.GetApartmentOwnerHistory;

/// <summary>Dairenin kat maliki geçmişi (SCD Type 2 — Etap 6.7). Kapalı kayıtlar dahil tüm dönemler.</summary>
public sealed record GetApartmentOwnerHistoryQuery(Guid ApartmentId)
    : IRequest<IReadOnlyList<ApartmentOwnerHistoryItem>>;

public sealed record ApartmentOwnerHistoryItem(
    Guid Id,
    string OwnerName,
    DateTimeOffset ValidFrom,
    DateTimeOffset? ValidTo);

public sealed class GetApartmentOwnerHistoryQueryHandler
    : IRequestHandler<GetApartmentOwnerHistoryQuery, IReadOnlyList<ApartmentOwnerHistoryItem>>
{
    private readonly IApplicationDbContext _db;

    public GetApartmentOwnerHistoryQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ApartmentOwnerHistoryItem>> Handle(
        GetApartmentOwnerHistoryQuery request, CancellationToken cancellationToken)
    {
        var exists = await _db.Apartments.AnyAsync(a => a.Id == request.ApartmentId, cancellationToken);
        if (!exists)
        {
            throw new DomainException(DomainConstants.ValidationMessages.ApartmentNotFoundForDoorNumber);
        }

        var items = await _db.ApartmentOwners
            .Where(o => o.ApartmentId == request.ApartmentId)
            .OrderBy(o => o.ValidFrom)
            .Select(o => new ApartmentOwnerHistoryItem(o.Id, o.OwnerName, o.ValidFrom, o.ValidTo))
            .ToListAsync(cancellationToken);

        return items;
    }
}