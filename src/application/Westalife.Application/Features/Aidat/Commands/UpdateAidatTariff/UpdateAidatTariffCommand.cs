using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Aidat.Commands.UpdateAidatTariff;

/// <summary>
/// Faz 6.9: Mevcut aidat tarifesini günceller. Geçmiş tahakkuk satırları etkilenmez.
/// </summary>
public sealed record UpdateAidatTariffCommand(
    Guid Id,
    string Name,
    decimal SquareMeters,
    decimal Amount) : IRequest<Guid>;

public sealed class UpdateAidatTariffCommandValidator : AbstractValidator<UpdateAidatTariffCommand>
{
    public UpdateAidatTariffCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();

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

public sealed class UpdateAidatTariffCommandHandler : IRequestHandler<UpdateAidatTariffCommand, Guid>
{
    private readonly IApplicationDbContext _db;

    public UpdateAidatTariffCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<Guid> Handle(UpdateAidatTariffCommand request, CancellationToken cancellationToken)
    {
        var tariff = await _db.AidatTariffs
            .FirstOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Aidat tarifesı bulunamadı.");

        var name = request.Name.Trim();

        // Benzersizlik: kendi kaydı hariç aynı isim kabul edilmez.
        var nameTaken = await _db.AidatTariffs
            .AnyAsync(t => t.Id != request.Id && t.Name.ToLower() == name.ToLower(), cancellationToken);

        if (nameTaken)
        {
            throw new DomainException(DomainConstants.ValidationMessages.AidatTariffNameAlreadyExists);
        }

        tariff.Update(name, request.SquareMeters, request.Amount);
        await _db.SaveChangesAsync(cancellationToken);

        return tariff.Id;
    }
}
