using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;

namespace Westalife.Application.Features.Apartments.Queries.GetApartments;

/// <summary>Daire listesi + aktif SCD m² + bağlı sayaçlar (Faz 5 yönetim grid'inin veri kaynağı).</summary>
public sealed record GetApartmentsQuery : IRequest<IReadOnlyList<ApartmentListItem>>;

public sealed record ApartmentListItem(
    Guid Id,
    string DoorNumber,
    string Title,
    decimal? ActiveSquareMeters,
    string? OwnerName,
    Guid? ActiveMeterId,
    decimal? LastIndexValue,
    DateTimeOffset? LastReadingDateUtc,
    IReadOnlyList<ApartmentMeterItem> Meters);

public sealed record ApartmentMeterItem(Guid Id, string SerialNumber, string MeterType, int? MbusPrimaryAddress, bool IsActive);

public sealed class GetApartmentsQueryHandler : IRequestHandler<GetApartmentsQuery, IReadOnlyList<ApartmentListItem>>
{
    private readonly IApplicationDbContext _db;

    public GetApartmentsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ApartmentListItem>> Handle(
        GetApartmentsQuery request, CancellationToken cancellationToken)
    {
        var apartments = await _db.Apartments
            .Where(a => a.IsActive)
            .OrderBy(a => a.DoorNumber)
            .Select(a => new
            {
                a.Id,
                a.DoorNumber,
                a.Title,
                ActiveSquareMeters = a.Dimensions
                    .Where(d => d.ValidTo == null)
                    .Select(d => (decimal?)d.SquareMeters)
                    .FirstOrDefault(),
                // Etap 6.7: aktif kat maliki (SCD Type 2 — ValidTo NULL kayıt).
                OwnerName = a.Owners
                    .Where(o => o.ValidTo == null)
                    .Select(o => (string?)o.OwnerName)
                    .FirstOrDefault(),
                // Etap 6.7: aktif kalorimetre (en yeni CreatedOn — ImportReadings ile aynı deterministik
                // seçim) ve SADECE BU SAYACIN son okuması. Okumalar seçilen sayaca bağlı çözülür ki
                // dairede birden fazla aktif sayaç varsa "son endeks" farklı sayactan gelmesin.
                ActiveMeter = a.Meters
                    .Where(m => m.IsActive && m.Type == Domain.Enums.MeterType.Calorimeter)
                    .OrderByDescending(m => m.CreatedOn)
                    .Select(m => new
                    {
                        m.Id,
                        LastIndex = m.Readings
                            .OrderByDescending(r => r.ReadingDateUtc)
                            .Select(r => (decimal?)r.IndexValue)
                            .FirstOrDefault(),
                        LastDate = m.Readings
                            .OrderByDescending(r => r.ReadingDateUtc)
                            .Select(r => (DateTimeOffset?)r.ReadingDateUtc)
                            .FirstOrDefault()
                    })
                    .FirstOrDefault(),
                Meters = a.Meters
                    .Select(m => new ApartmentMeterItem(
                        m.Id,
                        m.SerialNumber,
                        m.Type.ToString(),
                        m.MbusPrimaryAddress,
                        m.IsActive))
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return apartments
            .Select(a => new ApartmentListItem(
                a.Id, a.DoorNumber, a.Title, a.ActiveSquareMeters, a.OwnerName,
                a.ActiveMeter?.Id, a.ActiveMeter?.LastIndex, a.ActiveMeter?.LastDate, a.Meters))
            .ToList();
    }
}
