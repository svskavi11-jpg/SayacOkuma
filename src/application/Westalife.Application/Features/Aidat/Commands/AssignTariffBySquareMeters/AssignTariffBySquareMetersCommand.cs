using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Aidat.Commands.AssignTariffBySquareMeters;

/// <summary>
/// Faz 6.9: Toplu tarife atama — aktif SCD m² kaydı verilen değere eşit (kuruş
/// hassasiyetinde, decimal.Round × 2) olan tüm dairelere tarife atanır.
/// Zaten bu tarifeye atanmış daireler tekrar dokunulmaz (no-op), raporda sayılır.
/// </summary>
public sealed record AssignTariffBySquareMetersCommand(Guid TariffId, decimal SquareMeters)
    : IRequest<AssignTariffBySquareMetersResult>;

public sealed record AssignTariffBySquareMetersResult(
    int AssignedCount,
    int AlreadyAssignedCount,
    int NoMatchCount,
    IReadOnlyList<string> DoorNumbers);

public sealed class AssignTariffBySquareMetersCommandValidator
    : AbstractValidator<AssignTariffBySquareMetersCommand>
{
    public AssignTariffBySquareMetersCommandValidator()
    {
        RuleFor(c => c.TariffId).NotEmpty();

        RuleFor(c => c.SquareMeters)
            .GreaterThan(0m)
            .LessThanOrEqualTo(DomainConstants.AidatTariff.SquareMetersMax)
            .WithMessage(DomainConstants.ValidationMessages.AidatTariffSquareMetersInvalid);
    }
}

public sealed class AssignTariffBySquareMetersCommandHandler
    : IRequestHandler<AssignTariffBySquareMetersCommand, AssignTariffBySquareMetersResult>
{
    private readonly IApplicationDbContext _db;

    public AssignTariffBySquareMetersCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<AssignTariffBySquareMetersResult> Handle(
        AssignTariffBySquareMetersCommand request, CancellationToken cancellationToken)
    {
        var tariff = await _db.AidatTariffs
            .FirstOrDefaultAsync(t => t.Id == request.TariffId, cancellationToken)
            ?? throw new NotFoundException("Aidat tarifesı bulunamadı.");

        if (!tariff.IsActive)
        {
            throw new DomainException(DomainConstants.ValidationMessages.AidatTariffNotActive);
        }

        // Kuruş hassasiyetinde eşleştirme: 47,4 ile 47,40 aynı kabul edilir.
        var target = decimal.Round(request.SquareMeters, 2);

        var apartments = await _db.Apartments
            .Include(a => a.Dimensions)
            .Where(a => a.IsActive)
            .ToListAsync(cancellationToken);

        var alreadyAssigned = apartments.Count(a => a.AidatTariffId == tariff.Id);

        var matched = apartments
            .Where(a => a.AidatTariffId != tariff.Id)
            .Where(a =>
            {
                var dimension = a.Dimensions.FirstOrDefault(d => d.ValidTo is null);
                return dimension is not null && decimal.Round(dimension.SquareMeters, 2) == target;
            })
            .OrderByDoorNumber(a => a.DoorNumber)
            .ToList();

        foreach (var apartment in matched)
        {
            apartment.SetAidatTariff(tariff.Id);
        }

        if (matched.Count > 0)
        {
            await _db.SaveChangesAsync(cancellationToken);
        }

        var noMatchCount = apartments.Count - matched.Count - alreadyAssigned;

        return new AssignTariffBySquareMetersResult(
            matched.Count,
            alreadyAssigned,
            noMatchCount,
            matched.Select(a => a.DoorNumber).ToList());
    }
}
