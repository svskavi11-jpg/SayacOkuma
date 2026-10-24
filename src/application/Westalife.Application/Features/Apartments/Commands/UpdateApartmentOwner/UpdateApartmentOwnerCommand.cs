using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
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

        // No-op tespiti: aktif malik adı değişmediyse hiçbir kayıt açılmaz.
        var currentActive = apartment.Owners.FirstOrDefault(o => o.ValidTo is null);
        var isNoOp = currentActive is not null
            && currentActive.OwnerName.Equals(request.OwnerName.Trim(), StringComparison.OrdinalIgnoreCase);

        var validFrom = DateTimeOffset.UtcNow;
        var newOwner = apartment.UpdateOwner(request.OwnerName, validFrom);

        // Yeni kayıt açıldıysa garanti şekilde ekle (navigation-discovery detayı —
        // UpdateApartmentDimension ile aynı bilinçli Add() deseni).
        if (!isNoOp)
        {
            _db.ApartmentOwners.Add(newOwner);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new UpdateApartmentOwnerResult(apartment.Id, newOwner.OwnerName, newOwner.ValidFrom, !isNoOp);
    }
}