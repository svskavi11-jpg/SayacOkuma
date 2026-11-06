using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Apartments.Commands.UpdateApartmentOwner;

/// <summary>
/// Kat maliki değişikliği (Etap 6.7 — SCD Type 2): eski malik kaydı kapatılır, yenisi açılır;
/// geçmiş ASLA silinmez. Ad değişmediyse no-op döner (Changed=false).
/// </summary>
public sealed record UpdateApartmentOwnerCommand(
    Guid ApartmentId,
    string OwnerName) : IRequest<UpdateApartmentOwnerResult>;

public sealed record UpdateApartmentOwnerResult(
    Guid ApartmentId,
    string OwnerName,
    DateTimeOffset ValidFrom,
    bool Changed);

public sealed class UpdateApartmentOwnerCommandValidator : AbstractValidator<UpdateApartmentOwnerCommand>
{
    public UpdateApartmentOwnerCommandValidator()
    {
        RuleFor(c => c.ApartmentId).NotEmpty();

        RuleFor(c => c.OwnerName)
            .NotEmpty()
            .MaximumLength(DomainConstants.ApartmentOwner.OwnerNameMaxLength)
            .WithMessage(DomainConstants.ValidationMessages.OwnerNameRequired);
    }
}

public sealed class UpdateApartmentOwnerCommandHandler
    : IRequestHandler<UpdateApartmentOwnerCommand, UpdateApartmentOwnerResult>
{
    private readonly IApplicationDbContext _db;

    public UpdateApartmentOwnerCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<UpdateApartmentOwnerResult> Handle(
        UpdateApartmentOwnerCommand request, CancellationToken cancellationToken)
    {
        var apartment = await _db.Apartments
            .Include(a => a.Owners)
            .FirstOrDefaultAsync(a => a.Id == request.ApartmentId, cancellationToken)
            ?? throw new DomainException(DomainConstants.ValidationMessages.ApartmentNotFoundForDoorNumber);

        // No-op tespiti: aktif EV SAHİBİ adı değişmediyse hiçbir kayıt açılmaz.
        // Tür filtresi şart — Kiracı da aktif olabilir; filtresiz FirstOrDefault aktif
        // Kiracı'yı yakalayıp no-op'ı yanlış hesaplıyor ve yeni Ev Sahibi satırı eklenmiyordu.
        var existingOwnerIds = apartment.Owners.Select(o => o.Id).ToHashSet();
        var currentActiveEvSahibi = apartment.Owners.FirstOrDefault(
            o => o.ResidentType == ResidentType.EvSahibi && o.ValidTo is null);

        var isNoOp = currentActiveEvSahibi is not null
            && currentActiveEvSahibi.OwnerName.Equals(request.OwnerName.Trim(), StringComparison.OrdinalIgnoreCase);

        var validFrom = DateTimeOffset.UtcNow;
        var newOwner = apartment.UpdateOwner(request.OwnerName, validFrom);

        // Yeni SCD satırı: client-GUID + ValueGeneratedOnAdd, navigation keşfinde Modified
        // olabiliyor. Kimliği bu yüklemede yoksa açıkça Added'a alınır (0-satır UPDATE yok).
        if (!existingOwnerIds.Contains(newOwner.Id))
        {
            _db.ApartmentOwners.Add(newOwner);
            if (_db is DbContext ctx && ctx.Entry(newOwner).State != EntityState.Added)
            {
                ctx.Entry(newOwner).State = EntityState.Added;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new UpdateApartmentOwnerResult(apartment.Id, newOwner.OwnerName, newOwner.ValidFrom, !isNoOp);
    }
}