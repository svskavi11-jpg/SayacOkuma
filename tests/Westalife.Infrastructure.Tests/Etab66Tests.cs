using ClosedXML.Excel;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Westalife.Application;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.GasInvoices.Commands.AddGasInvoice;
using Westalife.Application.Features.GasInvoices.Commands.CalculateCostDistribution;
using Westalife.Application.Features.GasInvoices.Commands.LockPeriod;
using Westalife.Application.Features.GasInvoices.Queries.GetGasInvoices;
using Westalife.Application.Features.MeterReadings.Commands.AddManualMeterReading;
using Westalife.Application.Features.MeterReadings.Commands.ImportReadingsFromExcel;
using Westalife.Application.Features.Meters.Commands.ImportMeters;
using Westalife.Domain.Constants;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;
using Westalife.Infrastructure.Persistence;
using Xunit;

namespace Westalife.Infrastructure.Tests;

/// <summary>Etap 6.6: Manuel okuma, Excel okuma içe aktarma ve dönem kilitleme entegrasyon testleri.</summary>
public class Etab66Tests
{
    private static (IMediator Mediator, IApplicationDbContext Db) Build()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(DesignTimeDbContextFactory.DesignTimeConnectionString);
        services.AddApplication();
        var provider = services.BuildServiceProvider();
        var db = provider.GetRequiredService<IApplicationDbContext>();

        // Kalıcı test DB temizliği: önceki koşullardan kalmış dönem kilitlerini aç
        // (Etap 6.7 testleriyle aynı DB paylaşıldığından kilitli dönemler çakışabilir).
        db.GasInvoices.Where(i => i.IsLocked).ExecuteUpdate(s => s
            .SetProperty(i => i.IsLocked, false)
            .SetProperty(i => i.LockedOn, (DateTimeOffset?)null));

