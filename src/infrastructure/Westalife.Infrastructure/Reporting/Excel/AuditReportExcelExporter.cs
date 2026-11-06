using ClosedXML.Excel;
using Westalife.Application.Features.Reports;

namespace Westalife.Infrastructure.Reporting.Excel;

public static class AuditReportExcelExporter
{
    public static byte[] IncomeExpense(AuditIncomeExpenseReport data)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Gelir Gider");
        var periodLabel = data.From.HasValue && data.To.HasValue
            ? $"{data.From:dd.MM.yyyy} - {data.To:dd.MM.yyyy}"
            : $"{data.Year}";
        Title(ws, data.BuildingName, $"Gelir-Gider Özeti ({periodLabel})");
        var row = Header(ws, 4, "Kalem", "Tahmini", "Gider", "Mahsup", "Gerçekleşen", "Kalan");
        foreach (var line in data.Expenses)
        {
            ws.Cell(row, 1).Value = line.Category;
            Money(ws, row, 2, line.Estimated);
            Money(ws, row, 3, line.ExpenseAmount);
            Money(ws, row, 4, line.WaiverAmount);
            Money(ws, row, 5, line.Actual);
            Money(ws, row, 6, line.Remaining);
            row++;
        }

        ws.Cell(row, 1).Value = "TOPLAM";
        ws.Cell(row, 1).Style.Font.SetBold();
        Money(ws, row, 2, data.TotalEstimated);
        Money(ws, row, 3, data.Expenses.Sum(x => x.ExpenseAmount));
        Money(ws, row, 4, data.Expenses.Sum(x => x.WaiverAmount));
        Money(ws, row, 5, data.TotalActual);
        Money(ws, row, 6, data.TotalRemaining);
        row += 2;

        row = Header(ws, row, "Gelir", "Tutar");
        foreach (var line in data.Income)
        {
            ws.Cell(row, 1).Value = line.Title;
            Money(ws, row, 2, line.Amount);
            row++;
        }

        ws.Cell(row, 1).Value = "TOPLAM GELİR";
        ws.Cell(row, 1).Style.Font.SetBold();
        Money(ws, row, 2, data.TotalIncome);
        row++;

        ws.Cell(row, 1).Value = "NET (Gelir − Gerçekleşen Gider)";
        ws.Cell(row, 1).Style.Font.SetBold();
        Money(ws, row, 2, data.Net);
        row += 2;

        row = Header(ws, row, "Banka", "Açılış", "Giren", "Çıkan", "Kapanış");
        foreach (var bank in data.Banks)
        {
            ws.Cell(row, 1).Value = bank.Label;
            Money(ws, row, 2, bank.Opening);
            Money(ws, row, 3, bank.MoneyIn);
            Money(ws, row, 4, bank.MoneyOut);
            Money(ws, row, 5, bank.Closing);
            row++;
        }

