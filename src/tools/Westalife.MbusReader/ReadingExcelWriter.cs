using ClosedXML.Excel;
using Westalife.Application.Common.Models;

namespace Westalife.MbusReader;

/// <summary>
/// Okumaları ana yazılımın "Okuma İçe Aktar" biçiminde .xlsx'e yazar. Başlıklar ana yazılımın
/// ExcelReadingImportParser'ı ile eşleşir: "Sayaç Seri No", "Okuma Tarihi", "Endeks (kWh)".
/// Eşleştirme seri no iledir (bu programda kapı no bilinmez); ek sütunlar içe aktarmada yok sayılır.
/// Tarih, okuma anının YEREL saatidir (ana yazılımın içe aktarma kuralı ile tutarlı).
/// </summary>
public static class ReadingExcelWriter
{
    public static void Save(string path, IReadOnlyList<MeterReadResult> results, DateTime readAtLocal)
    {
        using var workbook = new XLWorkbook();

        var ws = workbook.Worksheets.Add("Okuma Listesi");
        string[] headers =
        [
            "Sayaç Seri No", "Okuma Tarihi", "Endeks (kWh)", "M-Bus Adresi",
            "Hacim (m3)", "Güç (kW)", "Gidiş Sıcaklığı (°C)", "Dönüş Sıcaklığı (°C)",
        ];
        for (var i = 0; i < headers.Length; i++)
        {
            ws.Cell(1, i + 1).Value = headers[i];
        }

        ws.Range(1, 1, 1, headers.Length).Style.Font.SetBold();
        ws.Range(1, 1, 1, headers.Length).Style.Fill.SetBackgroundColor(XLColor.LightGray);

        var row = 2;
        foreach (var snapshot in results.Where(r => r.IsSuccess && r.Snapshot is not null)
                     .Select(r => r.Snapshot!).OrderBy(s => s.PrimaryAddress))
        {
            // Seri no metin olarak yazılır (baştaki sıfırlar/uzun sayılar bozulmasın).
            ws.Cell(row, 1).SetValue(snapshot.SerialNumber);
            ws.Cell(row, 2).SetValue(readAtLocal);
            ws.Cell(row, 2).Style.DateFormat.Format = "dd.MM.yyyy HH:mm";
            ws.Cell(row, 3).SetValue(snapshot.CumulativeEnergyKWh);
            ws.Cell(row, 3).Style.NumberFormat.Format = "0.000";
            ws.Cell(row, 4).SetValue(snapshot.PrimaryAddress);
            ws.Cell(row, 5).SetValue(snapshot.CumulativeVolumeM3);
            ws.Cell(row, 6).SetValue(snapshot.PowerKw);
            if (snapshot.FlowTemperatureC is { } flow)
            {
                ws.Cell(row, 7).SetValue(flow);
            }

            if (snapshot.ReturnTemperatureC is { } back)
            {
                ws.Cell(row, 8).SetValue(back);
            }

            row++;
        }

        ws.Columns().AdjustToContents();

        // Okunamayan (cevap verip veri vermeyen) sayaçlar ayrı sayfada; içe aktarmada kullanılmaz.
        var failures = results.Where(r => !r.IsSuccess).ToList();
        if (failures.Count > 0)
        {
            var errors = workbook.Worksheets.Add("Okunamayanlar");
            errors.Cell(1, 1).Value = "M-Bus Adresi";
            errors.Cell(1, 2).Value = "Sayaç Seri No";
            errors.Cell(1, 3).Value = "Hata";
            errors.Range(1, 1, 1, 3).Style.Font.SetBold();

            var errorRow = 2;
            foreach (var failure in failures.OrderBy(f => f.PrimaryAddress))
            {
                errors.Cell(errorRow, 1).SetValue(failure.PrimaryAddress);
                errors.Cell(errorRow, 2).SetValue(failure.SerialNumber ?? string.Empty);
                errors.Cell(errorRow, 3).SetValue(failure.ErrorMessage ?? string.Empty);
                errorRow++;
            }

            errors.Columns().AdjustToContents();
        }

        workbook.SaveAs(path);
    }
}
