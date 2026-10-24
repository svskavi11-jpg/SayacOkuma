using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Apartments.Commands.UpdateApartmentDimension;

/// <summary>
/// Daire m²'sini günceller — SCD Type 2 akışı: mevcut aktif kayıt kapatılır (ValidTo = şimdi),
/// yeni değerlerle yeni kayıt açılır. Eski veri EZİLMEZ (audit trail korunur).
/// </summary>
public sealed record UpdateApartmentDimensionCommand(
    Guid ApartmentId,
    decimal SquareMeters,
    decimal CommonAreaRatio) : IRequest<UpdateApartmentDimensionResult>;

public sealed record UpdateApartmentDimensionResult(
    Guid ApartmentId,
    decimal NewSquareMeters,
    DateTimeOffset ValidFrom,
    DateTimeOffset? PreviousValidTo);

public sealed class UpdateApartmentDimensionCommandValidator : AbstractValidator<UpdateApartmentDimensionCommand>
{
    public UpdateApartmentDimensionCommandValidator()
    {
        RuleFor(c => c.SquareMeters)
            .GreaterThan(0)
            .LessThanOrEqualTo(DomainConstants.ApartmentDimension.MaxSquareMeters)
            .WithMessage(DomainConstants.ValidationMessages.SquareMetersMustBePositive);

        RuleFor(c => c.CommonAreaRatio)
            .GreaterThanOrEqualTo(0)
            .WithMessage(DomainConstants.ValidationMessages.CommonAreaRatioMustBeNonNegative);
    }
}

public sealed class UpdateApartmentDimensionCommandHandler
    : IRequestHandler<UpdateApartmentDimensionCommand, UpdateApartmentDimensionResult>
{
    private readonly IApplicationDbContext _db;

    public UpdateApartmentDimensionCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<UpdateApartmentDimensionResult> Handle(
        UpdateApartmentDimensionCommand request, CancellationToken cancellationToken)
    {
        var apartment = await _db.Apartments
            .Include(a => a.Dimensions)
            .FirstOrDefaultAsync(a => a.Id == request.ApartmentId, cancellationToken)
            ?? throw new Westalife.Domain.Exceptions.NotFoundException("Daire bulunamadı.");

        var validFrom = DateTimeOffset.UtcNow;
        var previousActive = apartment.Dimensions.FirstOrDefault(d => d.ValidTo is null);

        // Domain: eski kaydı kapat + yeni SCD kaydını oluştur.
        var newDimension = apartment.AddDimension(request.SquareMeters, request.CommonAreaRatio, validFrom);

        // Açık Added durumu: navigation-discovery, anahtarı dolu client-GUID'li entity'yi
        // Modified olarak takip edebiliyor (tespit edilen bug) → bilinçli Add() garantisi.
        _db.ApartmentDimensions.Add(newDimension);

        await _db.SaveChangesAsync(cancellationToken);

        return new UpdateApartmentDimensionResult(
            apartment.Id,
            request.SquareMeters,
            validFrom,
            previousActive?.ValidTo);
    }
}
