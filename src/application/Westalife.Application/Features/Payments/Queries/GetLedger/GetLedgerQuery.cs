using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Roles;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;
using Westalife.Domain.Services.Ledger;

namespace Westalife.Application.Features.Payments.Queries.GetLedger;

/// <summary>
/// Faz 6.8: Bir dairenin belirli bir tarihe kadar cari hesabını (Ledger) döner.
/// LedgerService domain servisi; FIFO allocation + basit faiz hesaplar (DB'ye yazmaz).
/// </summary>
public sealed record GetLedgerQuery(
    Guid ApartmentId,
    DateOnly? AsOfDate = null) : IRequest<LedgerResult>;

public sealed class GetLedgerQueryHandler : IRequestHandler<GetLedgerQuery, LedgerResult>
{
    private readonly IApplicationDbContext _db;

    public GetLedgerQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<LedgerResult> Handle(GetLedgerQuery request, CancellationToken cancellationToken)
    {
        var asOf = request.AsOfDate ?? DateOnly.FromDateTime(DateTime.Today);

        await WaiverOffsetSyncService.SyncAllAsync(_db, cancellationToken);

        // Daire (bakiye alanları ile)
        var apartment = await _db.Apartments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.ApartmentId, cancellationToken)
            ?? throw new NotFoundException("Daire bulunamadı.");

        // Aktif ApartmentInvoice satırları — GasInvoice + borç sahibi sakin navigation'ları ile
        var invoices = await _db.ApartmentInvoices
            .AsNoTracking()
            .Include(i => i.GasInvoice)
            .Include(i => i.ApartmentOwner)
            .Where(i => i.ApartmentId == request.ApartmentId && !i.IsSuperseded)
            .ToListAsync(cancellationToken);

        // Tüm ödemeler (iptal dahil — LedgerService içinde filtrelenir)
        var payments = await _db.Payments
            .AsNoTracking()
            .Include(p => p.Allocations)
            .Where(p => p.ApartmentId == request.ApartmentId)
            .ToListAsync(cancellationToken);

        // Faz 7.1: Aidat borç satırları (dönem vadesi + borç sahibi sakin ile)
        var aidatCharges = await _db.AidatCharges
            .AsNoTracking()
            .Include(c => c.AidatInvoice)
            .Include(c => c.ApartmentOwner)
            .Where(c => c.ApartmentId == request.ApartmentId)
            .ToListAsync(cancellationToken);

        // Faiz oranını ayarlardan oku
        var settings = await _db.AppSettings
            .AsNoTracking()
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var annualRate = settings?.InterestRate ?? 0m;

        var waivers = await _db.WaiverOffsets
            .AsNoTracking()
            .Where(w => w.ApartmentId == request.ApartmentId)
            .ToListAsync(cancellationToken);

        return LedgerService.Calculate(apartment, invoices, payments, annualRate, asOf, aidatCharges, waivers);
    }
}
