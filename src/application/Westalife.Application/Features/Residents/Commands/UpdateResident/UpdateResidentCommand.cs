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
    string? Notes,
    DateOnly? LeaseStartDate = null,
    DateOnly? LeaseEndDate = null,
    DateOnly? ValidFrom = null,
    DateOnly? ValidTo = null);

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
    string? KiraciName,
    Guid? EvSahibiId = null,
    Guid? KiraciId = null);

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

        // İstemci "yyyy-MM-dd" gönderirse JSON bunu yerel ofsetle çözer (ör. -03:00).
        // Npgsql timestamptz yalnızca UTC (ofset 0) kabul eder.
        var validFrom = request.ValidFrom.ToUniversalTime();

        // UpdateResident no-op'ta mevcut (tracked) kaydı döndürür. Yalnızca bu çağrıdan
        // önce koleksiyonda olmayan kimlik yeni SCD satırıdır.
        var existingOwnerIds = apartment.Owners.Select(o => o.Id).ToHashSet();
        Guid? evSahibiId = null;
        Guid? kiraciId = null;

        if (request.EvSahibi is { } evSahibi)
        {
            var resident = Place(apartment, ResidentType.EvSahibi, evSahibi, validFrom);
            EnsureNewOwnerAdded(_db, resident, existingOwnerIds);
            evSahibiId = resident.Id;
        }

        // Kiracı bloğu verilmiş ama ad boş bırakılmışsa kiracı kaydına dokunulmaz (bilinçli boş geçiş).
        if (request.Kiraci is { } kiraci && !string.IsNullOrWhiteSpace(kiraci.Name))
        {
            var resident = Place(apartment, ResidentType.Kiraci, kiraci, validFrom);
            EnsureNewOwnerAdded(_db, resident, existingOwnerIds);
            kiraciId = resident.Id;
        }

        await _db.SaveChangesAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;

        return new UpdateResidentResult(
            apartment.Id,
            apartment.GetActiveResident(ResidentType.EvSahibi, now)?.OwnerName,
            apartment.GetActiveResident(ResidentType.Kiraci, now)?.OwnerName,
            evSahibiId,
            kiraciId);
    }

    private static ApartmentOwner Place(
        Apartment apartment, ResidentType residentType, ResidentInfoInput info, DateTimeOffset commandValidFrom)
    {
        var from = info.ValidFrom is { } day ? TurkeyStart(day) : commandValidFrom;
        DateTimeOffset? to = info.ValidTo is { } end ? TurkeyStart(end) : null;
        return apartment.UpdateResident(
            residentType,
            info.Name,
            info.PhoneNumber,
            info.Email,
            info.IdentityNumber,
            info.Notes,
            from,
            replaceContact: true,
            validTo: to,
            leaseStart: info.LeaseStartDate,
            leaseEnd: info.LeaseEndDate);
    }

    private static DateTimeOffset TurkeyStart(DateOnly date)
    {
        var zoneId = OperatingSystem.IsWindows() ? "Turkey Standard Time" : "Europe/Istanbul";
        var zone = TimeZoneInfo.FindSystemTimeZoneById(zoneId);
        var local = date.ToDateTime(TimeOnly.MinValue);
        return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }

    /// <summary>
    /// Yeni SCD satırını INSERT (Added) olarak işaretler.
    /// Anahtarı dolu client-GUID, ValueGeneratedOnAdd olduğu için navigation keşfi
    /// kaydı Modified sanıp var olmayan satıra UPDATE gönderebiliyor (0 satır çakışması).
    /// No-op mevcut kaydı döndürür; onun kimliği <paramref name="existingOwnerIds"/> içindedir.
    /// </summary>
    private static void EnsureNewOwnerAdded(
        IApplicationDbContext db, ApartmentOwner resident, IReadOnlySet<Guid> existingOwnerIds)
    {
        if (existingOwnerIds.Contains(resident.Id))
        {
            return;
        }

        db.ApartmentOwners.Add(resident);

        if (db is DbContext ctx && ctx.Entry(resident).State != EntityState.Added)
        {
            ctx.Entry(resident).State = EntityState.Added;
        }
    }
}
