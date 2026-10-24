using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;
using Westalife.Domain.Services.CostDistribution;

namespace Westalife.Application.Features.GasInvoices.Commands.CalculateCostDistribution;

public sealed class CalculateCostDistributionCommandHandler
    : IRequestHandler<CalculateCostDistributionCommand, CalculateCostDistributionResult>
{
    private readonly IApplicationDbContext _db;

    public CalculateCostDistributionCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<CalculateCostDistributionResult> Handle(
        CalculateCostDistributionCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _db.GasInvoices
            .FirstOrDefaultAsync(i => i.Id == request.GasInvoiceId, cancellationToken)
            ?? throw new DomainException("Belirtilen fatura bulunamadı.");

        // Dönem kilidi (Etap 6.6): kilitli dönem yeniden hesaplanamaz (denetim bütünlüğü).
        if (invoice.IsLocked)
        {
            throw new DomainException(DomainConstants.GasInvoice.RecalculateBlocked);
        }

        var building = await _db.Buildings
            .FirstOrDefaultAsync(b => b.IsActive, cancellationToken)
            ?? throw new DomainException("Aktif bina bulunamadı.");

        // --- SCD Type 2 çözümleme: dönem ORTA NOKTASINDA geçerli m² değerleri ---
        // (Exclusion constraint her daire için tek cevabı garanti eder.)
        var periodMidpoint = invoice.ReadingDateFrom
            .Add((invoice.ReadingDateTo - invoice.ReadingDateFrom) / 2);

        var apartmentsWithDimensions = await
            (from apartment in _db.Apartments
             where apartment.IsActive
             join dimension in _db.ApartmentDimensions
                 on apartment.Id equals dimension.ApartmentId
             where dimension.ValidFrom <= periodMidpoint
                && (dimension.ValidTo == null || dimension.ValidTo > periodMidpoint)
             select new { apartment.Id, apartment.DoorNumber, dimension.SquareMeters })
            .ToListAsync(cancellationToken);

        if (apartmentsWithDimensions.Count == 0)
        {
            throw new DomainException("Dönemde geçerli m² bilgisi olan daire bulunamadı.");
        }

        // --- Dönemsel tüketimler: fatura penceresi içindeki okumaların dönem tüketimi toplamı ---
        // Okunamayan (dönemde hiç okuması olmayan) daire: tüketim 0 + IsMeterReadFailed (onaylı strateji §3).
        var periodStart = invoice.ReadingDateFrom;
        var periodEnd = invoice.ReadingDateTo;

        var consumptionByApartment = await
            (from reading in _db.MeterReadings
             join meter in _db.Meters on reading.MeterId equals meter.Id
             where meter.ApartmentId != null
                && reading.ReadingDateUtc >= periodStart
                && reading.ReadingDateUtc < periodEnd
             group reading by meter.ApartmentId!.Value into g
             select new { ApartmentId = g.Key, TotalKWh = g.Sum(r => r.ConsumptionKWh) })
            .ToDictionaryAsync(x => x.ApartmentId, x => x.TotalKWh, cancellationToken);

        var inputItems = apartmentsWithDimensions
            .OrderBy(a => a.DoorNumber)
            .Select(a =>
            {
                var hasReading = consumptionByApartment.TryGetValue(a.Id, out var kwh);
                return new ApartmentConsumptionInfo(
                    a.Id,
                    int.Parse(a.DoorNumber),
                    a.SquareMeters,
                    hasReading ? kwh : 0m,
                    !hasReading);
            })
            .ToList();

        // --- Hesaplama motoru (Domain Service) — Kural #1 invariant'ı burada garanti edilir ---
        var totalKurus = (long)Math.Round(invoice.TotalAmount * 100m, MidpointRounding.AwayFromZero);
        var shares = CostDistributionService.Calculate(totalKurus, building.FixedCostRatio, inputItems);

        // --- Kalıcılık: eski aktif satırlar supersede edilir, yenileri yazılır (tek SaveChanges = tek transaction) ---
        var existingActive = await _db.ApartmentInvoices
            .Where(x => x.GasInvoiceId == invoice.Id && !x.IsSuperseded)
            .ToListAsync(cancellationToken);

        foreach (var old in existingActive)
        {
            old.Supersede();
        }

        var calculatedOn = DateTimeOffset.UtcNow;
        foreach (var share in shares)
        {
            _db.ApartmentInvoices.Add(new ApartmentInvoice(
                invoice.Id,
                share.ApartmentId,
                share.SquareMetersUsed,
                share.ConsumptionKWhUsed,
                share.FixedAmountKurus,
                share.ConsumptionAmountKurus,
                share.IsMeterReadFailed,
                calculatedOn));
        }

        await _db.SaveChangesAsync(cancellationToken);

        return new CalculateCostDistributionResult(
            shares.Count,
            shares.Count(s => s.IsMeterReadFailed),
            shares.Sum(s => s.TotalAmountKurus) / 100m,
            invoice.TotalAmount);
    }
}