        ws.Columns().AdjustToContents();
        return Save(workbook);
    }

    public static byte[] GeneralAssembly(GeneralAssemblyReport data)
    {
        using var workbook = new XLWorkbook();
        var periodLabel = $"{data.From:dd.MM.yyyy} - {data.To:dd.MM.yyyy}";

        // 1. Sayfa: Genel Özet, Gelir-Gider ve Banka Mutabakatı
        var wsSummary = workbook.Worksheets.Add("1-Genel Ozet ve Gelir Gider");
        Title(wsSummary, data.BuildingName, $"Genel Kurul ve Denetim Kurulu Resmi Raporu ({periodLabel})");

        var row = Header(wsSummary, 4, "Finansal Özet Kalemi", "Tutar (TL)");
        var kpis = new (string Title, decimal Value)[]
        {
            ("Toplam Tahsilat (Gelir)", data.TotalIncome),
            ("Nakit / Banka Giderleri", data.TotalCashExpense),
            ("Görev Mahsubu Giderleri", data.TotalWaiverExpense),
            ("Toplam Gerçekleşen Gider", data.TotalActualExpense),
            ("Dönem Net Nakit Farkı (Gelir - Gider)", data.NetIncomeExpense),
            ("Dönem Başı Banka Mevcudu", data.TotalBankOpeningBalance),
            ("Dönem Sonu Banka Mevcudu", data.TotalBankClosingBalance),
            ("Toplam Daire Alacakları (Borçlu Daireler)", data.TotalReceivables),
            ("Alacaklı Dairelere Borçlar (Fazla Ödeme/Devir)", data.TotalResidentCredits),
        };
        foreach (var kpi in kpis)
        {
            wsSummary.Cell(row, 1).Value = kpi.Title;
            Money(wsSummary, row, 2, kpi.Value);
            row++;
        }

        row += 1;
        row = Header(wsSummary, row, "Gider Kalemi", "Tahmini Bütçe", "Nakit/Banka Harcama", "Görev Mahsubu", "Gerçekleşen Toplam", "Kalan Bütçe");
        foreach (var exp in data.ExpenseRows)
        {
            wsSummary.Cell(row, 1).Value = exp.Category;
            Money(wsSummary, row, 2, exp.Estimated);
            Money(wsSummary, row, 3, exp.ExpenseAmount);
            Money(wsSummary, row, 4, exp.WaiverAmount);
            Money(wsSummary, row, 5, exp.Actual);
            Money(wsSummary, row, 6, exp.Remaining);
            row++;
        }

        wsSummary.Cell(row, 1).Value = "TOPLAM GİDER";
        wsSummary.Cell(row, 1).Style.Font.SetBold();
        Money(wsSummary, row, 2, data.TotalEstimatedExpense);
        Money(wsSummary, row, 3, data.TotalCashExpense);
        Money(wsSummary, row, 4, data.TotalWaiverExpense);
        Money(wsSummary, row, 5, data.TotalActualExpense);
        Money(wsSummary, row, 6, data.TotalRemainingBudget);
        row += 2;

        row = Header(wsSummary, row, "Gelir Kalemi", "Tutar (TL)");
        foreach (var inc in data.IncomeRows)
        {
            wsSummary.Cell(row, 1).Value = inc.Title;
            Money(wsSummary, row, 2, inc.Amount);
            row++;
        }

        wsSummary.Cell(row, 1).Value = "TOPLAM GELİR";
        wsSummary.Cell(row, 1).Style.Font.SetBold();
        Money(wsSummary, row, 2, data.TotalIncome);
        row += 2;

        row = Header(wsSummary, row, "Banka Hesabı", "Dönem Başı Açılış", "Dönem İçi Giren", "Dönem İçi Çıkan", "Dönem Sonu Kapanış");
        foreach (var bank in data.Banks)
        {
            wsSummary.Cell(row, 1).Value = bank.Label;
            Money(wsSummary, row, 2, bank.Opening);
            Money(wsSummary, row, 3, bank.MoneyIn);
            Money(wsSummary, row, 4, bank.MoneyOut);
            Money(wsSummary, row, 5, bank.Closing);
            row++;
        }

        wsSummary.Columns().AdjustToContents();

        // 2. Sayfa: Daire Bazlı Alacak / Borç ve Görev Mahsubu Cetveli
        var wsTracking = workbook.Worksheets.Add("2-Daire Borc Alacak Cetveli");
        Title(wsTracking, data.BuildingName, $"Daire Takip ve Alacak/Borç Cetveli ({periodLabel})");
        var tRow = Header(
            wsTracking, 4,
            "Kapı", "Daire", "Devir", "Ek Aidat", "Aidat Tahakkuku", "Yakıt Tahakkuku",
            "Aidat Tahsilatı", "Yakıt Tahsilatı", "Görev Mahsubu", "Gecikme Faizi", "Net Bakiye");
        foreach (var line in data.ApartmentRows.Append(data.ApartmentTotals))
        {
            wsTracking.Cell(tRow, 1).Value = line.DoorNumber;
            wsTracking.Cell(tRow, 2).Value = line.Title;
            Money(wsTracking, tRow, 3, line.Devir);
            Money(wsTracking, tRow, 4, line.ExtraAidat);
            Money(wsTracking, tRow, 5, line.AidatAccrual);
            Money(wsTracking, tRow, 6, line.FuelAccrual);
            Money(wsTracking, tRow, 7, line.AidatCollected);
            Money(wsTracking, tRow, 8, line.FuelCollected);
            Money(wsTracking, tRow, 9, line.Waiver);
            Money(wsTracking, tRow, 10, line.Interest);
            Money(wsTracking, tRow, 11, line.Net);
            tRow++;
        }

        wsTracking.Columns().AdjustToContents();

        // 3. Sayfa: Dinamik Aylık Matris (Seçilen yönetim döneminin başlangıç-bitiş aylarına göre)
        var wsMatrix = workbook.Worksheets.Add("3-Dinamik Aylik Matris");
        Title(wsMatrix, data.BuildingName, $"Dinamik Aylık Gelir-Gider ve Daire Tahsilat Matrisi ({periodLabel})");

        var monthHeaders = new List<string> { "Kalem / Bağımsız Bölüm" };
        monthHeaders.AddRange(data.MonthColumns.Select(c => c.Label));
        monthHeaders.Add("DÖNEM TOPLAMI");

        var mRow = Header(wsMatrix, 4, monthHeaders.ToArray());
        foreach (var summaryLine in data.MonthlySummaryRows)
        {
            wsMatrix.Cell(mRow, 1).Value = summaryLine.RowTitle;
            wsMatrix.Cell(mRow, 1).Style.Font.SetBold();
            for (var i = 0; i < data.MonthColumns.Count; i++)
            {
                var key = data.MonthColumns[i].Key;
                Money(wsMatrix, mRow, i + 2, summaryLine.MonthValues.GetValueOrDefault(key));
            }

            Money(wsMatrix, mRow, data.MonthColumns.Count + 2, summaryLine.Total);
            mRow++;
        }

        mRow += 2;
        var aptMatrixHeaders = new List<string> { "Kapı", "Daire", "Dönem Başı Devir" };
        aptMatrixHeaders.AddRange(data.MonthColumns.Select(c => $"{c.Label} Tahsilat"));
        aptMatrixHeaders.AddRange(["Toplam Tahakkuk", "Toplam Tahsilat", "Görev Mahsubu", "Gecikme Faizi", "Dönem Sonu Bakiye"]);

        mRow = Header(wsMatrix, mRow, aptMatrixHeaders.ToArray());
        foreach (var apt in data.ApartmentMatrixRows)
        {
            wsMatrix.Cell(mRow, 1).Value = apt.DoorNumber;
            wsMatrix.Cell(mRow, 2).Value = apt.Title;
            Money(wsMatrix, mRow, 3, apt.OpeningBalance);

            for (var i = 0; i < data.MonthColumns.Count; i++)
            {
                var key = data.MonthColumns[i].Key;
                Money(wsMatrix, mRow, i + 4, apt.MonthlyCollections.GetValueOrDefault(key));
            }

            var offset = data.MonthColumns.Count + 4;
            Money(wsMatrix, mRow, offset, apt.TotalAccrual);
            Money(wsMatrix, mRow, offset + 1, apt.TotalCollected);
            Money(wsMatrix, mRow, offset + 2, apt.TotalWaiver);
            Money(wsMatrix, mRow, offset + 3, apt.TotalInterest);
            Money(wsMatrix, mRow, offset + 4, apt.ClosingBalance);
            mRow++;
        }

        wsMatrix.Columns().AdjustToContents();
        return Save(workbook);
    }

    public static byte[] ApartmentTracking(ApartmentTrackingReport data)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Daire Takip");
        Title(ws, data.BuildingName, $"Daire Takip {data.From:dd.MM.yyyy} - {data.To:dd.MM.yyyy}");
        var row = Header(
            ws, 4,
            "Kapı", "Daire", "Devir", "Ek Aidat", "Aidat Tahakkuku", "Yakıt Tahakkuku",
            "Aidat Tahsilatı", "Yakıt Tahsilatı", "Görev Mahsubu", "Gecikme Faizi", "Net Bakiye");
        foreach (var line in data.Rows.Append(data.Totals))
        {
            ws.Cell(row, 1).Value = line.DoorNumber;
            ws.Cell(row, 2).Value = line.Title;
            Money(ws, row, 3, line.Devir);
            Money(ws, row, 4, line.ExtraAidat);
            Money(ws, row, 5, line.AidatAccrual);
            Money(ws, row, 6, line.FuelAccrual);
            Money(ws, row, 7, line.AidatCollected);
            Money(ws, row, 8, line.FuelCollected);
            Money(ws, row, 9, line.Waiver);
            Money(ws, row, 10, line.Interest);
            Money(ws, row, 11, line.Net);
            row++;
        }

        ws.Columns().AdjustToContents();
        return Save(workbook);
    }

    public static byte[] Fuel(FuelBreakdownReport data)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Yakıt");
        Title(ws, string.Empty, $"Yakıt Dökümü {data.From:dd.MM.yyyy} - {data.To:dd.MM.yyyy}");
        var row = Header(
            ws, 4,
            "Dönem", "Fatura", "Kapı", "Daire", "m²", "kWh", "Sabit", "Tüketim", "Toplam");
        foreach (var line in data.Rows)
        {
            ws.Cell(row, 1).Value = $"{line.PeriodFrom:dd.MM.yyyy} - {line.PeriodTo:dd.MM.yyyy}";
            ws.Cell(row, 2).Value = line.InvoiceNumber;
            ws.Cell(row, 3).Value = line.DoorNumber;
            ws.Cell(row, 4).Value = line.Title;
            Money(ws, row, 5, line.SquareMeters);
            Money(ws, row, 6, line.ConsumptionKWh);
            Money(ws, row, 7, line.FixedAmount);
            Money(ws, row, 8, line.ConsumptionAmount);
            Money(ws, row, 9, line.TotalAmount);
            row++;
        }

        ws.Cell(row, 8).Value = "Toplam";
        Money(ws, row, 9, data.Total);
        ws.Columns().AdjustToContents();
        return Save(workbook);
    }

    public static byte[] Bank(BankReconciliationReport data)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Banka");
        Title(ws, data.Label, $"Banka Kontrolü {data.From:dd.MM.yyyy} - {data.To:dd.MM.yyyy}");
        ws.Cell(4, 1).Value = "Açılış";
        Money(ws, 4, 2, data.Opening);
        ws.Cell(5, 1).Value = "Giren";
        Money(ws, 5, 2, data.MoneyIn);
        ws.Cell(6, 1).Value = "Çıkan";
        Money(ws, 6, 2, data.MoneyOut);
        ws.Cell(7, 1).Value = "Kapanış";
        Money(ws, 7, 2, data.Closing);
        var row = Header(ws, 9, "Tarih", "Tür", "Açıklama", "Giren", "Çıkan");
        foreach (var line in data.Movements)
        {
            ws.Cell(row, 1).Value = line.Date.ToString("dd.MM.yyyy");
            ws.Cell(row, 2).Value = line.Kind;
            ws.Cell(row, 3).Value = line.Description;
            Money(ws, row, 4, line.MoneyIn);
            Money(ws, row, 5, line.MoneyOut);
            row++;
        }

        ws.Columns().AdjustToContents();
        return Save(workbook);
    }

    private static void Title(IXLWorksheet ws, string building, string report)
    {
        ws.Cell(1, 1).Value = building;
        ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(14);
        ws.Cell(2, 1).Value = report;
        ws.Cell(2, 1).Style.Font.SetBold();
    }

    private static int Header(IXLWorksheet ws, int row, params string[] headers)
    {
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(row, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.SetBold();
            cell.Style.Fill.SetBackgroundColor(XLColor.LightGray);
        }

        return row + 1;
    }

    private static void Money(IXLWorksheet ws, int row, int column, decimal amount)
    {
        var cell = ws.Cell(row, column);
        cell.Value = amount;
        cell.Style.NumberFormat.Format = "#,##0.00";
    }

    private static byte[] Save(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
