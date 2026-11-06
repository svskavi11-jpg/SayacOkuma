using ClosedXML.Excel;
using Westalife.Application.Features.Budget.Queries.GetBudgetRealization;
using Westalife.Application.Features.Budget.Queries.GetIncomeExpenseReport;
using Westalife.Domain.Constants;
using Westalife.Infrastructure.Reporting.Excel;
using Westalife.Infrastructure.Reporting.Pdf;

namespace Westalife.Infrastructure.Tests;

/// <summary>Faz 6.10: Gelir-gider ve bütçe gerçekleşme PDF/Excel çıktıları.</summary>
public class BudgetReportingTests
{
    private static IncomeExpenseReport IncomeExpenseSample() =>
        new(
            2026,
            "Westalife Residence",
            1500.50m,
            900m,
            600.50m,
            Enumerable.Range(1, 12).Select(m => new IncomeExpenseMonthRow(m, m == 3 ? 1500.50m : 0m, m == 3 ? 900m : 0m, m == 3 ? 600.50m : 0m)).ToList(),
            [
                new IncomeExpenseLine(new DateOnly(2026, 3, 2), DomainConstants.Budget.IncomeLabel, "Tahsilat — Daire 12", "Aidat", 1500.50m),
                new IncomeExpenseLine(new DateOnly(2026, 3, 15), DomainConstants.Budget.ExpenseLabel, "Asansör", "Bakım", 900m),
            ],
            2000m,
            10000m,
            -8000m);

    private static BudgetRealizationReport RealizationSample() =>
        new(
            2026,
            "Westalife Residence",
            10_000m,
            9_100m,
            900m,
            91m,
            [
                new BudgetRealizationRow(Guid.NewGuid(), 2026, "Asansör", 10_000m, 9_100m, 900m, 91m, 0m, 0m),
            ],
            2_000m,
            -8_000m,
            false);

    [Fact]
    public void IncomeExpensePdf_StartsWithPdfHeader()
    {
        var pdf = IncomeExpensePdfDocument.Generate(IncomeExpenseSample());
        Assert.True(pdf.Length > 500);
        Assert.Equal("%PDF", System.Text.Encoding.Latin1.GetString(pdf, 0, 4));
    }

    [Fact]
    public void RealizationPdf_StartsWithPdfHeader()
    {
        var pdf = BudgetRealizationPdfDocument.Generate(RealizationSample());
        Assert.True(pdf.Length > 500);
        Assert.Equal("%PDF", System.Text.Encoding.Latin1.GetString(pdf, 0, 4));
    }

    [Fact]
    public void IncomeExpenseExcel_ContainsBuildingAndNet()
    {
        var bytes = IncomeExpenseExcelExporter.Generate(IncomeExpenseSample());
        Assert.Equal("PK", System.Text.Encoding.Latin1.GetString(bytes, 0, 2));

        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet(1);
        Assert.Equal("Westalife Residence", ws.Cell(1, 1).GetString());
        Assert.Equal(600.50m, ws.Cell(6, 2).GetValue<decimal>());
        Assert.Contains("Asansör", ws.CellsUsed().Select(c => c.GetString()));
    }

    [Fact]
    public void RealizationExcel_ContainsRemainingAsEstimatedMinusActual()
    {
        var bytes = BudgetRealizationExcelExporter.Generate(RealizationSample());
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        var ws = workbook.Worksheet(1);

        Assert.Equal("Asansör", ws.Cell(5, 1).GetString());
        Assert.Equal(10_000m, ws.Cell(5, 2).GetValue<decimal>());
        Assert.Equal(9_100m, ws.Cell(5, 3).GetValue<decimal>());
        Assert.Equal(900m, ws.Cell(5, 4).GetValue<decimal>());
        Assert.Equal(900m, ws.Cell(6, 4).GetValue<decimal>());
    }
}
