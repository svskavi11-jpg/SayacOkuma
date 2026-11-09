using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Roles;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Enums;
using Westalife.Domain.Services.Ledger;

namespace Westalife.Application.Features.Notifications;

/// <summary>
/// Seçilen daireler için alıcı ve cari bakiye bağlamını hazırlar.
/// Bir dairenin hesabı hesaplanamazsa diğer daireler etkilenmez.
/// </summary>
public static class NotificationContextLoader
{
    public static async Task<IReadOnlyList<ApartmentNotificationContext>> LoadAsync(
        IApplicationDbContext db,
        IReadOnlyList<Guid> apartmentIds,
        CancellationToken cancellationToken)
    {
        var ids = apartmentIds.Where(id => id != Guid.Empty).Distinct().ToList();
        await WaiverOffsetSyncService.SyncAllAsync(db, cancellationToken);

        var apartments = await db.Apartments.AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .ToListAsync(cancellationToken);
        var owners = await db.ApartmentOwners.AsNoTracking()
            .Where(o => ids.Contains(o.ApartmentId) && o.ValidTo == null)
            .ToListAsync(cancellationToken);
        var invoices = await db.ApartmentInvoices.AsNoTracking()
            .Include(i => i.GasInvoice)
            .Include(i => i.ApartmentOwner)
            .Where(i => ids.Contains(i.ApartmentId) && !i.IsSuperseded)
            .ToListAsync(cancellationToken);
        var payments = await db.Payments.AsNoTracking()
            .Include(p => p.Allocations)
            .Where(p => ids.Contains(p.ApartmentId))
            .ToListAsync(cancellationToken);
        var charges = await db.AidatCharges.AsNoTracking()
            .Include(c => c.AidatInvoice)
            .Include(c => c.ApartmentOwner)
            .Where(c => ids.Contains(c.ApartmentId))
            .ToListAsync(cancellationToken);
        var waivers = await db.WaiverOffsets.AsNoTracking()
            .Where(w => ids.Contains(w.ApartmentId))
            .ToListAsync(cancellationToken);

        var settings = await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var annualRate = settings?.InterestRate ?? 0m;
        var asOf = DateOnly.FromDateTime(DateTime.Today);
        var period = NotificationPlaceholderRenderer.FormatPeriod(
            settings?.ManagementPeriodStart, settings?.ManagementPeriodEnd, asOf);
        var currentMonth = NotificationPlaceholderRenderer.FormatCurrentMonth(asOf);

        var result = new List<ApartmentNotificationContext>(ids.Count);
        foreach (var id in ids)
        {
            var apartment = apartments.FirstOrDefault(a => a.Id == id);
            if (apartment is null)
            {
                result.Add(ApartmentNotificationContext.Failed(id, DomainConstants.Notification.ApartmentNotFound));
                continue;
            }

            try
            {
                var ledger = LedgerService.Calculate(
                    apartment,
                    invoices.Where(i => i.ApartmentId == id),
                    payments.Where(p => p.ApartmentId == id),
                    annualRate,
                    asOf,
                    charges.Where(c => c.ApartmentId == id),
                    waivers.Where(w => w.ApartmentId == id));

                var resident = ResolveResident(owners.Where(o => o.ApartmentId == id));
                var money = NotificationPlaceholderRenderer.ReadAmounts(
                    ledger,
                    charges.Where(c => c.ApartmentId == id),
                    invoices.Where(i => i.ApartmentId == id),
                    asOf);
                result.Add(new ApartmentNotificationContext(
                    id,
                    apartment.DoorNumber,
                    string.IsNullOrWhiteSpace(resident?.OwnerName) ? "Sakin" : resident!.OwnerName,
                    resident?.Email,
                    resident?.PhoneNumber,
                    ledger.TotalBalance,
                    money.AidatDebt,
                    money.HeatingDebt,
                    money.CurrentAidat,
                    money.CurrentHeating,
                    period,
                    currentMonth,
                    null));
            }
            catch (Exception ex)
            {
                result.Add(ApartmentNotificationContext.Failed(id, apartment.DoorNumber, ex.Message));
            }
        }

        return result;
    }

    /// <summary>Aktif kiracı varsa kiracı, yoksa aktif ev sahibi.</summary>
    public static ApartmentOwner? ResolveResident(IEnumerable<ApartmentOwner> owners)
    {
        var active = owners.Where(o => o.ValidTo is null).ToList();
        return active.FirstOrDefault(o => o.ResidentType == ResidentType.Kiraci)
            ?? active.FirstOrDefault(o => o.ResidentType == ResidentType.EvSahibi);
    }
}

public sealed record ApartmentNotificationContext(
    Guid ApartmentId,
    string DoorNumber,
    string FullName,
    string? Email,
    string? Phone,
    decimal Balance,
    decimal AidatDebt,
    decimal HeatingDebt,
    decimal CurrentAidat,
    decimal CurrentHeating,
    string PeriodLabel,
    string CurrentMonthLabel,
    string? LoadError,
    string? PdfLink = null)
{
    public static ApartmentNotificationContext Failed(Guid apartmentId, string error) =>
        new(apartmentId, string.Empty, string.Empty, null, null, 0m, 0m, 0m, 0m, 0m, string.Empty, string.Empty, error);

    public static ApartmentNotificationContext Failed(Guid apartmentId, string doorNumber, string error) =>
        new(apartmentId, doorNumber, string.Empty, null, null, 0m, 0m, 0m, 0m, 0m, string.Empty, string.Empty, error);

    public ApartmentNotificationContext WithPdfLink(string? pdfLink) =>
        this with { PdfLink = pdfLink };

    public NotificationPlaceholderValues ToValues() => new(
        string.IsNullOrWhiteSpace(FullName) ? "Sakin" : FullName,
        DoorNumber,
        Balance,
        PeriodLabel,
        AidatDebt,
        HeatingDebt,
        CurrentAidat,
        CurrentHeating,
        CurrentMonthLabel,
        PdfLink);
}
