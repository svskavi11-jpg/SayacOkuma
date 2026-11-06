using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;
using Westalife.Domain.Services.Ledger;

namespace Westalife.Application.Features.Aidat.Commands.CancelAidatInvoice;

/// <summary>
/// Hatalı oluşturulan aidat tahakkukunu iptal eder (Soft Delete — denetim izi korunur).
/// <list type="bullet">
///   <item>İlgili bütçe dönemi kilitliyse (<see cref="BudgetPeriod.IsLocked"/>) engellenir.</item>
///   <item>Tahakkuka ait herhangi bir tahsilat (FIFO ile mahsup edilmiş ödeme veya doğrudan allocation) varsa engellenir.</item>
///   <item>Özel toplama fonuna bağlı gider yazılmışsa engellenir.</item>
///   <item>Tahsilat yoksa <see cref="AidatInvoice.IsCancelled"/> = true yapılır; daire borçları Cari Hesap'ta 0 TL bakiye ile "İptal Edilmiş Aidat Tahakkuku" olarak iz bırakır.</item>
/// </list>
/// </summary>
public sealed record CancelAidatInvoiceCommand(Guid Id) : IRequest<CancelAidatInvoiceResult>;

public sealed record CancelAidatInvoiceResult(
    Guid Id,
    bool IsCancelled,
    DateTimeOffset CancelledAt);

public sealed class CancelAidatInvoiceCommandHandler : IRequestHandler<CancelAidatInvoiceCommand, CancelAidatInvoiceResult>
{
    private readonly IApplicationDbContext _db;

    public CancelAidatInvoiceCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CancelAidatInvoiceResult> Handle(
        CancelAidatInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _db.AidatInvoices
            .Include(i => i.Charges)
            .FirstOrDefaultAsync(i => i.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException(DomainConstants.AidatInvoice.InvoiceNotFound);

        if (invoice.IsCancelled)
        {
            throw new DomainException(DomainConstants.AidatInvoice.AlreadyCancelled);
        }

        // 1. Dönem kilidi (IsLocked) kontrolü
        var startYear = invoice.PeriodStart.Year;
        var endYear = invoice.PeriodEnd.Year;
        var isLocked = await _db.BudgetPeriods
            .AsNoTracking()
            .AnyAsync(p => (p.Year == startYear || p.Year == endYear) && p.IsLocked, cancellationToken);

        if (isLocked)
        {
            throw new DomainException(DomainConstants.AidatInvoice.CancelBlockedByLockedPeriod);
        }

        // Özel toplama fonuna bağlı gider varsa iptal engellenir
        if (invoice.ChargeType == AidatChargeType.OzelToplama)
        {
            var hasFundExpenses = await _db.Expenses
                .AsNoTracking()
                .AnyAsync(e => e.FundAidatInvoiceId == invoice.Id, cancellationToken);

            if (hasFundExpenses)
            {
                throw new DomainException(DomainConstants.AidatInvoice.CancelBlockedByExpenses);
            }
        }

        // 2. Tahsilat (Payment) kontrolü — doğrudan allocation veya Ledger FIFO mahsubu
        var chargeIds = invoice.Charges.Select(c => c.Id).ToList();
        var apartmentIds = invoice.Charges.Select(c => c.ApartmentId).Distinct().ToList();

        if (chargeIds.Count > 0)
        {
            var hasDirectAllocation = await _db.PaymentAllocations
                .AsNoTracking()
                .AnyAsync(
                    a => !a.IsVoided
                        && a.AidatChargeId != null
                        && chargeIds.Contains(a.AidatChargeId.Value),
                    cancellationToken);

            if (hasDirectAllocation)
            {
                throw new DomainException(DomainConstants.AidatInvoice.CancelBlockedByPayments);
            }

            // FIFO motoru üzerinden kontrol: bu tahakkukun herhangi bir daire borcuna ödeme düşmüş mü?
            var apartments = await _db.Apartments
                .AsNoTracking()
                .Where(a => apartmentIds.Contains(a.Id))
                .ToDictionaryAsync(a => a.Id, cancellationToken);

            var allApartmentCharges = await _db.AidatCharges
                .AsNoTracking()
                .Include(c => c.AidatInvoice)
                .Where(c => apartmentIds.Contains(c.ApartmentId))
                .ToListAsync(cancellationToken);

            var apartmentInvoices = await _db.ApartmentInvoices
                .AsNoTracking()
                .Include(i => i.GasInvoice)
                .Where(i => !i.IsSuperseded && apartmentIds.Contains(i.ApartmentId))
                .ToListAsync(cancellationToken);

            var payments = await _db.Payments
                .AsNoTracking()
                .Where(p => !p.IsCancelled && apartmentIds.Contains(p.ApartmentId))
                .ToListAsync(cancellationToken);

            var allChargeIds = allApartmentCharges.Select(c => c.Id).ToList();
            var waivers = await _db.WaiverOffsets
                .AsNoTracking()
                .Where(w => allChargeIds.Contains(w.AidatChargeId))
                .ToListAsync(cancellationToken);

            var targetChargeIdStrings = chargeIds.Select(id => id.ToString()).ToHashSet();
            var asOf = DateOnly.FromDateTime(DateTime.Today);

            foreach (var aptId in apartmentIds)
            {
                if (!apartments.TryGetValue(aptId, out var apartment))
                {
                    continue;
                }

                var aptPayments = payments.Where(p => p.ApartmentId == aptId).ToList();
                if (aptPayments.Count == 0)
                {
                    continue;
                }

                var ledger = LedgerService.Calculate(
                    apartment,
                    apartmentInvoices.Where(i => i.ApartmentId == aptId),
                    aptPayments,
                    annualInterestRate: 0m,
                    asOfDate: asOf,
                    aidatCharges: allApartmentCharges.Where(c => c.ApartmentId == aptId),
                    waivers: waivers.Where(w => w.ApartmentId == aptId));

                var hasFifoPayment = ledger.Lines.Any(
                    l => l.Type == LedgerDebtType.Aidat
                        && targetChargeIdStrings.Contains(l.SourceId)
                        && l.PaidAmount > 0m);

                if (hasFifoPayment)
                {
                    throw new DomainException(DomainConstants.AidatInvoice.CancelBlockedByPayments);
                }
            }
        }

        // 3. Tahakkuku iptal et (Soft Delete)
        invoice.Cancel();
        await _db.SaveChangesAsync(cancellationToken);

        return new CancelAidatInvoiceResult(
            invoice.Id,
            invoice.IsCancelled,
            invoice.CancelledAt!.Value);
    }
}

