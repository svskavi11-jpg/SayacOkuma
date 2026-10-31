using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Aidat.Commands.SetApartmentTariff;

/// <summary>
/// Faz 6.9: Tekli tarife atama — daireye aktif bir aidat tarifesı atanır veya
/// TariffId null verilerek mevcut atama temizlenir (tarifesiz).
/// </summary>
public sealed record SetApartmentTariffCommand(Guid ApartmentId, Guid? TariffId) : IRequest<SetApartmentTariffResult>;

public sealed record SetApartmentTariffResult(Guid ApartmentId, Guid? TariffId, string? TariffName);

public sealed class SetApartmentTariffCommandValidator : AbstractValidator<SetApartmentTariffCommand>
{
    public SetApartmentTariffCommandValidator()
    {
        RuleFor(c => c.ApartmentId).NotEmpty();
    }
}

public sealed class SetApartmentTariffCommandHandler : IRequestHandler<SetApartmentTariffCommand, SetApartmentTariffResult>
{
    private readonly IApplicationDbContext _db;

    public SetApartmentTariffCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<SetApartmentTariffResult> Handle(
        SetApartmentTariffCommand request, CancellationToken cancellationToken)
    {
        var apartment = await _db.Apartments
            .FirstOrDefaultAsync(a => a.Id == request.ApartmentId, cancellationToken)
            ?? throw new NotFoundException("Daire bulunamadı.");

        string? tariffName = null;
        if (request.TariffId is { } tariffId)
        {
            var tariff = await _db.AidatTariffs
                .FirstOrDefaultAsync(t => t.Id == tariffId, cancellationToken)
                ?? throw new NotFoundException("Aidat tarifesı bulunamadı.");

            if (!tariff.IsActive)
            {
                throw new DomainException(DomainConstants.ValidationMessages.AidatTariffNotActive);
            }

            tariffName = tariff.Name;
        }

        apartment.SetAidatTariff(request.TariffId);
        await _db.SaveChangesAsync(cancellationToken);

        return new SetApartmentTariffResult(apartment.Id, request.TariffId, tariffName);
    }
}
