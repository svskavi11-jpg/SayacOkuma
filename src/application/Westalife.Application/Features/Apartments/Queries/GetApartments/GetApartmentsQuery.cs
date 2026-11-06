using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Roles;
using Westalife.Domain.Constants;
using Westalife.Domain.Services.Ledger;

namespace Westalife.Application.Features.Apartments.Queries.GetApartments;

/// <summary>Daire listesi + aktif SCD m² + bağlı sayaçlar (Faz 5 yönetim grid'inin veri kaynağı).</summary>
public sealed record GetApartmentsQuery : IRequest<IReadOnlyList<ApartmentListItem>>;

public sealed record ApartmentListItem(
    Guid Id,
    string DoorNumber,
    string Title,
    decimal? ActiveSquareMeters,
    string? OwnerName,
    Guid? ActiveMeterId,
    decimal? LastIndexValue,
    DateTimeOffset? LastReadingDateUtc,
    IReadOnlyList<ApartmentMeterItem> Meters,
    decimal OpeningBalancePrincipal = 0m,
    decimal OpeningBalanceInterest = 0m,
    DateOnly? OpeningBalanceDate = null,
    Guid? AidatTariffId = null,
    string? AidatTariffName = null,
    string? KiraciName = null,
    string? EvSahibiPhoneNumber = null,
    string? EvSahibiEmail = null,
    string? EvSahibiIdentityNumber = null,
    string? EvSahibiNotes = null,
    string? KiraciPhoneNumber = null,
    string? KiraciEmail = null,
    string? KiraciIdentityNumber = null,
    string? KiraciNotes = null,
    decimal CurrentBalance = 0m,
    string BalanceStatus = DomainConstants.Apartment.BalanceSettled,
    decimal AidatDebt = 0m,
    decimal HeatingDebt = 0m,
    Guid? OpeningBalanceOwnerId = null);

public sealed record ApartmentMeterItem(Guid Id, string SerialNumber, string MeterType, int? MbusPrimaryAddress, bool IsActive);

public sealed class GetApartmentsQueryHandler : IRequestHandler<GetApartmentsQuery, IReadOnlyList<ApartmentListItem>>
{
    private readonly IApplicationDbContext _db;

