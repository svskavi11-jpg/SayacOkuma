using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Application.Features.Meters.Commands.ImportMeters;

public sealed class ImportMetersFromIzarCenterCommandHandler
    : IRequestHandler<ImportMetersFromIzarCenterCommand, ImportMetersResult>
{
    private readonly IApplicationDbContext _db;

    public ImportMetersFromIzarCenterCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ImportMetersResult> Handle(
        ImportMetersFromIzarCenterCommand request, CancellationToken cancellationToken)
    {
        // Tek SaveChanges = tek transaction; herhangi bir hata tüm içe aktarmayı geri alır.
        var apartmentsByDoor = await _db.Apartments.ToDictionaryAsync(a => int.Parse(a.DoorNumber), cancellationToken);

        var existingSerials = (await _db.Meters
            .Select(m => m.SerialNumber)
            .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var imported = 0;
        var skippedExisting = 0;
        var unmatched = 0;

        foreach (var item in request.Items)
        {
            if (existingSerials.Contains(item.SerialNumber))
            {
                skippedExisting++;
                continue;
            }

            if (!apartmentsByDoor.TryGetValue(item.DoorNumber, out var apartment))
            {
                unmatched++;
                continue;
            }

            var meter = Meter.CreateCalorimeter(
                item.SerialNumber,
                apartment.Id,
                item.PrimaryAddress,
                Manufacturer,
                DomainConstants.SeedData.SeedTimestamp);

            _db.Meters.Add(meter);
            existingSerials.Add(item.SerialNumber); // komut içindeki duplicate'ler de atlanır
            imported++;
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new ImportMetersResult(imported, skippedExisting, unmatched);
    }

    /// <summary>Gerçek dışa aktarımdaki üretici kodu (Danfoss SONOMETER).</summary>
    private const string Manufacturer = "DFS";
}
