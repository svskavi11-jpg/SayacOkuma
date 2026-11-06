using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Roles;
using Westalife.Domain.Entities;
using Westalife.Domain.Services.Ledger;
using Westalife.Domain.Services.Reports;

namespace Westalife.Application.Features.Reports;

internal sealed record AuditBundle(
    string BuildingName,
    decimal InterestRate,
    IReadOnlyList<Apartment> Apartments,
    IReadOnlyList<ApartmentInvoice> Invoices,
    IReadOnlyList<Payment> Payments,
    IReadOnlyList<AidatCharge> Charges,
    IReadOnlyList<WaiverOffset> Waivers);

internal static class AuditBooks
{
    public static async Task<AuditBundle> LoadAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        await WaiverOffsetSyncService.SyncAllAsync(db, cancellationToken);

        var settings = await db.AppSettings.AsNoTracking()
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var apartments = await db.Apartments.AsNoTracking().ToListAsync(cancellationToken);
        var invoices = await db.ApartmentInvoices.AsNoTracking()
            .Include(i => i.GasInvoice)
            .Where(i => !i.IsSuperseded)
            .ToListAsync(cancellationToken);
        var payments = await db.Payments.AsNoTracking().ToListAsync(cancellationToken);
        var charges = await db.AidatCharges.AsNoTracking()
            .Include(c => c.AidatInvoice)
            .ToListAsync(cancellationToken);
        var waivers = await db.WaiverOffsets.AsNoTracking().ToListAsync(cancellationToken);

        return new AuditBundle(
            settings?.BuildingName ?? string.Empty,
            settings?.InterestRate ?? 0m,
            apartments,
            invoices,
            payments,
            charges,
            waivers);
    }

    public static DateOnly FuelPeriodStart(ApartmentInvoice invoice)
    {
        if (invoice.GasInvoice is null)
        {
            return DateOnly.FromDateTime(invoice.CalculatedOnUtc.UtcDateTime);
        }

        return DateOnly.FromDateTime(invoice.GasInvoice.ReadingDateFrom.UtcDateTime);
    }

    public static bool FuelInRange(ApartmentInvoice invoice, DateOnly from, DateOnly to, Guid? gasInvoiceId)
    {
        if (gasInvoiceId is { } id && invoice.GasInvoiceId != id)
        {
            return false;
        }

        return AuditTrackingMath.InRange(FuelPeriodStart(invoice), from, to);
    }

    public static DateOnly FuelPeriodEnd(ApartmentInvoice invoice)
    {
        if (invoice.GasInvoice is null)
        {
            return FuelPeriodStart(invoice);
        }

        return DateOnly.FromDateTime(invoice.GasInvoice.ReadingDateTo.UtcDateTime);
    }

    public static LedgerResult Snapshot(AuditBundle books, Apartment apartment, DateOnly asOf)
    {
        var invoices = books.Invoices.Where(i =>
            i.ApartmentId == apartment.Id && FuelPeriodStart(i) <= asOf);
        var charges = books.Charges.Where(c =>
            c.ApartmentId == apartment.Id && c.AidatInvoice.PeriodStart <= asOf);
        var payments = books.Payments.Where(p =>
            p.ApartmentId == apartment.Id && p.PaymentDate <= asOf);
        var waivers = books.Waivers.Where(w =>
            w.ApartmentId == apartment.Id && w.OffsetDate <= asOf);

        return LedgerService.Calculate(
            apartment,
            invoices,
            payments,
            books.InterestRate,
            asOf,
            charges,
            waivers,
            cutOpeningAtAsOf: true);
    }

    public static LedgerResult Full(AuditBundle books, Apartment apartment, DateOnly asOf)
    {
        return LedgerService.Calculate(
            apartment,
            books.Invoices.Where(i => i.ApartmentId == apartment.Id),
            books.Payments.Where(p => p.ApartmentId == apartment.Id),
            books.InterestRate,
            asOf,
            books.Charges.Where(c => c.ApartmentId == apartment.Id),
            books.Waivers.Where(w => w.ApartmentId == apartment.Id));
    }

    public static decimal Collected(
        LedgerResult ledger,
        DateOnly from,
        DateOnly to,
        params AllocationTarget[] targets)
    {
        var allowed = targets.ToHashSet();
        return ledger.Lines
            .SelectMany(l => l.Allocations)
            .Where(a => a.PaymentId != Guid.Empty && allowed.Contains(a.Target) && AuditTrackingMath.InRange(a.PaymentDate, from, to))
            .Sum(a => a.AllocatedAmount);
    }

    public static decimal Unallocated(LedgerResult ledger, DateOnly from, DateOnly to) =>
        (ledger.Credits ?? [])
            .Where(c => AuditTrackingMath.InRange(c.PaymentDate, from, to))
            .Sum(c => c.Amount);
}
