using ClosedXML.Excel;
using Westalife.Infrastructure.Reporting.Excel;

namespace Westalife.Infrastructure.Tests;

/// <summary>
/// Bağlantısız okuyucu programın (SayacOkuyucu.exe) ürettiği dosya biçimi: Kapı No YOK, Sayaç Seri No var.
/// </summary>
public class ReadingImportBySerialTests
{
    [Fact]
    public void Parse_SerialNumberWithoutDoorColumn_ReturnsRowsKeyedBySerial()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Okuma Listesi");
        ws.Cell(1, 1).Value = "Sayaç Seri No";
        ws.Cell(1, 2).Value = "Okuma Tarihi";
        ws.Cell(1, 3).Value = "Endeks (kWh)";
        ws.Cell(1, 4).Value = "M-Bus Adresi";
        ws.Cell(2, 1).SetValue("55238358");
        ws.Cell(2, 2).SetValue(new DateTime(2026, 5, 9, 14, 30, 0));
        ws.Cell(2, 3).SetValue(20819.5m);
        ws.Cell(2, 4).SetValue(58);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var result = new ExcelReadingImportParser().Parse(stream);

        Assert.Empty(result.Errors);
        var row = Assert.Single(result.Rows);
        Assert.Equal("55238358", row.SerialNumber);
        Assert.Null(row.DoorNumber);
        Assert.Equal(20819.5m, row.IndexValue);
        Assert.Equal(new DateTime(2026, 5, 9, 14, 30, 0), row.ReadingDateUtc.DateTime);
    }
}
