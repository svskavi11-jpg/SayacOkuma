using ClosedXML.Excel;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Westalife.Application;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Apartments.Commands.ImportMetersFromExcel;
using Westalife.Application.Features.Apartments.Commands.UpdateApartmentOwner;
using Westalife.Application.Features.Apartments.Queries.GetApartmentConsumptionAnalysis;
using Westalife.Application.Features.Apartments.Queries.GetApartmentOwnerHistory;
using Westalife.Application.Features.Apartments.Queries.GetApartments;
using Westalife.Application.Features.GasInvoices.Commands.AddGasInvoice;
using Westalife.Application.Features.GasInvoices.Commands.CalculateCostDistribution;
using Westalife.Application.Features.GasInvoices.Commands.LockPeriod;
using Westalife.Application.Features.GasInvoices.Commands.UpdateGasInvoice;
using Westalife.Application.Features.GasInvoices.Queries.GetGasInvoiceDetail;
using Westalife.Application.Features.Meters.Commands.ImportMeters;
using Westalife.Application.Features.MeterReadings.Commands.AddManualMeterReading;
using Westalife.Application.Features.MeterReadings.Commands.UpdateMeterReading;
using Westalife.Application.Features.MeterReadings.Queries.GetMeterReadingAuditLog;
using Westalife.Application.Features.MeterReadings.Queries.GetMeterReadings;
using Westalife.Application.Features.Settings.UpdateAppSettings;
using Westalife.Domain.Constants;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;
using Westalife.Infrastructure.Persistence;
using Xunit;

// Etap 6.7 notu: Test sınıfları ortak (kalıcı) PostgreSQL'i paylaştığından paralel çalışma
// kapalıdır — daire/sayaç seviyesinde çapraz test girişimini önler.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Westalife.Infrastructure.Tests;

