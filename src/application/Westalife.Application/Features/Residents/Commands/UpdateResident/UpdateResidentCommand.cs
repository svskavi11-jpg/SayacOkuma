using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Entities;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Residents.Commands.UpdateResident;

/// <summary>Sakin bilgi bloğu — Ev Sahibi veya Kiracı için ortak alanlar.</summary>
public sealed record ResidentInfoInput(
    string Name,
    string? PhoneNumber,
    string? Email,
    string? IdentityNumber,
    string? Notes);

/// <summary>
/// Faz 7.1: Sakin ekler/günceller — hem Ev Sahibi hem Kiracı tek komutta (SCD Type 2,
/// tür bazlı ayrı akış). Kiracı bloğu verilmezse kiracı kaydına dokunulmaz.
/// Doğrulamalar (telefon/e-posta/TCKN) Domain'de yapılır; hata FluentValidation'a yansır.
/// </summary>
public sealed record UpdateResidentCommand(
    Guid ApartmentId,
    DateTimeOffset ValidFrom,
    ResidentInfoInput? EvSahibi,
    ResidentInfoInput? Kiraci) : IRequest<UpdateResidentResult>;

public sealed record UpdateResidentResult(
    Guid ApartmentId,
    string? EvSahibiName,
    string? KiraciName);

public sealed class UpdateResidentCommandValidator : AbstractValidator<UpdateResidentCommand>
{
    public UpdateResidentCommandValidator()
    {
        RuleFor(c => c.ApartmentId).NotEmpty();

        RuleFor(c => c.ValidFrom).NotEmpty();

        // En az bir sakin bloğu verilmelidir.
        RuleFor(c => c)
            .Must(c => c.EvSahibi is not null || c.Kiraci is not null)
            .WithMessage("Ev sahibi veya kiracı bilgilerinden en az biri verilmelidir.");

        RuleFor(c => c.EvSahibi!.Name)
            .NotEmpty()
            .WithMessage("Ev sahibi adı soyadı zorunludur.")
            .When(c => c.EvSahibi is not null);

        RuleFor(c => c.Kiraci!.Name)
            .NotEmpty()
            .WithMessage("Kiracı adı soyadı zorunludur.")
            .When(c => c.Kiraci is not null);
    }
}

public sealed class UpdateResidentCommandHandler : IRequestHandler<UpdateResidentCommand, UpdateResidentResult>
{
    private readonly IApplicationDbContext _db;

    public UpdateResidentCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<UpdateResidentResult> Handle(
        UpdateResidentCommand request, CancellationToken cancellationToken)
    {
        var apartment = await _db.Apartments
            .Include(a => a.Owners)
            .FirstOrDefaultAsync(a => a.Id == request.ApartmentId, cancellationToken)
            ?? throw new NotFoundException("Daire bulunamadı.");

        if (request.EvSahibi is { } evSahibi)
        {
            apartment.UpdateResident(
                ResidentType.EvSahibi,
                evSahibi.Name,
                evSahibi.PhoneNumber,
                evSahibi.Email,
                evSahibi.IdentityNumber,
                evSahibi.Notes,
                request.ValidFrom);
        }

        // Kiracı bloğu verilmiş ama ad boş bırakılmışsa kiracı kaydına dokunulmaz (bilinçli boş geçiş).
        if (request.Kiraci is { } kiraci && !string.IsNullOrWhiteSpace(kiraci.Name))
        {
            apartment.UpdateResident(
                ResidentType.Kiraci,
                kiraci.Name,
                kiraci.PhoneNumber,
                kiraci.Email,
                kiraci.IdentityNumber,
                kiraci.Notes,
                request.ValidFrom);
        }

        await _db.SaveChangesAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;

        return new UpdateResidentResult(
            apartment.Id,
            apartment.GetActiveResident(ResidentType.EvSahibi, now)?.OwnerName,
            apartment.GetActiveResident(ResidentType.Kiraci, now)?.OwnerName);
    }
}
