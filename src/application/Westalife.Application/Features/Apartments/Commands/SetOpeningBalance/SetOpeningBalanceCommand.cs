using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Apartments.Commands.SetOpeningBalance;

/// <summary>
/// Devir (açılış) bakiyesi girişi (Etap 6.8 → anapara/faiz ayrımı).
/// Anapara ve geçmiş faiz AYRI tutarlar: faize tekrar faiz işlemez.
/// Her ikisi 0 verilirse devir temizlenir. Tarih, faizin başlangıcıdır.
/// </summary>
public sealed record SetOpeningBalanceCommand(
    Guid ApartmentId,
    decimal PrincipalAmount,
    decimal InterestAmount,
    DateOnly? BalanceDate,
    Guid? OwnerId = null) : IRequest<SetOpeningBalanceResult>;

public sealed record SetOpeningBalanceResult(
    Guid ApartmentId,
    decimal OpeningBalancePrincipal,
    decimal OpeningBalanceInterest,
    DateOnly? OpeningBalanceDate,
    Guid? OpeningBalanceOwnerId);

public sealed class SetOpeningBalanceCommandValidator : AbstractValidator<SetOpeningBalanceCommand>
{
    public SetOpeningBalanceCommandValidator()
    {
        RuleFor(c => c.PrincipalAmount)
            .InclusiveBetween(-DomainConstants.Apartment.OpeningBalanceMaxAbs, DomainConstants.Apartment.OpeningBalanceMaxAbs)
            .WithMessage(DomainConstants.Apartment.OpeningBalanceOutOfRange);
        RuleFor(c => c.InterestAmount)
            .GreaterThanOrEqualTo(0m)
            .WithMessage(DomainConstants.Apartment.OpeningBalanceInterestCannotBeNegative)
            .LessThanOrEqualTo(DomainConstants.Apartment.OpeningBalanceMaxAbs)
            .WithMessage(DomainConstants.Apartment.OpeningBalanceOutOfRange);
        RuleFor(c => c.BalanceDate).NotNull()
            .When(c => c.PrincipalAmount != 0m || c.InterestAmount != 0m)
            .WithMessage(DomainConstants.Apartment.OpeningBalanceDateRequired);
    }
}

public sealed class SetOpeningBalanceCommandHandler : IRequestHandler<SetOpeningBalanceCommand, SetOpeningBalanceResult>
{
    private readonly IApplicationDbContext _db;

    public SetOpeningBalanceCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<SetOpeningBalanceResult> Handle(SetOpeningBalanceCommand request, CancellationToken cancellationToken)
    {
        var apartment = await _db.Apartments.FirstOrDefaultAsync(a => a.Id == request.ApartmentId, cancellationToken)
            ?? throw new NotFoundException("Daire bulunamadı.");

        Guid? ownerId = request.OwnerId is { } id && id != Guid.Empty ? id : null;
        if (ownerId is { } selected)
        {
            var belongs = await _db.ApartmentOwners.AnyAsync(
                o => o.Id == selected && o.ApartmentId == apartment.Id, cancellationToken);
            if (!belongs)
            {
                throw new DomainException(DomainConstants.ApartmentOwner.OpeningBalanceOwnerNotFound);
            }
        }

        apartment.SetOpeningBalance(request.PrincipalAmount, request.InterestAmount, request.BalanceDate, ownerId);
        await _db.SaveChangesAsync(cancellationToken);

        return new SetOpeningBalanceResult(
            apartment.Id,
            apartment.OpeningBalancePrincipal,
            apartment.OpeningBalanceInterest,
            apartment.OpeningBalanceDate,
            apartment.OpeningBalanceOwnerId);
    }
}