        return (provider.GetRequiredService<IMediator>(), db);
    }

    private static byte[] BuildExcel(string[] headers, string[][] rows)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Okuma Listesi");
        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
        }

        for (var r = 0; r < rows.Length; r++)
        {
            for (var c = 0; c < rows[r].Length; c++)
            {
                ws.Cell(r + 2, c + 1).Value = rows[r][c];
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>Test için rastgele bir daireye sayaç içe aktarır (apartmentId + meterId döner).</summary>
    private static async Task<(Guid ApartmentId, Guid MeterId)> ImportRandomMeterAsync(
        IMediator mediator, IApplicationDbContext db)
    {
        var random = new Random();
        var door = random.Next(1, 73);
        var serial = $"E66{random.Next(100000, 999999)}";
        var address = random.Next(1, 251);

        await mediator.Send(new ImportMetersFromIzarCenterCommand(
            [new ImportedMeterItem(door, serial, address)]));

        var apartment = await db.Apartments.FirstAsync(a => a.DoorNumber == door.ToString());
        var meter = await db.Meters.FirstAsync(m => m.SerialNumber == serial);
        return (apartment.Id, meter.Id);
    }

    /// <summary>Çakışmayan rastgele fatura dönemi üretir (exclusion constraint'e takılmamak için).</summary>
    private static async Task<(DateTimeOffset From, DateTimeOffset To)> RandomFreePeriodAsync(
        IApplicationDbContext db)
    {
        var random = new Random();
        var from = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero)
            .AddDays(random.Next(0, 270));
        var to = from.AddDays(3);

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var overlaps = await db.GasInvoices.AnyAsync(i =>
                i.ReadingDateFrom < to && from < i.ReadingDateTo);
            if (!overlaps)
            {
                return (from, to);
            }

            from = from.AddDays(7);
            to = to.AddDays(7);
        }

        throw new InvalidOperationException("Test için boş fatura dönemi bulunamadı.");
    }

    private static AddGasInvoiceCommand BuildInvoiceCommand(
        string invoiceNumber, DateTimeOffset from, DateTimeOffset to) => new(
        invoiceNumber, "10060828", "2160191", from, to,
        to.AddDays(5), 693.25m, 19126.000m, 1.22588m,
        600m, 108m, 400m, 72m, 1180m);

    [Fact]
    public async Task ManualReading_CreatesManualEntryReading_WithConsumption()
    {
        var (mediator, db) = Build();
        var (apartmentId, meterId) = await ImportRandomMeterAsync(mediator, db);

        var first = await mediator.Send(new AddManualMeterReadingCommand(
            apartmentId, meterId, new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero), 100m));
        var second = await mediator.Send(new AddManualMeterReadingCommand(
            apartmentId, meterId, new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero), 150m));

        Assert.True(first.IsInitialReading);
        Assert.Equal(0m, first.PeriodConsumptionKWh);
        Assert.Equal(50m, second.PeriodConsumptionKWh);

        var saved = await db.MeterReadings.FirstAsync(r => r.Id == second.ReadingId);
        Assert.Equal(ReadingSource.ManualEntry, saved.Source);
    }

    [Fact]
    public async Task ManualReading_OutOfPeriodDate_SavesWithWarning()
    {
        var (mediator, db) = Build();
        var (apartmentId, meterId) = await ImportRandomMeterAsync(mediator, db);

        var result = await mediator.Send(new AddManualMeterReadingCommand(
            apartmentId, meterId, new DateTimeOffset(2019, 1, 1, 10, 0, 0, TimeSpan.Zero), 10m));

        Assert.NotEmpty(result.Warnings);
        var reading = await db.MeterReadings.FirstAsync(r => r.Id == result.ReadingId);
        Assert.Equal(ReadingSource.ManualEntry, reading.Source);
    }

    [Fact]
    public async Task ManualReading_IndexRegression_Throws()
    {
        var (mediator, db) = Build();
        var (apartmentId, meterId) = await ImportRandomMeterAsync(mediator, db);

        await mediator.Send(new AddManualMeterReadingCommand(
            apartmentId, meterId, new DateTimeOffset(2026, 7, 1, 10, 0, 0, TimeSpan.Zero), 500m));

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            mediator.Send(new AddManualMeterReadingCommand(
                apartmentId, meterId, new DateTimeOffset(2026, 7, 10, 10, 0, 0, TimeSpan.Zero), 499m)));

        Assert.Contains(DomainConstants.ValidationMessages.ManualReadingIndexRegression, ex.Message);
    }

    [Fact]
    public async Task ManualReading_DuplicateDate_Throws()
    {
        var (mediator, db) = Build();
        var (apartmentId, meterId) = await ImportRandomMeterAsync(mediator, db);

        var date = new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);
        await mediator.Send(new AddManualMeterReadingCommand(apartmentId, meterId, date, 100m));

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            mediator.Send(new AddManualMeterReadingCommand(apartmentId, meterId, date, 120m)));

        Assert.Contains(DomainConstants.ValidationMessages.ManualReadingDuplicate, ex.Message);
    }

    [Fact]
    public async Task ImportReadingsExcel_CreatesImportSourceReadings_AndReportsUnmatched()
    {
        var (mediator, db) = Build();
        var (_, newMeterId) = await ImportRandomMeterAsync(mediator, db);
        var apartment = await db.Apartments.FirstAsync(a => a.Meters.Any(m => m.Id == newMeterId));
        var door = apartment.DoorNumber;

        var content = BuildExcel(
            ["Kapı No", "Okuma Tarihi", "Endeks (kWh)"],
            [[door, "05.02.2019", "1000,500"], ["9999", "06.02.2019", "2000,000"]]);

        var result = await mediator.Send(new ImportReadingsFromExcelCommand(content));

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.UnmatchedApartmentCount);
        Assert.Single(result.RowErrors);

        var reading = await db.MeterReadings
            .FirstAsync(r => r.MeterId == newMeterId && r.IndexValue == 1000.500m);
        Assert.Equal(ReadingSource.Import, reading.Source);
    }

    [Fact]
    public async Task ImportReadingsExcel_DotDecimalIndex_InitialReadingHasZeroConsumption()
    {
        // Canlı hata düzeltmesi: DB'de hiç okuma yoksa Excel'den gelen endeks (örn. "2929.888"
        // — Invariant noktalı ondalık) başlangıç okumasıdır → tüketim 0, IsInitialReading=true.
        // Aynı dosyadaki ikinci satır, ilk satıra göre fark ile tüketim alır.
        var (mediator, db) = Build();
        var (_, newMeterId) = await ImportRandomMeterAsync(mediator, db);
        var apartment = await db.Apartments.FirstAsync(a => a.Meters.Any(m => m.Id == newMeterId));
        var door = apartment.DoorNumber;

        var content = BuildExcel(
            ["Kapı No", "Okuma Tarihi", "Endeks (kWh)"],
            [
                [door, "2026-04-08 00:00:00", "2929.888"],
                [door, "2026-05-08 00:00:00", "3000,500"],
            ]);

        var result = await mediator.Send(new ImportReadingsFromExcelCommand(content));

        Assert.Equal(2, result.ImportedCount);
        Assert.Empty(result.RowErrors);

        var readings = await db.MeterReadings
            .Where(r => r.MeterId == newMeterId)
            .OrderBy(r => r.ReadingDateUtc)
            .ToListAsync();

        Assert.Equal(2, readings.Count);
        Assert.True(readings[0].IsInitialReading);
        Assert.Equal(0m, readings[0].ConsumptionKWh);
        Assert.Equal(2929.888m, readings[0].IndexValue);
        Assert.False(readings[1].IsInitialReading);
        Assert.Equal(70.612m, readings[1].ConsumptionKWh);
        Assert.Equal(ReadingSource.Import, readings[1].Source);
    }

    [Fact]
    public async Task ImportReadingsExcel_InvalidRow_DoesNotAbortRemainingRows()
    {
        // Kısmi başarı: hatalı/uygunsuz satır çökertmez; geçerli satırlar kaydedilir.
        var (mediator, db) = Build();
        var (_, newMeterId) = await ImportRandomMeterAsync(mediator, db);
        var apartment = await db.Apartments.FirstAsync(a => a.Meters.Any(m => m.Id == newMeterId));
        var door = apartment.DoorNumber;

        var content = BuildExcel(
            ["Kapı No", "Okuma Tarihi", "Endeks (kWh)"],
            [
                [door, "geçersiz-tarih", "100.000"],
                [door, "2026-04-08 00:00:00", "2929.888"],
            ]);

        var result = await mediator.Send(new ImportReadingsFromExcelCommand(content));

        Assert.Equal(1, result.ImportedCount);
        Assert.Single(result.RowErrors);

        var reading = await db.MeterReadings.FirstAsync(r => r.MeterId == newMeterId);
        Assert.True(reading.IsInitialReading);
        Assert.Equal(0m, reading.ConsumptionKWh);
    }

    [Fact]
    public void Parser_DotDecimalAndThousands_AreDisambiguated()
    {
        var content = BuildExcel(
            ["Kapı No", "Okuma Tarihi", "Endeks (kWh)"],
            [
                ["10", "2026-04-08 00:00:00", "2929.888"],  // tek nokta → ondalık (Invariant)
                ["11", "2026-04-08 00:00:00", "1.234.567"], // çoklu nokta → binlik ayraç
                ["12", "2026-04-08 00:00:00", "1000,500"],  // virgül → ondalık (TR)
                ["13", "2026-04-08 00:00:00", "1.234,567"], // ikisi birden → sondaki ondalık
            ]);

        using var stream = new MemoryStream(content);
        var parser = new Westalife.Infrastructure.Reporting.Excel.ExcelReadingImportParser();
        var result = parser.Parse(stream);

        Assert.Empty(result.Errors);
        Assert.Equal(4, result.Rows.Count);
        Assert.Equal(2929.888m, result.Rows[0].IndexValue);
        Assert.Equal(1234567m, result.Rows[1].IndexValue);
        Assert.Equal(1000.500m, result.Rows[2].IndexValue);
        Assert.Equal(1234.567m, result.Rows[3].IndexValue);
    }

    [Fact]
    public async Task Lock_UncalculatedInvoice_Throws()
    {
        var (mediator, db) = Build();
        var (from, to) = await RandomFreePeriodAsync(db);
        var invoiceId = await mediator.Send(BuildInvoiceCommand(
            $"E66UNC{Random.Shared.Next(100000, 999999)}", from, to));

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            mediator.Send(new LockPeriodCommand(invoiceId)));

        Assert.Contains(DomainConstants.GasInvoice.LockRequiresCalculation, ex.Message);
    }

    [Fact]
    public async Task Lock_CalculatedInvoice_BlocksManualReadingImportAndRecalculate_ThenUnlock()
    {
        var (mediator, db) = Build();
        var (apartmentId, meterId) = await ImportRandomMeterAsync(mediator, db);
        var (from, to) = await RandomFreePeriodAsync(db);

        // Dönem içi tüketim: başlangıç + dönem okuması (hesaplamada tüketim > 0 olsun).
        await mediator.Send(new AddManualMeterReadingCommand(apartmentId, meterId, from.AddHours(-24), 1000m));
        await mediator.Send(new AddManualMeterReadingCommand(apartmentId, meterId, from.AddHours(24), 1500m));

        var invoiceId = await mediator.Send(BuildInvoiceCommand(
            $"E66LOCK{Random.Shared.Next(100000, 999999)}", from, to));

        var calc = await mediator.Send(new CalculateCostDistributionCommand(invoiceId));
        Assert.True(calc.Reconciled);

        var lockResult = await mediator.Send(new LockPeriodCommand(invoiceId));
        Assert.True(lockResult.IsLocked);
        Assert.NotNull(lockResult.LockedOn);

        // 1) Kilitli döneme manuel okuma → reddedilir.
        var manualEx = await Assert.ThrowsAsync<DomainException>(() =>
            mediator.Send(new AddManualMeterReadingCommand(
                apartmentId, meterId, from.AddHours(48), 1800m)));
        Assert.Contains(DomainConstants.GasInvoice.PeriodLocked, manualEx.Message);

        // 2) Kilitli döneme Excel içe aktarma → satır hatası.
        var apartment = await db.Apartments.FirstAsync(a => a.Id == apartmentId);
        var excel = BuildExcel(
            ["Kapı No", "Okuma Tarihi", "Endeks (kWh)"],
            [[apartment.DoorNumber, from.AddHours(60).ToString("dd.MM.yyyy HH:mm"), "2000,000"]]);
        var importResult = await mediator.Send(new ImportReadingsFromExcelCommand(excel));
        Assert.Equal(0, importResult.ImportedCount);
        Assert.Contains(importResult.RowErrors, e => e.Contains(DomainConstants.GasInvoice.PeriodLocked));

        // 3) Kilitli dönem yeniden hesaplanamaz.
        var recalcEx = await Assert.ThrowsAsync<DomainException>(() =>
            mediator.Send(new CalculateCostDistributionCommand(invoiceId)));
        Assert.Contains(DomainConstants.GasInvoice.RecalculateBlocked, recalcEx.Message);

        // 4) Kilit açılır → manuel okuma yeniden kabul edilir.
        await mediator.Send(new UnlockPeriodCommand(invoiceId));
        var invoice = await db.GasInvoices.FirstAsync(i => i.Id == invoiceId);
        Assert.False(invoice.IsLocked);

        var afterUnlock = await mediator.Send(new AddManualMeterReadingCommand(
            apartmentId, meterId, from.AddHours(72), 1800m));
        Assert.Equal(300m, afterUnlock.PeriodConsumptionKWh);

        // Liste IsLocked alanını taşır.
        var list = await mediator.Send(new GetGasInvoicesQuery());
        Assert.Contains(list, i => i.Id == invoiceId && !i.IsLocked);
    }
}
