using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Westalife.Application.Common.Enums;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Common.Models;
using Westalife.Application;
using Westalife.Application.Features.GasInvoices.Commands.AddGasInvoice;
using Westalife.Application.Features.GasInvoices.Commands.CalculateCostDistribution;
using Westalife.Application.Features.MeterReadings.Commands.RecordMeterReadings;
using Westalife.Application.Features.Meters.Commands.ImportMeters;
using Westalife.Domain.Enums;
using Westalife.Domain.Services.CostDistribution;
using Microsoft.EntityFrameworkCore;
using Westalife.Infrastructure.Persistence;
using Xunit;

namespace Westalife.Infrastructure.Tests;

/// <summary>
/// UÃ‡TAN UCA entegrasyon testi (gerÃ§ek PostgreSQL):
/// ImportMeters â†’ RecordMeterReadings (bir daire okunmayacak) â†’ AddGasInvoice â†’ CalculateCostDistribution.
/// Kural #1: Î£(daire borÃ§larÄ±) == fatura tutarÄ± (5.545,20 TL) â€” kuruÅŸu kuruÅŸuna.
/// </summary>
public class CostDistributionIntegrationTests
{
    [Fact]
    public async Task FullPipeline_WithRealDatabase_ReconcilesInvoiceToThePenny()
    {
        // --- Arrangement: gerÃ§ek DI kabÄ± (PostgreSQL: localhost:55432) ---
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(DesignTimeDbContextFactory.DesignTimeConnectionString);
        services.AddApplication();
        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var runStamp = Guid.NewGuid().ToString("N")[..8];

        // 1) 72 sayacÄ± iÃ§e aktar (seri no unique â€” tekrar Ã§alÄ±ÅŸtÄ±rmada skip edilir)
        var items = Enumerable.Range(1, 72)
            .Select(i => new ImportedMeterItem(i, $"E2EFIXED{i:D4}", i))
            .ToList();

        var importResult = await mediator.Send(new ImportMetersFromIzarCenterCommand(items));
        Assert.True(importResult.ImportedCount is 72 or 0); // ilk Ã§alÄ±ÅŸtÄ±rma 72, sonrakiler 0 (zaten kayÄ±tlÄ±)

        // 2) Okumalar: dÃ¶nemden Ã¶nce bir endeks + dÃ¶nem iÃ§inde bir endeks â†’ dÃ¶nem tÃ¼ketimi tÃ¼retilir.
        //    KapÄ± 72 bilinÃ§li olarak OKUNMAYACAK (kÄ±smi baÅŸarÄ± senaryosu).
        var db = provider.GetRequiredService<IApplicationDbContext>();

        // Kalici test DB temizligi: onceki kosullardan kalmis donem kilitlerini ac.
        // (Kilitli pencere, asagida hesaplanan okuma tarihlerini kapsarsa tum okumalar
        // LockedPeriodSkipped sayilir ve InsertedCount=0 olur.)
        await db.GasInvoices.Where(i => i.IsLocked).ExecuteUpdateAsync(s => s
            .SetProperty(i => i.IsLocked, false)
            .SetProperty(i => i.LockedOn, (DateTimeOffset?)null));

        var lastPeriodEnd = await db.GasInvoices.MaxAsync(i => (DateTimeOffset?)i.ReadingDateTo) ?? DateTimeOffset.UtcNow.AddDays(-365);
        // Tekrar koşum dayanikliligi: onceki basarisiz kosum okumalari ekleyip faturayi
        // olusturamadiysa ayni tarihler duplicate sayilir (InsertedCount=0 dongusu).
        // Bu yuzden okuma tarihleri bosalana kadar donem birer gun ileri kaydirilir.
        var periodFrom = lastPeriodEnd.AddDays(1);
        while (await db.MeterReadings.AnyAsync(r =>
            r.ReadingDateUtc == periodFrom.AddDays(-10) || r.ReadingDateUtc == periodFrom.AddDays(15)))
        {
            periodFrom = periodFrom.AddDays(1);
        }
        var periodTo = periodFrom.AddDays(30);
        var readingBefore = periodFrom.AddDays(-10);
        var readingInside = periodFrom.AddDays(15);

        var snapshots = items.Where(i => i.DoorNumber != 72)
            .Select((item, idx) => new MeterSnapshot
            {
                SerialNumber = item.SerialNumber,
                PrimaryAddress = (byte)item.PrimaryAddress,
                ReadingTimestampUtc = readingInside,
                CumulativeEnergyKWh = 1000m + idx * 50m + 850m, // dÃ¶nem tÃ¼ketimi = 850 kWh
                CumulativeVolumeM3 = 0m,
            }).ToList();

        // DÃ¶nem Ã¶ncesi referans endeksler (tÃ¼m daireler dahil, kapÄ± 72 de)
        var beforeSnapshots = items.Select((item, idx) => new MeterSnapshot
        {
            SerialNumber = item.SerialNumber,
            PrimaryAddress = (byte)item.PrimaryAddress,
            ReadingTimestampUtc = readingBefore,
            CumulativeEnergyKWh = 1000m + idx * 50m,
            CumulativeVolumeM3 = 0m,
        }).ToList();

        var beforeResult = await mediator.Send(new RecordMeterReadingsCommand(beforeSnapshots, ReadingSource.ManualEntry));
        var insideResult = await mediator.Send(new RecordMeterReadingsCommand(snapshots, ReadingSource.ManualEntry));
        Assert.Equal(72, beforeResult.InsertedCount);
        Assert.Equal(71, insideResult.InsertedCount); // kapÄ± 72 dÃ¶nem iÃ§inde okunmadÄ±

        // 3) GerÃ§ek Akmercan faturasÄ± (T6772024000027623 deÄŸerleri)
        var invoiceId = await mediator.Send(new AddGasInvoiceCommand(
            $"E2E-{runStamp}",
            "10060828",
            "2160191",
            periodFrom,
            periodTo,
            DateTimeOffset.UtcNow,
            CorrectedVolumeM3: 693.25m,
            TotalEnergyKWh: 7273.65m,
            PressureCorrectionFactor: 1.225880m,
            ConsumptionAmount: 4091.54m,
            ConsumptionVat: 924.20m,
            DistributionAmount: 529.46m,
            DistributionVat: 105.89m,
            TotalAmount: 5545.20m));

        // 4) Hesaplama
        var result = await mediator.Send(new CalculateCostDistributionCommand(invoiceId));

        // --- Kural #1: kuruÅŸu kuruÅŸuna mutabakat ---
        Assert.True(result.Reconciled, $"DaÄŸÄ±tÄ±lan {result.TotalDistributedAmount} != Fatura {result.InvoiceTotalAmount}");
        Assert.Equal(5545.20m, result.InvoiceTotalAmount);
        Assert.Equal(72, result.ApartmentCount);
        Assert.Equal(1, result.FailedMeterCount); // kapÄ± 72

        // --- DB doÄŸrulamasÄ±: 72 aktif satÄ±r, Î£(TotalAmount) == 5.545,20 ---
        var rows = db.ApartmentInvoices
            .Where(x => x.GasInvoiceId == invoiceId && !x.IsSuperseded)
            .ToList();

        Assert.Equal(72, rows.Count);
        Assert.Equal(5545.20m, rows.Sum(r => r.TotalAmount));
        Assert.Equal(1, rows.Count(r => r.IsMeterReadFailed));

        // Okunamayan daire tÃ¼ketim payÄ± alamaz ama sabit payÄ± pozitiftir (onaylÄ± strateji Â§3).
        var failedRow = rows.Single(r => r.IsMeterReadFailed);
        Assert.Equal(0m, failedRow.ConsumptionAmount);
        Assert.True(failedRow.FixedAmount > 0);
    }
}
