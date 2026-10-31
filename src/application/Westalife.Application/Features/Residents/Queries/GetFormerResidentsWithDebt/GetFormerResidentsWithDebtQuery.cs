using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Entities;
using Westalife.Domain.Enums;
using Westalife.Domain.Services.Ledger;

namespace Westalife.Application.Features.Residents.Queries.GetFormerResidentsWithDebt;

/// <summary>
/// Faz 7.1: Dairede oturmayı bitirmiş (ValidTo dolu) ve cari hesabında kalan borcu olan
/// eski sakinleri listeler. Kiracı borcunu ödemeden çıktıysa Hibrit Model (KMK m.22)
/// gereği "Ev Sahibine rücu edilebilir" bilgisi satırla birlikte döner.
/// </summary>
public sealed record GetFormerResidentsWithDebtQuery : IRequest<IReadOnlyList<FormerResidentDebtItem>>;

public sealed record FormerResidentDebtItem(
    Guid ApartmentId,
    string DoorNumber,
    string ApartmentTitle,
    Guid OwnerId,
    string OwnerName,
    string ResidentTypeLabel,
    string? PhoneNumber,
    DateTimeOffset? LeftOn,
    decimal PrincipalDue,
    decimal InterestAccrued,
    decimal Balance,

    /// <summary>Müteselsil sorumlu ev sahibi (kiracı satırlarında dolu — KMK m.22).</summary>
    string? EvSahibiName,

    /// <summary>Kiracı borcudur ve ev sahibi kaydı mevcut — rücu edilebilir (uyarı gösterilir).</summary>
    bool RucuPossible);

public sealed class GetFormerResidentsWithDebtQueryHandler
    : IRequestHandler<GetFormerResidentsWithDebtQuery, IReadOnlyList<FormerResidentDebtItem>>
{
    private readonly IApplicationDbContext _db;

    public GetFormerResidentsWithDebtQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<FormerResidentDebtItem>> Handle(
        GetFormerResidentsWithDebtQuery request, CancellationToken cancellationToken)
    {
        var asOf = DateOnly.FromDateTime(DateTime.Today);
        var now = DateTimeOffset.UtcNow;

        var apartments = await _db.Apartments
            .AsNoTracking()
            .Include(a => a.Owners)
            .Where(a => a.IsActive)
            .ToListAsync(cancellationToken);

        var apartmentIds = apartments.Select(a => a.Id).ToList();

        var invoices = await _db.ApartmentInvoices
            .AsNoTracking()
            .Include(i => i.GasInvoice)
            .Where(i => !i.IsSuperseded && apartmentIds.Contains(i.ApartmentId))
            .ToListAsync(cancellationToken);

        var charges = await _db.AidatCharges
            .AsNoTracking()
            .Include(c => c.AidatInvoice)
            .Where(c => apartmentIds.Contains(c.ApartmentId))
            .ToListAsync(cancellationToken);

        var payments = await _db.Payments
            .AsNoTracking()
            .Where(p => apartmentIds.Contains(p.ApartmentId))
            .ToListAsync(cancellationToken);

        var settings = await _db.AppSettings
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var annualRate = settings?.InterestRate ?? 0m;

        var items = new List<FormerResidentDebtItem>();

        foreach (var apartment in apartments)
        {
            var result = LedgerService.Calculate(
                apartment,
                invoices.Where(i => i.ApartmentId == apartment.Id),
                payments.Where(p => p.ApartmentId == apartment.Id),
                annualRate,
                asOf,
                charges.Where(c => c.ApartmentId == apartment.Id));

            // Borçlu satırlar sakin bazında gruplanır (devir bakiyesi OwnerId null olduğundan
            // otomatik dışlanır — o borç daire düzeyindedir, sakine izole edilemez).
            var debtorGroups = result.Lines
                .Where(l => l.OwnerId is { } ownerId && ownerId != Guid.Empty &&
                            l.RemainingPrincipal + l.InterestAccrued > 0m)
                .GroupBy(l => l.OwnerId!.Value);

            foreach (var group in debtorGroups)
            {
                var owner = apartment.Owners.FirstOrDefault(o => o.Id == group.Key);
                if (owner is null || owner.ValidTo is null)
                {
                    continue; // hâlâ aktif sakin → bu ekranın kapsamı dışı
                }

                var principal = group.Sum(l => Math.Max(0m, l.RemainingPrincipal));
                var interest = group.Sum(l => l.InterestAccrued);

                // Rücu hedefi: o anki aktif ev sahibi; yoksa dairenin son bilinen ev sahibi.
                var evSahibi = apartment.GetActiveResident(ResidentType.EvSahibi, now)
                    ?? apartment.Owners
                        .Where(o => o.ResidentType == ResidentType.EvSahibi)
                        .OrderByDescending(o => o.ValidFrom)
                        .FirstOrDefault();

                items.Add(new FormerResidentDebtItem(
                    apartment.Id,
                    apartment.DoorNumber,
                    apartment.Title,
                    owner.Id,
                    owner.OwnerName,
                    owner.ResidentType == ResidentType.Kiraci ? "Kiracı" : "Ev Sahibi",
                    owner.PhoneNumber,
                    owner.ValidTo,
                    principal,
                    interest,
                    principal + interest,
                    evSahibi?.OwnerName,
                    owner.ResidentType == ResidentType.Kiraci && evSahibi is not null));
            }
        }

        // En yüksek bakiye en üstte — yönetici en kritik eski borçluyu önce görür.
        return items.OrderByDescending(i => i.Balance).ToList();
    }
}
