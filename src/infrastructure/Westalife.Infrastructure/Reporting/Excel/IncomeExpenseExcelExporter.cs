using System.Globalization;
using ClosedXML.Excel;
using Westalife.Application.Features.Budget.Queries.GetIncomeExpenseReport;
using Westalife.Domain.Constants;

namespace Westalife.Infrastructure.Reporting.Excel;

/// <summary>Yıllık gelir-gider Excel çıktısı.</summary>
public static class IncomeExpenseExcelExporter
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static byte[] Generate(IncomeExpenseReport data)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Gelir Gider");

        ws.Cell(1, 1).Value = data.BuildingName;
        ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);

        ws.Cell(2, 1).Value = $"{data.Year} Gelir-Gider Raporu";
        ws.Cell(2, 1).Style.Font.SetBold();

        ws.Cell(4, 1).Value = $"{DomainConstants.Budget.TotalLabel} {DomainConstants.Budget.IncomeLabel}";
        Money(ws.Cell(4, 2), data.TotalIncome);
        ws.Cell(5, 1).Value = $"{DomainConstants.Budget.TotalLabel} {DomainConstants.Budget.ExpenseLabel}";
        Money(ws.Cell(5, 2), data.TotalExpense);
        ws.Cell(6, 1).Value = "Net";
        Money(ws.Cell(6, 2), data.Net);
        ws.Range(4, 1, 6, 1).Style.Font.SetBold();

        string[] monthHeaders = ["Ay", DomainConstants.Budget.IncomeLabel, DomainConstants.Budget.ExpenseLabel, "Net"];
        const int monthHeaderRow = 8;
        for (var i = 0; i < monthHeaders.Length; i++)
        {
            var cell = ws.Cell(monthHeaderRow, i + 1);
            cell.Value = monthHeaders[i];
            cell.Style.Font.SetBold();
            cell.Style.Fill.SetBackgroundColor(XLColor.LightGray);
        }

        var row = monthHeaderRow + 1;
        foreach (var month in data.Months)
        {
            ws.Cell(row, 1).Value = Tr.DateTimeFormat.GetMonthName(month.Month);
            Money(ws.Cell(row, 2), month.Income);
            Money(ws.Cell(row, 3), month.Expense);
            Money(ws.Cell(row, 4), month.Net);
            row++;
        }

        row += 1;
        ws.Cell(row, 1).Value = "Hareketler";
        ws.Cell(row, 1).Style.Font.SetBold().Font.SetFontSize(12);
        row++;

        string[] lineHeaders =
        [
            "Tarih",
            "Başlık",
            "Açıklama",
            DomainConstants.Budget.IncomeLabel,
            DomainConstants.Budget.ExpenseLabel,
        ];
        for (var i = 0; i < lineHeaders.Length; i++)
        {
            var cell = ws.Cell(row, i + 1);
            cell.Value = lineHeaders[i];
            cell.Style.Font.SetBold();
            cell.Style.Fill.SetBackgroundColor(XLColor.LightGray);
        }

        row++;
        if (data.Lines.Count == 0)
        {
            ws.Cell(row, 1).Value = "Bu yıl için hareket yok.";
        }
        else
        {
            foreach (var line in data.Lines)
            {
                var income = line.Kind == DomainConstants.Budget.IncomeLabel;
                ws.Cell(row, 1).Value = line.Date.ToString("dd.MM.yyyy", Tr);
                ws.Cell(row, 2).Value = line.Title;
                ws.Cell(row, 3).Value = line.Description ?? string.Empty;
                if (income)
                    Money(ws.Cell(row, 4), line.Amount);
                else
                    Money(ws.Cell(row, 5), line.Amount);
                row++;
            }

            ws.Cell(row, 1).Value = DomainConstants.Budget.TotalLabel;
            Money(ws.Cell(row, 4), data.TotalIncome);
            Money(ws.Cell(row, 5), data.TotalExpense);
            ws.Range(row, 1, row, 5).Style.Font.SetBold();
        }

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
}