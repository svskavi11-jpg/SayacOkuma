using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;

namespace Westalife.Application.Features.Aidat.Queries.GetAidatTariffs;

/// <summary>
/// Faz 6.9: Aidat tarife listesi (atanmış daire sayısı ile). IsActive filtresi opsiyonel.
/// </summary>
public sealed record GetAidatTariffsQuery(bool? IsActive = null) : IRequest<IReadOnlyList<AidatTariffListItem>>;

public sealed record AidatTariffListItem(
    Guid Id,
    string Name,
    decimal SquareMeters,
    decimal Amount,
    bool IsActive,
    int ApartmentCount);

public sealed class GetAidatTariffsQueryHandler : IRequestHandler<GetAidatTariffsQuery, IReadOnlyList<AidatTariffListItem>>
{
    private readonly IApplicationDbContext _db;

    public GetAidatTariffsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<AidatTariffListItem>> Handle(
        GetAidatTariffsQuery request, CancellationToken cancellationToken)
    {
        var query = _db.AidatTariffs.AsNoTracking().AsQueryable();

        if (request.IsActive.HasValue)
        {
            query = query.Where(t => t.IsActive == request.IsActive.Value);
        }

        var tariffs = await query
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        // Tarife başına atanmış daire sayısı (bilgi amaçlı).
        var counts = await _db.Apartments
            .AsNoTracking()
            .Where(a => a.AidatTariffId != null)
            .GroupBy(a => a.AidatTariffId!.Value)
            .Select(g => new { TariffId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var countMap = counts.ToDictionary(x => x.TariffId, x => x.Count);

        return tariffs
            .Select(t => new AidatTariffListItem(
                t.Id,
                t.Name,
                t.SquareMeters,
                t.Amount,
                t.IsActive,
                countMap.GetValueOrDefault(t.Id)))
            .ToList();
    }
}