    public GetApartmentsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ApartmentListItem>> Handle(
        GetApartmentsQuery request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        await WaiverOffsetSyncService.SyncAllAsync(_db, cancellationToken);

        var apartments = await _db.Apartments
            .Where(a => a.IsActive)
            // Not: kapı numarası metin olduğundan sıralama bellekte sayısal yapılır (1, 2, ... 10).
            .Select(a => new
            {
                a.Id,
                a.DoorNumber,
                a.Title,
                a.OpeningBalancePrincipal,
                a.OpeningBalanceInterest,
                a.OpeningBalanceDate,
                a.OpeningBalanceOwnerId,
                AidatTariffId = (Guid?)a.AidatTariffId,
                AidatTariffName = a.AidatTariff != null ? a.AidatTariff.Name : null,
                ActiveSquareMeters = a.Dimensions
                    .Where(d => d.ValidTo == null)
                    .Select(d => (decimal?)d.SquareMeters)
                    .FirstOrDefault(),
                // Etap 6.7: aktif kat maliki (SCD Type 2 — ValidTo NULL kayıt).
                OwnerName = a.Owners
                    .Where(o => o.ValidTo == null)
                    .Select(o => (string?)o.OwnerName)
                    .FirstOrDefault(),
                // Faz 7.1: aktif sakin bilgileri (dropdown etiketleri + iletişim).
                EvSahibiName = a.Owners
                    .Where(o => o.ResidentType == Domain.Enums.ResidentType.EvSahibi &&
                                o.ValidFrom <= now && (o.ValidTo == null || o.ValidTo > now))
                    .Select(o => (string?)o.OwnerName)
                    .FirstOrDefault(),
                EvSahibiPhoneNumber = a.Owners
                    .Where(o => o.ResidentType == Domain.Enums.ResidentType.EvSahibi &&
                                o.ValidFrom <= now && (o.ValidTo == null || o.ValidTo > now))
                    .Select(o => o.PhoneNumber)
                    .FirstOrDefault(),
                EvSahibiEmail = a.Owners
                    .Where(o => o.ResidentType == Domain.Enums.ResidentType.EvSahibi &&
                                o.ValidFrom <= now && (o.ValidTo == null || o.ValidTo > now))
                    .Select(o => o.Email)
                    .FirstOrDefault(),
                EvSahibiIdentityNumber = a.Owners
                    .Where(o => o.ResidentType == Domain.Enums.ResidentType.EvSahibi &&
                                o.ValidFrom <= now && (o.ValidTo == null || o.ValidTo > now))
                    .Select(o => o.IdentityNumber)
                    .FirstOrDefault(),
                EvSahibiNotes = a.Owners
                    .Where(o => o.ResidentType == Domain.Enums.ResidentType.EvSahibi &&
                                o.ValidFrom <= now && (o.ValidTo == null || o.ValidTo > now))
                    .Select(o => o.Notes)
                    .FirstOrDefault(),
                KiraciName = a.Owners
                    .Where(o => o.ResidentType == Domain.Enums.ResidentType.Kiraci &&
                                o.ValidFrom <= now && (o.ValidTo == null || o.ValidTo > now))
                    .Select(o => (string?)o.OwnerName)
                    .FirstOrDefault(),
                KiraciPhoneNumber = a.Owners
                    .Where(o => o.ResidentType == Domain.Enums.ResidentType.Kiraci &&
                                o.ValidFrom <= now && (o.ValidTo == null || o.ValidTo > now))
                    .Select(o => o.PhoneNumber)
                    .FirstOrDefault(),
                KiraciEmail = a.Owners
                    .Where(o => o.ResidentType == Domain.Enums.ResidentType.Kiraci &&
                                o.ValidFrom <= now && (o.ValidTo == null || o.ValidTo > now))
                    .Select(o => o.Email)
                    .FirstOrDefault(),
                KiraciIdentityNumber = a.Owners
                    .Where(o => o.ResidentType == Domain.Enums.ResidentType.Kiraci &&
                                o.ValidFrom <= now && (o.ValidTo == null || o.ValidTo > now))
                    .Select(o => o.IdentityNumber)
                    .FirstOrDefault(),
                KiraciNotes = a.Owners
                    .Where(o => o.ResidentType == Domain.Enums.ResidentType.Kiraci &&
                                o.ValidFrom <= now && (o.ValidTo == null || o.ValidTo > now))
                    .Select(o => o.Notes)
                    .FirstOrDefault(),
                ActiveMeter = a.Meters
                    .Where(m => m.IsActive && m.Type == Domain.Enums.MeterType.Calorimeter)
                    .OrderByDescending(m => m.CreatedOn)
                    .Select(m => new
                    {
                        m.Id,
                        LastIndex = m.Readings
                            .OrderByDescending(r => r.ReadingDateUtc)
                            .Select(r => (decimal?)r.IndexValue)
                            .FirstOrDefault(),
                        LastDate = m.Readings
                            .OrderByDescending(r => r.ReadingDateUtc)
                            .Select(r => (DateTimeOffset?)r.ReadingDateUtc)
                            .FirstOrDefault()
                    })
                    .FirstOrDefault(),
                Meters = a.Meters
                    .Select(m => new ApartmentMeterItem(
                        m.Id,
                        m.SerialNumber,
                        m.Type.ToString(),
                        m.MbusPrimaryAddress,
                        m.IsActive))
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var balances = await CalculateBalances(apartments.Select(a => a.Id).ToList(), cancellationToken);

        return apartments
            .OrderByDoorNumber(a => a.DoorNumber)
            .Select(a =>
            {
                var totals = balances.GetValueOrDefault(a.Id);
                return new ApartmentListItem(
                    a.Id, a.DoorNumber, a.Title, a.ActiveSquareMeters, a.OwnerName,
                    a.ActiveMeter?.Id, a.ActiveMeter?.LastIndex, a.ActiveMeter?.LastDate, a.Meters,
                    a.OpeningBalancePrincipal, a.OpeningBalanceInterest, a.OpeningBalanceDate,
                    a.AidatTariffId, a.AidatTariffName,
                    a.KiraciName, a.EvSahibiPhoneNumber, a.EvSahibiEmail, a.EvSahibiIdentityNumber, a.EvSahibiNotes,
                    a.KiraciPhoneNumber, a.KiraciEmail, a.KiraciIdentityNumber, a.KiraciNotes,
                    totals.Balance, ApartmentBalance.Status(totals.Balance),
                    totals.AidatDebt, totals.HeatingDebt, a.OpeningBalanceOwnerId);
            })
            .ToList();
    }

    private readonly record struct ApartmentLedgerTotals(decimal Balance, decimal AidatDebt, decimal HeatingDebt);

    private async Task<Dictionary<Guid, ApartmentLedgerTotals>> CalculateBalances(
        IReadOnlyList<Guid> apartmentIds, CancellationToken cancellationToken)
    {
        if (apartmentIds.Count == 0)
        {
            return [];
        }

        var settings = await _db.AppSettings.AsNoTracking()
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var annualRate = settings?.InterestRate ?? 0m;
        var asOf = DateOnly.FromDateTime(DateTime.Today);

        var apartmentEntities = await _db.Apartments.AsNoTracking()
            .Where(a => apartmentIds.Contains(a.Id))
            .ToListAsync(cancellationToken);

        var invoices = await _db.ApartmentInvoices.AsNoTracking()
            .Include(i => i.GasInvoice)
            .Where(i => apartmentIds.Contains(i.ApartmentId) && !i.IsSuperseded)
            .ToListAsync(cancellationToken);

        var payments = await _db.Payments.AsNoTracking()
            .Where(p => apartmentIds.Contains(p.ApartmentId))
            .ToListAsync(cancellationToken);

        var charges = await _db.AidatCharges.AsNoTracking()
            .Include(c => c.AidatInvoice)
            .Where(c => apartmentIds.Contains(c.ApartmentId))
            .ToListAsync(cancellationToken);

        var invoicesByApartment = invoices.GroupBy(i => i.ApartmentId).ToDictionary(g => g.Key, g => (IReadOnlyList<Domain.Entities.ApartmentInvoice>)g.ToList());
        var paymentsByApartment = payments.GroupBy(p => p.ApartmentId).ToDictionary(g => g.Key, g => (IReadOnlyList<Domain.Entities.Payment>)g.ToList());
        var chargesByApartment = charges.GroupBy(c => c.ApartmentId).ToDictionary(g => g.Key, g => (IReadOnlyList<Domain.Entities.AidatCharge>)g.ToList());

        var waivers = await _db.WaiverOffsets.AsNoTracking()
            .Where(w => apartmentIds.Contains(w.ApartmentId))
            .ToListAsync(cancellationToken);
        var waiversByApartment = waivers.GroupBy(w => w.ApartmentId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Domain.Entities.WaiverOffset>)g.ToList());

        var balances = new Dictionary<Guid, ApartmentLedgerTotals>(apartmentEntities.Count);
        foreach (var apartment in apartmentEntities)
        {
            var ledger = LedgerService.Calculate(
                apartment,
                invoicesByApartment.GetValueOrDefault(apartment.Id) ?? [],
                paymentsByApartment.GetValueOrDefault(apartment.Id) ?? [],
                annualRate,
                asOf,
                chargesByApartment.GetValueOrDefault(apartment.Id) ?? [],
                waiversByApartment.GetValueOrDefault(apartment.Id) ?? []);
            var summary = ledger.Summary;
            balances[apartment.Id] = new ApartmentLedgerTotals(
                ledger.TotalBalance,
                (summary?.AidatPrincipalDue ?? 0m) + (summary?.AidatInterestAccrued ?? 0m),
                (summary?.InvoicePrincipalDue ?? 0m) + (summary?.InvoiceInterestAccrued ?? 0m));
        }

        return balances;
    }
}
