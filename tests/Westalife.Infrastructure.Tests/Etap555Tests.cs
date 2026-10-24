using ClosedXML.Excel;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Westalife.Application;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Apartments.Commands.ImportMetersFromExcel;
using Westalife.Application.Features.Settings.GetAppSettings;
using Westalife.Application.Features.Settings.UpdateAppSettings;
using Westalife.Domain.Constants;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;
using Westalife.Infrastructure.Persistence;
using Westalife.Infrastructure.Reporting.Excel;
using Xunit;

namespace Westalife.Infrastructure.Tests;

/// <summary>Etap 5.5.5: Excel parser, başlangıç endeksi mantığı ve Ayarlar (mirror) testleri.</summary>
public class Etap555Tests
{
    private static (IMediator Mediator, IApplicationDbContext Db) Build()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(DesignTimeDbContextFactory.DesignTimeConnectionString);
        services.AddApplication();
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IMediator>(), provider.GetRequiredService<IApplicationDbContext>());
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

    [Fact]
    public void ExcelParser_WithTurkishHeaders_ParsesAllColumns()
    {
        var parser = new ExcelMeterImportParser();
        var content = BuildExcel(
            ["Kapı No", "Sayaç Seri No", "M-Bus Adresi", "Başlangıç Endeksi", "Başlangıç Tarihi"],
            [["1", "TEST0001", "58", "19126,000", "30.11.2021"]]);

        var result = parser.Parse(new MemoryStream(content));

        Assert.Empty(result.Errors);
        var row = Assert.Single(result.Rows);
        Assert.Equal(1, row.DoorNumber);
        Assert.Equal("TEST0001", row.SerialNumber);
        Assert.Equal(58, row.PrimaryAddress);
        Assert.Equal(19126.000m, row.InitialIndexValue);
        Assert.NotNull(row.InitialReadingDate);
        Assert.Equal(11, row.InitialReadingDate!.Value.Month);
    }

    [Fact]
    public void ExcelParser_WithHeaderVariants_MapsDynamically()
    {
        // Başlık varyasyonu: "Kapi No" / "seri no" / "adres" — normalize edilerek eşlenir.
        var parser = new ExcelMeterImportParser();
        var content = BuildExcel(
            ["Kapi No", "seri no", "adres"],
            [["5", "TEST0002", "12"]]);

        var result = parser.Parse(new MemoryStream(content));

        Assert.Empty(result.Errors);
        Assert.Equal(5, Assert.Single(result.Rows).DoorNumber);
    }

    [Fact]
    public void ExcelParser_WithMissingRequiredHeaders_ReportsError()
    {
        var parser = new ExcelMeterImportParser();
        var content = BuildExcel(["Sütun A", "Sütun B"], [["x", "y"]]);

        var result = parser.Parse(new MemoryStream(content));

        Assert.Empty(result.Rows);
        Assert.Contains(result.Errors, e => e.Contains("Zorunlu başlıklar bulunamadı"));
    }

    [Fact]
    public void ExcelParser_WithInvalidRow_ReportsRowErrorButKeepsValidRow()
    {
        var parser = new ExcelMeterImportParser();
        var content = BuildExcel(
            ["Kapı No", "Sayaç Seri No", "M-Bus Adresi"],
            [["1", "TEST0003", "58"], ["abc", "TEST0004", "59"]]);

        var result = parser.Parse(new MemoryStream(content));

        Assert.Single(result.Rows); // geçerli satır korunur
        Assert.Single(result.Errors); // hatalı satır raporlanır
        Assert.Contains("Satır 3", result.Errors[0]);
    }

    [Fact]
    public async Task ImportExcel_WithInitialIndex_CreatesInitialReading()
    {
        var (mediator, db) = Build();

        // Rastgele bir daireye Excel üzerinden sayaç + başlangıç endeksi içe aktar.
        var random = new Random();
        var door = random.Next(1, 73);
        var serial = $"EXCEL{random.Next(100000, 999999)}";

        var content = BuildExcel(
            ["Kapı No", "Sayaç Seri No", "M-Bus Adresi", "Başlangıç Endeksi", "Başlangıç Tarihi"],
            [[door.ToString(), serial, "58", "19126,000", "30.11.2021"]]);

        var result = await mediator.Send(new ImportMetersFromExcelCommand(content));

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.InitialReadingsCreated);
        Assert.Empty(result.RowErrors);

        var meter = await db.Meters.FirstAsync(m => m.SerialNumber == serial);
        var reading = await db.MeterReadings.FirstAsync(r => r.MeterId == meter.Id);

        // Kural (Özellik 3): başlangıç okuması tüketim DEĞİLDİR.
        Assert.True(reading.IsInitialReading);
        Assert.Equal(0m, reading.ConsumptionKWh);
        Assert.Equal(19126.000m, reading.IndexValue);
        Assert.Equal(ReadingSource.Import, reading.Source);
    }

    [Fact]
    public async Task UpdateSettings_ValidRatios_MirrorsBuilding()
    {
        var (mediator, db) = Build();

        var response = await mediator.Send(new UpdateAppSettingsCommand(
            "Çınar Teknik", "Westalife Residence", 0.35m, 0.65m,
            DomainConstants.AppSettings.ConnectionSimulator, null, 2000, null, 2400));

        Assert.Equal(0.35m, response.FixedCostRatio);
        Assert.Equal(0.65m, response.ConsumptionRatio);

        // Mirror doğrulaması: Building tablosu da aynı orana güncellenmiş olmalı.
        var building = await db.Buildings.FirstAsync(b => b.IsActive);
        Assert.Equal(0.35m, building.FixedCostRatio);
    }

    [Fact]
    public async Task UpdateSettings_RatiosNotSummingToOne_Throws()
    {
        var (mediator, _) = Build();

        // FluentValidation pipeline ilk savunma hattıdır — ValidationException fırlatır.
        var ex = await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            mediator.Send(new UpdateAppSettingsCommand("Çınar Teknik", "Westalife Residence", 0.40m, 0.65m,
                DomainConstants.AppSettings.ConnectionSimulator, null, 2000, null, 2400)));

        Assert.Contains(DomainConstants.AppSettings.RatiosMustSumToOne, ex.Message);
    }
}
