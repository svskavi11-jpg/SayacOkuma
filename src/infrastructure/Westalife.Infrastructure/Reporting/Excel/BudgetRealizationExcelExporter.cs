using ClosedXML.Excel;
using Westalife.Application.Features.Budget.Queries.GetBudgetRealization;
using Westalife.Domain.Constants;

namespace Westalife.Infrastructure.Reporting.Excel;

/// <summary>Bütçe gerçekleşme Excel çıktısı. Kalan = tahmini − gerçekleşen.</summary>
public static class BudgetRealizationExcelExporter
{
    public static byte[] Generate(BudgetRealizationReport data)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Bütçe Gerçekleşme");

        ws.Cell(1, 1).Value = data.BuildingName;
        ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        ws.Cell(2, 1).Value = $"{data.Year} Bütçe Gerçekleşme Raporu";
        ws.Cell(2, 1).Style.Font.SetBold();

        string[] headers = ["Kategori", "Tahmini", "Gerçekleşen", "Kalan", "Yüzde"];
        const int headerRow = 4;
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(headerRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.SetBold();
            cell.Style.Fill.SetBackgroundColor(XLColor.LightGray);
        }

        var row = headerRow + 1;
        foreach (var item in data.Rows)
        {
            ws.Cell(row, 1).Value = item.Category;
            Money(ws.Cell(row, 2), item.EstimatedAmount);
            Money(ws.Cell(row, 3), item.ActualAmount);
            Money(ws.Cell(row, 4), item.RemainingAmount);
            Percent(ws.Cell(row, 5), item.UtilizationPercent);
            row++;
        }

        ws.Cell(row, 1).Value = "TOPLAM";
        ws.Cell(row, 1).Style.Font.SetBold();
        Money(ws.Cell(row, 2), data.TotalEstimated);
        Money(ws.Cell(row, 3), data.TotalActual);
        Money(ws.Cell(row, 4), data.TotalRemaining);
        Percent(ws.Cell(row, 5), data.UtilizationPercent);
        ws.Range(row, 2, row, 5).Style.Font.SetBold();

        row += 2;
        ws.Cell(row, 1).Value = DomainConstants.Budget.EstimatedIncomeLabel;
        Money(ws.Cell(row, 2), data.TotalEstimatedIncome);
        row++;
        ws.Cell(row, 1).Value = DomainConstants.Budget.EstimatedExpenseLabel;
        Money(ws.Cell(row, 2), data.TotalEstimated);
        row++;
        ws.Cell(row, 1).Value = DomainConstants.Budget.BalanceLabel;
        Money(ws.Cell(row, 2), data.Balance);
        ws.Range(row, 1, row, 2).Style.Font.SetBold();

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void Money(IXLCell cell, decimal value)
    {
        cell.Value = value;
        cell.Style.NumberFormat.SetFormat("#,##0.00");
    }

    private static void Percent(IXLCell cell, decimal value)
    {
        cell.Value = value;
        cell.Style.NumberFormat.SetFormat("0.00");
    }
}