/// <summary>
/// Etap 6.7: Fatura düzenleme, ayarlar validasyonu, kat maliki SCD Type 2,
/// tüketim analizi, okuma düzeltme + denetim izi entegrasyon testleri.
/// </summary>
public class Etap67Tests
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
        // (kilitli dönemler manuel okuma/analiz testlerini engeller).
        db.GasInvoices.Where(i => i.IsLocked).ExecuteUpdate(s => s
            .SetProperty(i => i.IsLocked, false)
            .SetProperty(i => i.LockedOn, (DateTimeOffset?)null));

        return (provider.GetRequiredService<IMediator>(), db);
    }

    private static byte[] BuildExcel(string[] headers, string[][] rows)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Sayac Listesi");
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

    private static async Task<(Guid ApartmentId, Guid MeterId)> ImportRandomMeterAsync(
        IMediator mediator, IApplicationDbContext db)
    {
        var random = new Random();
        var door = random.Next(1, 73);
        var serial = $"E67{random.Next(100000, 999999)}";
        var address = random.Next(1, 251);

        await mediator.Send(new ImportMetersFromIzarCenterCommand(
            [new ImportedMeterItem(door, serial, address)]));

        var apartment = await db.Apartments.FirstAsync(a => a.DoorNumber == door.ToString());
        var meter = await db.Meters.FirstAsync(m => m.SerialNumber == serial);
        return (apartment.Id, meter.Id);
    }

    // Deterministik (sabit) donem slotlari: rastgele/UtcNow'a bagli tarih yok. Slotlar Etab66Tests'ten (0-1) ayridir.
    private const int EditSlot = 2;
    private const int WarningSlot = 3;
    private const int LockedEditSlot = 4;
    private const int LockedReadingSlot = 5;

    private static Task<(DateTimeOffset From, DateTimeOffset To)> FixedPeriodAsync(
        IApplicationDbContext db, int slot) => TestPeriods.FixedFreePeriodAsync(db, slot);

    private static AddGasInvoiceCommand BuildInvoiceCommand(
        string invoiceNumber, DateTimeOffset from, DateTimeOffset to, string? note = null) => new(
        invoiceNumber, "10060828", "2160191", from, to,
        to.AddDays(5), 693.25m, 19126.000m, 1.22588m,
        600m, 108m, 400m, 72m, 1180m, note);

    // ---------- Madde 1: Fatura Düzenleme ----------

    [Fact]
    public async Task UpdateInvoice_EditsFields_TouchesLastModifiedOn_AndAddsNote()
    {
        var (mediator, db) = Build();
        var (from, to) = await FixedPeriodAsync(db, EditSlot);
        var invoiceId = await mediator.Send(BuildInvoiceCommand(
            $"E67A{Random.Shared.Next(100000, 999999)}", from, to));

        var before = await db.GasInvoices.FirstAsync(i => i.Id == invoiceId);
        var beforeModified = before.LastModifiedOn;
        await Task.Delay(50);

        var result = await mediator.Send(new UpdateGasInvoiceCommand(
            invoiceId, before.InvoiceNumber, "10060828", "2160191",
            from, to, to.AddDays(5),
            700m, 19126.000m, 1.22588m,
            600m, 108m, 400m, 72m, 1200m,
            "Bina ortak alan tüketimi %30 içinde eritilmiştir."));

        Assert.False(result.HasCalculationWarning);

        var after = await db.GasInvoices.FirstAsync(i => i.Id == invoiceId);
        Assert.Equal(1200m, after.TotalAmount);
        Assert.NotNull(after.Note);
        Assert.Contains("ortak alan", after.Note);
        Assert.NotNull(after.LastModifiedOn);
        Assert.NotEqual(beforeModified, after.LastModifiedOn);
    }

    [Fact]
    public async Task UpdateInvoice_WithCalculation_ReturnsWarning()
    {
        var (mediator, db) = Build();
        var (apartmentId, meterId) = await ImportRandomMeterAsync(mediator, db);
        var (from, to) = await FixedPeriodAsync(db, WarningSlot);

        await mediator.Send(new AddManualMeterReadingCommand(apartmentId, meterId, from.AddHours(-24), 1000m));
        await mediator.Send(new AddManualMeterReadingCommand(apartmentId, meterId, from.AddHours(24), 1500m));

        var invoiceId = await mediator.Send(BuildInvoiceCommand(
            $"E67B{Random.Shared.Next(100000, 999999)}", from, to));
        await mediator.Send(new CalculateCostDistributionCommand(invoiceId));

        var invoice = await db.GasInvoices.FirstAsync(i => i.Id == invoiceId);
        var result = await mediator.Send(new UpdateGasInvoiceCommand(
            invoiceId, invoice.InvoiceNumber, invoice.SubscriberNumber, invoice.MasterMeterSerial,
            from, to, invoice.IssuedOn, invoice.CorrectedVolumeM3, invoice.TotalEnergyKWh,
            invoice.PressureCorrectionFactor, invoice.ConsumptionAmount, invoice.ConsumptionVat,
            invoice.DistributionAmount, invoice.DistributionVat, invoice.TotalAmount, null));

        Assert.True(result.HasCalculationWarning);
    }

    [Fact]
    public async Task UpdateInvoice_Locked_Throws()
    {
        var (mediator, db) = Build();
        var (apartmentId, meterId) = await ImportRandomMeterAsync(mediator, db);
        var (from, to) = await FixedPeriodAsync(db, LockedEditSlot);

        await mediator.Send(new AddManualMeterReadingCommand(apartmentId, meterId, from.AddHours(-24), 1000m));
        await mediator.Send(new AddManualMeterReadingCommand(apartmentId, meterId, from.AddHours(24), 1500m));

        var invoiceId = await mediator.Send(BuildInvoiceCommand(
            $"E67C{Random.Shared.Next(100000, 999999)}", from, to));
        await mediator.Send(new CalculateCostDistributionCommand(invoiceId));
        await mediator.Send(new LockPeriodCommand(invoiceId));

        var invoice = await db.GasInvoices.FirstAsync(i => i.Id == invoiceId);
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            mediator.Send(new UpdateGasInvoiceCommand(
                invoiceId, invoice.InvoiceNumber, invoice.SubscriberNumber, invoice.MasterMeterSerial,
                from, to, invoice.IssuedOn, invoice.CorrectedVolumeM3, invoice.TotalEnergyKWh,
                invoice.PressureCorrectionFactor, invoice.ConsumptionAmount, invoice.ConsumptionVat,
                invoice.DistributionAmount, invoice.DistributionVat, invoice.TotalAmount, null)));

        Assert.Contains(DomainConstants.ValidationMessages.InvoiceLockedForEdit, ex.Message);

        var detail = await mediator.Send(new GetGasInvoiceDetailQuery(invoiceId));
        Assert.True(detail.IsLocked);
        Assert.True(detail.HasCalculation);
    }

    // ---------- Madde 2: Ayarlar Validasyon Bug Fix ----------

    [Fact]
    public async Task Settings_SimulatorConnection_SavesWithoutTcpUsbValidationErrors()
    {
        var (mediator, db) = Build();

        // Bug: Simulator seçiliyken TcpPort=0/UsbBaudRate=0 koşulsuz kurallara takılıyordu.
        var result = await mediator.Send(new UpdateAppSettingsCommand(
            "Çınar Teknik", "Westalife Residence", 0.30m, 0.70m,
            DomainConstants.AppSettings.ConnectionSimulator,
            null, 0, null, 0));

        Assert.Equal(DomainConstants.AppSettings.ConnectionSimulator, result.MeterConnectionType);

        var settings = await db.AppSettings.AsNoTracking().FirstAsync();
        Assert.Equal(DomainConstants.AppSettings.ConnectionSimulator, settings.MeterConnectionType);
    }

    // ---------- Madde 3 + 4: Kat Maliki (Excel import + SCD Type 2) ----------

    [Fact]
    public async Task ImportMetersExcel_WithOwnerAndSquareMeters_CreatesScdRecords()
    {
        var (mediator, db) = Build();
        var random = new Random();
        var door = random.Next(1, 73);
        var serial = $"E67D{random.Next(100000, 999999)}";

        // Tekrar-koşum dayanıklılığı: kapı eski koşulardan kalma m²/malik taşıyabilir;
        // beklenen değerleri MEVCUT aktif kayıtlara göre üret (no-op davranışı da doğrulanır).
        var existing = await db.Apartments
            .Include(a => a.Owners)
            .Include(a => a.Dimensions)
            .FirstAsync(a => a.DoorNumber == door.ToString());
        var currentM2 = existing.Dimensions.FirstOrDefault(d => d.ValidTo is null)?.SquareMeters ?? 0m;
        var targetM2 = currentM2 + 25m;
        var ownerName = $"Malik {serial}";
        var expectedOwnerSet = existing.Owners.FirstOrDefault(o => o.ValidTo is null)?.OwnerName == ownerName ? 0 : 1;
        var expectedDimensionUpdate = currentM2 == targetM2 ? 0 : 1;

        var content = BuildExcel(
            ["Kapı No", "Sayaç Seri No", "M-Bus Adresi", "Kat Maliki Adı Soyadı", "m²"],
            [[door.ToString(), serial, random.Next(1, 251).ToString(), ownerName,
              targetM2.ToString("0.000", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"))]]);

        var result = await mediator.Send(new ImportMetersFromExcelCommand(content));

        Assert.True(result.ImportedCount == 1,
            $"imported={result.ImportedCount}, skipped={result.SkippedExistingCount}, " +
            $"unmatched={result.UnmatchedApartmentCount}, owners={result.OwnersSetCount}, " +
            $"dims={result.DimensionsUpdatedCount}, errors=[{string.Join(" | ", result.RowErrors)}]");
        Assert.Equal(expectedOwnerSet, result.OwnersSetCount);
        Assert.True(result.DimensionsUpdatedCount == expectedDimensionUpdate,
            $"dims={result.DimensionsUpdatedCount}, expected={expectedDimensionUpdate}, " +
            $"currentM2={currentM2}, targetM2={targetM2}, excel='{targetM2.ToString("0.000", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"))}'");

        var apartment = await db.Apartments
            .Include(a => a.Owners)
            .Include(a => a.Dimensions)
            .FirstAsync(a => a.DoorNumber == door.ToString());

        var activeOwner = apartment.Owners.Single(o => o.ValidTo is null);
        Assert.Equal(ownerName, activeOwner.OwnerName);

        var activeDimension = apartment.Dimensions.Single(d => d.ValidTo is null);
        Assert.Equal(targetM2, activeDimension.SquareMeters);
    }

    [Fact]
    public async Task UpdateOwner_ClosesPreviousRecord_KeepsHistory()
    {
        var (mediator, db) = Build();
        var (apartmentId, _) = await ImportRandomMeterAsync(mediator, db);

        // Tekrar-koşum dayanıklılığı: benzersiz malik adları; geçmiş sayımı YALNIZ bu isimlere göre.
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var firstOwner = $"Malik A {suffix}";
        var secondOwner = $"Malik B {suffix}";

        var first = await mediator.Send(new UpdateApartmentOwnerCommand(apartmentId, firstOwner));
        Assert.True(first.Changed);
        await Task.Delay(20);

        var second = await mediator.Send(new UpdateApartmentOwnerCommand(apartmentId, secondOwner));
        Assert.True(second.Changed);

        var apartment = await db.Apartments
            .Include(a => a.Owners)
            .FirstAsync(a => a.Id == apartmentId);

        // SCD Type 2: eski kayıt kapatıldı, geçmişte kalıyor; aktif kayıt yeni malik.
        var closed = apartment.Owners.Single(o => o.OwnerName == firstOwner);
        Assert.NotNull(closed.ValidTo);

        var active = apartment.Owners.Single(o => o.ValidTo is null);
        Assert.Equal(secondOwner, active.OwnerName);

        // Geçmiş sorgusu her iki dönemi döner (kapalı + aktif).
        var history = await mediator.Send(new GetApartmentOwnerHistoryQuery(apartmentId));
        var mine = history.Where(h => h.OwnerName == firstOwner || h.OwnerName == secondOwner).ToList();
        Assert.Equal(2, mine.Count);
        Assert.Contains(mine, h => h.OwnerName == firstOwner && h.ValidTo is not null);
        Assert.Contains(mine, h => h.OwnerName == secondOwner && h.ValidTo is null);

        // No-op: aynı ad tekrar gönderilirse yeni kayıt AÇILMAZ.
        var noOp = await mediator.Send(new UpdateApartmentOwnerCommand(apartmentId, secondOwner));
        Assert.False(noOp.Changed);
        var historyAfterNoOp = await mediator.Send(new GetApartmentOwnerHistoryQuery(apartmentId));
        Assert.Equal(2, historyAfterNoOp.Count(h => h.OwnerName == firstOwner || h.OwnerName == secondOwner));
    }

    [Fact]
    public async Task GetApartments_ReturnsOwnerAndLastReading()
    {
        var (mediator, db) = Build();
        var (apartmentId, meterId) = await ImportRandomMeterAsync(mediator, db);
        await mediator.Send(new UpdateApartmentOwnerCommand(apartmentId, "Mehmet Kaya"));
        await mediator.Send(new AddManualMeterReadingCommand(
            apartmentId, meterId, new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero), 500m));

        var list = await mediator.Send(new GetApartmentsQuery());
        var item = list.Single(a => a.Id == apartmentId);

        Assert.Equal("Mehmet Kaya", item.OwnerName);
        Assert.Equal(meterId, item.ActiveMeterId);
        Assert.Equal(500m, item.LastIndexValue);
        Assert.NotNull(item.LastReadingDateUtc);

        // "Okuma Yok" tutarlılığı: endeksi olmayan dairenin okuma tarihi de null olmalı.
        // (Kalıcı test DB'sinde tüm daireler tarihsel koşulardan okuma/malik taşıyabildiğinden
        // mutlak "en az bir tane" şartı koşulmaz; varsa tutarlılık doğrulanır.)
        var withoutReading = list.FirstOrDefault(a => a.LastIndexValue is null);
        if (withoutReading is not null)
        {
            Assert.Null(withoutReading.LastReadingDateUtc);
        }
    }

    // ---------- Madde 7: Tüketim Analizi ----------

    [Fact]
    public async Task ConsumptionAnalysis_MonthlyBuckets_AreCorrect()
    {
        var (mediator, db) = Build();
        var (apartmentId, meterId) = await ImportRandomMeterAsync(mediator, db);

        await mediator.Send(new AddManualMeterReadingCommand(
            apartmentId, meterId, new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero), 100m));
        await mediator.Send(new AddManualMeterReadingCommand(
            apartmentId, meterId, new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero), 150m));
        await mediator.Send(new AddManualMeterReadingCommand(
            apartmentId, meterId, new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero), 210m));

        // DB gerçeği: aylık kovaların, okuma kayıtlarındaki tüketim toplamlarıyla birebir
        // eşleşmesi doğrulanır (haziran: 150-100=50; temmuz: 210-150=60; toplam 110).
        var yearFrom = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var yearTo = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var dbReadings = await db.MeterReadings.AsNoTracking()
            .Where(r => r.MeterId == meterId && r.ReadingDateUtc >= yearFrom && r.ReadingDateUtc < yearTo)
            .ToListAsync();

        var analysis = await mediator.Send(
            new GetApartmentConsumptionAnalysisQuery(apartmentId, 2026, null, null));

        Assert.Equal(2026, analysis.Year);
        Assert.Equal(12, analysis.Monthly.Count); // yıl görünümü: 12 ay
        Assert.Equal(3, analysis.ReadingCount);
        Assert.Equal(110m, analysis.TotalConsumptionKWh); // 50 (haz) + 60 (tem)
        Assert.Equal(50m, analysis.Monthly.Single(m => m.Month == 6).ConsumptionKWh);
        Assert.Equal(60m, analysis.Monthly.Single(m => m.Month == 7).ConsumptionKWh);
        Assert.Equal(dbReadings.Sum(r => r.ConsumptionKWh), analysis.TotalConsumptionKWh);

        // Tarih aralığı görünümü (Temmuz 2026): yalnız r3 → tüketim 60.
        var range = await mediator.Send(new GetApartmentConsumptionAnalysisQuery(
            apartmentId, null,
            new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero)));
        Assert.Null(range.Year);
        Assert.Single(range.Monthly);
        Assert.Equal(60m, range.TotalConsumptionKWh);
        Assert.Equal(60m, range.Monthly[0].ConsumptionKWh);
    }

    // ---------- Madde 8: Okuma Düzeltme + Denetim İzi ----------

    [Fact]
    public async Task UpdateReading_Single_RecalculatesConsumptions_AndWritesAuditLog()
    {
        var (mediator, db) = Build();
        var (apartmentId, meterId) = await ImportRandomMeterAsync(mediator, db);

        // 100 → 150 → 200 zinciri; orta okuma 150 → 160 düzeltilir.
        var r1 = await mediator.Send(new AddManualMeterReadingCommand(
            apartmentId, meterId, new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero), 100m));
        var r2 = await mediator.Send(new AddManualMeterReadingCommand(
            apartmentId, meterId, new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero), 150m));
        var r3 = await mediator.Send(new AddManualMeterReadingCommand(
            apartmentId, meterId, new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.Zero), 200m));

        var result = await mediator.Send(new UpdateMeterReadingCommand(
        [
            new MeterReadingCorrectionItem(r2.ReadingId, 160m, "Excel aktarımında yanlış girildi", "Operatör 1"),
        ]));

        Assert.Equal(1, result.UpdatedCount);
        Assert.Empty(result.RowErrors);

        var corrected = await db.MeterReadings.FirstAsync(x => x.Id == r2.ReadingId);
        Assert.Equal(160m, corrected.IndexValue);
        Assert.Equal(60m, corrected.ConsumptionKWh);       // 160 - 100

        // Zincirleme güncelleme: SONRAKİ okumanın tüketimi de yeniden hesaplandı.
        var next = await db.MeterReadings.FirstAsync(x => x.Id == r3.ReadingId);
        Assert.Equal(40m, next.ConsumptionKWh);            // 200 - 160

        // Denetim izi: eski değer, yeni değer, neden, kullanıcı.
        var log = await mediator.Send(new GetMeterReadingAuditLogQuery(r2.ReadingId));
        Assert.Single(log);
        Assert.Equal(150m, log[0].OldIndexValue);
        Assert.Equal(160m, log[0].NewIndexValue);
        Assert.Equal(50m, log[0].OldConsumptionKWh);
        Assert.Equal(60m, log[0].NewConsumptionKWh);
        Assert.Equal("Excel aktarımında yanlış girildi", log[0].Reason);
        Assert.Equal("Operatör 1", log[0].ChangedBy);

        // İlk okuma etiketi korunur: r1 hâlâ initial (düzeltmede dokunulmadı).
        var first = await db.MeterReadings.FirstAsync(x => x.Id == r1.ReadingId);
        Assert.True(first.IsInitialReading);

        // Okuma listesi: audit sayısı ve kilit durumu taşınır.
        var readings = await mediator.Send(new GetMeterReadingsQuery(meterId));
        var correctedItem = readings.Single(x => x.Id == r2.ReadingId);
        Assert.Equal(1, correctedItem.AuditLogCount);
        Assert.False(correctedItem.IsLockedPeriod);
    }

    [Fact]
    public async Task UpdateReading_CorrectedToInitial_MarksInitialAndZeroConsumption()
    {
        var (mediator, db) = Build();
        var (apartmentId, meterId) = await ImportRandomMeterAsync(mediator, db);

        var r1 = await mediator.Send(new AddManualMeterReadingCommand(
            apartmentId, meterId, new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero), 100m));
        var r2 = await mediator.Send(new AddManualMeterReadingCommand(
            apartmentId, meterId, new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero), 150m));

        // İlk okuma 100 → 90 düzeltilir: tüketimi 0 kalır (initial), sonraki okuma 150-90=60 olur.
        var result = await mediator.Send(new UpdateMeterReadingCommand(
        [
            new MeterReadingCorrectionItem(r1.ReadingId, 90m, "Saha okuması düzeltildi", null),
        ]));

        Assert.Equal(1, result.UpdatedCount);

        var first = await db.MeterReadings.FirstAsync(x => x.Id == r1.ReadingId);
        Assert.Equal(90m, first.IndexValue);
        Assert.Equal(0m, first.ConsumptionKWh);
        Assert.True(first.IsInitialReading);

        var second = await db.MeterReadings.FirstAsync(x => x.Id == r2.ReadingId);
        Assert.Equal(60m, second.ConsumptionKWh);

        // ChangedBy boş → varsayılan "Yönetici".
        var log = await mediator.Send(new GetMeterReadingAuditLogQuery(r1.ReadingId));
        Assert.Equal(DomainConstants.MeterReadingAuditLog.DefaultChangedBy, log[0].ChangedBy);
    }

    [Fact]
    public async Task UpdateReading_Batch_PartialSuccess()
    {
        var (mediator, db) = Build();
        var (apartmentId, meterId) = await ImportRandomMeterAsync(mediator, db);

        var r1 = await mediator.Send(new AddManualMeterReadingCommand(
            apartmentId, meterId, new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero), 100m));
        var r2 = await mediator.Send(new AddManualMeterReadingCommand(
            apartmentId, meterId, new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero), 150m));

        // Toplu düzeltme: r2 → 99 (önceki 100'den küçük) satır hatası; r1 → 110 geçerli.
        var result = await mediator.Send(new UpdateMeterReadingCommand(
        [
            new MeterReadingCorrectionItem(r2.ReadingId, 99m, "test gerileme", "Ops"),
            new MeterReadingCorrectionItem(r1.ReadingId, 110m, "test gecerli", "Ops"),
        ]));

        Assert.Equal(1, result.UpdatedCount);
        Assert.Single(result.RowErrors);
        Assert.Contains(DomainConstants.ValidationMessages.ReadingCorrectionIndexOutsideChain, result.RowErrors[0]);

        var first = await db.MeterReadings.FirstAsync(x => x.Id == r1.ReadingId);
        Assert.Equal(110m, first.IndexValue);
        Assert.Equal(0m, first.ConsumptionKWh);       // ilk okuma tüketim DEĞİLDİR (initial)
        Assert.True(first.IsInitialReading);

        // Zincirleme: r2'nin tüketimi yeni endekse göre güncellendi (150 - 110 = 40).
        var second = await db.MeterReadings.FirstAsync(x => x.Id == r2.ReadingId);
        Assert.Equal(40m, second.ConsumptionKWh);
    }

    [Fact]
    public async Task UpdateReading_LockedPeriod_Rejected()
    {
        var (mediator, db) = Build();
        var (apartmentId, meterId) = await ImportRandomMeterAsync(mediator, db);
        var (from, to) = await FixedPeriodAsync(db, LockedReadingSlot);

        var r1 = await mediator.Send(new AddManualMeterReadingCommand(
            apartmentId, meterId, from.AddHours(-24), 1000m));
        var r2 = await mediator.Send(new AddManualMeterReadingCommand(
            apartmentId, meterId, from.AddHours(24), 1500m));

        var invoiceId = await mediator.Send(BuildInvoiceCommand(
            $"E67L{Random.Shared.Next(100000, 999999)}", from, to));
        await mediator.Send(new CalculateCostDistributionCommand(invoiceId));
        await mediator.Send(new LockPeriodCommand(invoiceId));

        // Kilitli döneme düşen okuma düzeltilemez.
        var result = await mediator.Send(new UpdateMeterReadingCommand(
        [
            new MeterReadingCorrectionItem(r2.ReadingId, 1600m, "test kilit", "Ops"),
        ]));

        Assert.Equal(0, result.UpdatedCount);
        Assert.Single(result.RowErrors);
        Assert.Contains(DomainConstants.GasInvoice.PeriodLocked, result.RowErrors[0]);

        // Liste kilit durumunu bildirir.
        var readings = await mediator.Send(new GetMeterReadingsQuery(meterId));
        Assert.Contains(readings, x => x.Id == r2.ReadingId && x.IsLockedPeriod);

        // Temizlik: kilidi aç (diğer testlerin 2026 dönemine kalan kilit çakışmasını önler).
        await mediator.Send(new UnlockPeriodCommand(invoiceId));
    }

    [Fact]
    public async Task UpdateReading_EmptyReason_FailsValidation()
    {
        var (mediator, db) = Build();
        var (apartmentId, meterId) = await ImportRandomMeterAsync(mediator, db);

        var r1 = await mediator.Send(new AddManualMeterReadingCommand(
            apartmentId, meterId, new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero), 100m));

        // Neden zorunlu (denetim izi) — ValidationBehavior ValidationException fırlatır.
        await Assert.ThrowsAsync<ValidationException>(() =>
            mediator.Send(new UpdateMeterReadingCommand(
            [
                new MeterReadingCorrectionItem(r1.ReadingId, 120m, "", null),
            ])));
    }
}