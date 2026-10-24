using System.Globalization;
using ClosedXML.Excel;
using Westalife.Application.Common.Models;
using Westalife.Application.Features.GasInvoices.Queries.GetNoticeReport;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;
using Westalife.Infrastructure.Reporting.Excel;
using Westalife.Infrastructure.Reporting.Pdf;
using Xunit;

namespace Westalife.Infrastructure.Tests;

/// <summary>
/// Raporlama motoru testleri: PDF geçerliliği, Türkçe karakterler, 72 sayfa toplu üretim,
/// Kural #1 mutabakat invariant'ı (Σ satır != fatura → PDF ÜRETİLMEZ) ve Excel içeriği.
/// </summary>
public class ReportingTests
{
    private static NoticeRow Row(int door, decimal fixedAmount, decimal consumptionAmount, decimal kwh = 500m) =>
        new(Guid.NewGuid(), door.ToString(), $"Daire {door}", $"552{door:D5}", 100m, kwh,
            1000m, 1000m + kwh, DateTimeOffset.UtcNow, fixedAmount, consumptionAmount,
            fixedAmount + consumptionAmount, false);

    private static NoticeReportData BuildData(IReadOnlyList<NoticeRow> rows, decimal total)
    {
        var totalSqm = rows.Sum(r => r.SquareMeters);
        var totalKwh = rows.Sum(r => r.ConsumptionKWh);
        var fixedPool = rows.Sum(r => r.FixedAmount);
        var consumptionPool = rows.Sum(r => r.ConsumptionAmount);

        return new NoticeReportData(
            "Çınar Teknik",
            "Westalife Residence",
            "Zonguldak, Mithat Paşa Mh.",
            "Ocak 2031",
            new DateTimeOffset(2031, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2031, 2, 1, 0, 0, 0, TimeSpan.Zero),
            31,
            "10060828",
            "2160191",
            new DateTimeOffset(2031, 2, 1, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2031, 2, 16, 12, 0, 0, TimeSpan.Zero),
            total,
            totalSqm,
            totalKwh,
            totalSqm == 0m ? 0m : Math.Round(fixedPool / totalSqm, 6),
            totalKwh == 0m ? 0m : Math.Round(consumptionPool / totalKwh, 6),
            0m,
            null,
            rows);
    }

    private static ReportingOptions Options() => new()
    {
        LogoLeftFallbackText = "Çınar Teknik",
        LogoRightFallbackText = "Danfoss"
    };

    [Fact]
    public void GenerateSingle_ProducesValidPdfWithPdfHeader()
    {
        var rows = new List<NoticeRow> { Row(1, 100m, 1000m), Row(2, 200m, 2000m), Row(3, 300m, 1945.20m) };
        var data = BuildData(rows, 5545.20m);

        var pdf = IndividualNoticePdfDocument.GenerateSingle(data, Options(), rows[0].ApartmentId);

        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.Latin1.GetString(pdf, 0, 4));
    }

    [Fact]
    public void GenerateAll_SeventyTwoRows_ProducesPageForEachApartment()
    {
        // 72 daire: sabit toplam 720, tüketim toplam 4825,20 → Σ = 5545,20 ✔
        var rows = new List<NoticeRow>();
        decimal fixedSum = 0, consumptionSum = 0;
        for (var i = 1; i <= 71; i++)
        {
            rows.Add(Row(i, 10m, 67m));
            fixedSum += 10m;
            consumptionSum += 67m;
        }

        rows.Add(Row(72, 720m - fixedSum, 4825.20m - consumptionSum));

        var data = BuildData(rows, 5545.20m);

        var pdf = IndividualNoticePdfDocument.GenerateAll(data, Options());

        Assert.True(pdf.Length > 10_000);
        var content = System.Text.Encoding.Latin1.GetString(pdf);
        var pageCount = System.Text.RegularExpressions.Regex.Matches(content, @"/Type\s*/Page[^s]").Count;
        Assert.Equal(72, pageCount);
    }

    [Fact]
    public void Generate_WithTurkishCharacters_SucceedsWithEmbeddedFont()
    {
        var rows = new List<NoticeRow>
        {
            new(Guid.NewGuid(), "1", "DAİRE ĞÜŞİ ÖÇ", "55200001", 100m, 1000m,
                1000m, 2000m, DateTimeOffset.UtcNow, 554.52m, 4990.68m, 5545.20m, false)
        };

        var data = BuildData(rows, 5545.20m);

        var pdf = IndividualNoticePdfDocument.GenerateSingle(data, Options(), rows[0].ApartmentId);

        Assert.True(pdf.Length > 1000);
        Assert.Equal("%PDF", System.Text.Encoding.Latin1.GetString(pdf, 0, 4));
    }

    [Fact]
    public void Generate_WhenRowsDoNotSumToInvoice_ThrowsInvariantException()
    {
        // Kural #1: Σ satır (5.000,00) != fatura (5.545,20) → PDF ÜRETİLMEZ.
        var rows = new List<NoticeRow>
        {
            new(Guid.NewGuid(), "1", "Daire 1", "55200001", 100m, 1000m,
                1000m, 2000m, DateTimeOffset.UtcNow, 100m, 4900m, 5000m, false)
        };

        var data = BuildData(rows, 5545.20m);

        var ex = Assert.Throws<DomainException>(() =>
            IndividualNoticePdfDocument.GenerateAll(data, Options()));

        Assert.Contains("Dağıtılan", ex.Message);
    }

    [Fact]
    public void Excel_ContainsAllRowsAndReconciliationTotal()
    {
        var rows = new List<NoticeRow> { Row(1, 100m, 1000m), Row(2, 200m, 2000m), Row(3, 300m, 1945.20m) };
        var data = BuildData(rows, 5545.20m);

        var bytes = DebtListExcelExporter.Generate(data);

        Assert.True(bytes.Length > 1000);
        Assert.Equal("PK", System.Text.Encoding.Latin1.GetString(bytes, 0, 2)); // xlsx = zip

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheet(1);

        Assert.Equal("Westalife Residence", ws.Cell(1, 1).GetString());
        Assert.Equal("Kapi No", ws.Cell(5, 1).GetString());
        Assert.Equal("1", ws.Cell(6, 1).GetString());
        Assert.Equal("3", ws.Cell(8, 1).GetString());

        var totalRow = ws.RowsUsed().Last();
        Assert.Equal(5545.20m, totalRow.Cell(8).GetValue<decimal>());
        Assert.Contains("MUTABIK", totalRow.Cell(9).GetString());
    }
}
