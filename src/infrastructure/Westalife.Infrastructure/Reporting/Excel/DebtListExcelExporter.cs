using System.Globalization;
using ClosedXML.Excel;
using Westalife.Application.Features.GasInvoices.Queries.GetNoticeReport;

namespace Westalife.Infrastructure.Reporting.Excel;

/// <summary>
/// Yönetici borç listesi Excel çıktısı (ClosedXML, MIT — Office kurulumu gerektirmez).
/// Üst bilgi bloğu + 72 satır + Σ mutabakat kontrol satırı.
/// </summary>
public static class DebtListExcelExporter
{
    public static byte[] Generate(Westalife.Application.Features.GasInvoices.Queries.GetNoticeReport.NoticeReportData data)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Borc Listesi");

        var tr = CultureInfo.GetCultureInfo("tr-TR");

        ws.Cell(1, 1).Value = data.BuildingName;
        ws.Cell(1, 1).Style.Font.SetBold().Font.SetFontSize(13);
        ws.Cell(2, 1).Value = $"Bireysel Gider Bildirimi - {data.PeriodName}";
        ws.Cell(2, 1).Style.Font.SetBold();
        ws.Cell(3, 1).Value = $"Fatura Dönemi: {data.PeriodFrom:dd.MM.yyyy} - {data.PeriodTo:dd.MM.yyyy}";

        string[] headers =
        [
            "Kapi No", "Daire", "Sayaç Seri No", "m²", "Tüketim (kWh)",
            "Sabit Pay (%30)", "Tüketim Payı (%70)", "Toplam Borç (TL)", "Okuma Durumu"
        ];

        const int headerRow = 5;
        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(headerRow, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.SetBold();
            cell.Style.Fill.SetBackgroundColor(XLColor.LightGray);
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        }

        var currentRow = headerRow + 1;
        foreach (var row in data.Rows)
        {
            ws.Cell(currentRow, 1).Value = row.DoorNumber;
            ws.Cell(currentRow, 2).Value = row.ApartmentTitle;
            ws.Cell(currentRow, 3).Value = row.MeterSerialNumber;
            ws.Cell(currentRow, 4).Value = row.SquareMeters;
            ws.Cell(currentRow, 5).Value = row.ConsumptionKWh;
            ws.Cell(currentRow, 6).Value = row.FixedAmount;
            ws.Cell(currentRow, 7).Value = row.ConsumptionAmount;
            ws.Cell(currentRow, 8).Value = row.TotalAmount;
            ws.Cell(currentRow, 9).Value = row.IsMeterReadFailed ? "OKUNAMADI" : "OKUNDU";

            ws.Cell(currentRow, 4).Style.NumberFormat.SetFormat("#,##0.000");
            ws.Cell(currentRow, 5).Style.NumberFormat.SetFormat("#,##0.000");
            ws.Cell(currentRow, 6).Style.NumberFormat.SetFormat("#,##0.00");
            ws.Cell(currentRow, 7).Style.NumberFormat.SetFormat("#,##0.00");
            ws.Cell(currentRow, 8).Style.NumberFormat.SetFormat("#,##0.00");

            if (row.IsMeterReadFailed)
            {
                ws.Cell(currentRow, 9).Style.Font.SetFontColor(XLColor.OrangeRed);
            }

            currentRow++;
        }

        // Mutabakat özet satırı — Kural #1 kontrolü Excel çıktısında da görünür.
        ws.Cell(currentRow, 7).Value = "GENEL TOPLAM:";
        ws.Cell(currentRow, 7).Style.Font.SetBold().Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
        ws.Cell(currentRow, 8).Value = data.Rows.Sum(r => r.TotalAmount);
        ws.Cell(currentRow, 8).Style.Font.SetBold().NumberFormat.SetFormat("#,##0.00");
        ws.Cell(currentRow, 9).Value = data.Rows.Sum(r => r.TotalAmount) == data.TotalInvoiceAmount
            ? "FATURA İLE MUTABIK ✔"
            : "MUTABAKAT HATASI!";

        ws.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
