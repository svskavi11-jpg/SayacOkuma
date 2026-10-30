using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Aidat.Commands.CreateAidatTariff;

/// <summary>
/// Faz 6.9: Yeni aidat tarifesı tanımlar (örn: "1+1 C" → 32,1 m² → 2.000,00 TL/ay).
/// </summary>
public sealed record CreateAidatTariffCommand(
    string Name,
    decimal SquareMeters,
    decimal Amount) : IRequest<Guid>;

public sealed class CreateAidatTariffCommandValidator : AbstractValidator<CreateAidatTariffCommand>
{
    public CreateAidatTariffCommandValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty()
            .WithMessage(DomainConstants.ValidationMessages.AidatTariffNameRequired)
            .MaximumLength(DomainConstants.AidatTariff.NameMaxLength);

        RuleFor(c => c.SquareMeters)
            .GreaterThan(0m)
            .LessThanOrEqualTo(DomainConstants.AidatTariff.SquareMetersMax)
            .WithMessage(DomainConstants.ValidationMessages.AidatTariffSquareMetersInvalid);

        RuleFor(c => c.Amount)
            .GreaterThan(0m)
            .LessThanOrEqualTo(DomainConstants.AidatTariff.AmountMax)
            .WithMessage(DomainConstants.ValidationMessages.AidatTariffAmountMustBePositive);
    }
}

public sealed class CreateAidatTariffCommandHandler : IRequestHandler<CreateAidatTariffCommand, Guid>
{
    private readonly IApplicationDbContext _db;

    public CreateAidatTariffCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(CreateAidatTariffCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();

        // Tarife adı benzersiz olmalı (büyük/küçük harf duyarsız).
        var nameTaken = await _db.AidatTariffs
            .AnyAsync(t => t.Name.ToLower() == name.ToLower(), cancellationToken);

        if (nameTaken)
        {
            throw new DomainException(DomainConstants.ValidationMessages.AidatTariffNameAlreadyExists);
        }

        var tariff = new AidatTariff(name, request.SquareMeters, request.Amount);

        _db.AidatTariffs.Add(tariff);
        await _db.SaveChangesAsync(cancellationToken);

        return tariff.Id;
    }
}
