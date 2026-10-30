using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Apartments.Commands.AddApartments;

/// <summary>
/// Toplu daire ekleme: verilen sayı kadar yeni daire, mevcut en büyük kapı numarasından
/// devam eden numaralarla oluşturulur (boş sistemde 1'den başlar). Her daireye varsayılan
/// m² ile başlangıç SCD kaydı açılır; gerçek m² / malik sonradan Excel veya tek tek girilir.
/// </summary>
public sealed record AddApartmentsCommand(int Count) : IRequest<AddApartmentsResult>;

public sealed record AddApartmentsResult(int AddedCount, int FirstDoorNumber, int LastDoorNumber, int TotalApartments);

public sealed class AddApartmentsCommandValidator : AbstractValidator<AddApartmentsCommand>
{
    public const int MaxPerRequest = 500;

    public AddApartmentsCommandValidator()
    {
        RuleFor(c => c.Count)
            .InclusiveBetween(1, MaxPerRequest)
            .WithMessage($"Eklenecek daire sayısı 1 ile {MaxPerRequest} arasında olmalıdır.");
    }
}

public sealed class AddApartmentsCommandHandler : IRequestHandler<AddApartmentsCommand, AddApartmentsResult>
{
    private readonly IApplicationDbContext _db;

    public AddApartmentsCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<AddApartmentsResult> Handle(AddApartmentsCommand request, CancellationToken cancellationToken)
    {
        var building = await _db.Buildings.OrderBy(b => b.CreatedOn).FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Bina kaydı bulunamadı.");

        var existingDoors = await _db.Apartments
            .Where(a => a.BuildingId == building.Id)
            .Select(a => a.DoorNumber)
            .ToListAsync(cancellationToken);

        var maxDoor = existingDoors
            .Select(d => int.TryParse(d, out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();

        var validFrom = DateTimeOffset.UtcNow;
        var first = maxDoor + 1;

        for (var i = 0; i < request.Count; i++)
        {
            var door = (first + i).ToString();
            var apartment = new Apartment(building.Id, door, $"Daire {door}");
            apartment.AddDimension(
                DomainConstants.SeedData.DefaultSquareMeters,
                DomainConstants.SeedData.DefaultCommonAreaRatio,
                validFrom);
            _db.Apartments.Add(apartment);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new AddApartmentsResult(
            request.Count, first, first + request.Count - 1, existingDoors.Count + request.Count);
    }
}
