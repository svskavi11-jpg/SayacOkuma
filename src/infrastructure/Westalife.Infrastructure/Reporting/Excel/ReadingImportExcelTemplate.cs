using ClosedXML.Excel;

namespace Westalife.Infrastructure.Reporting.Excel;

/// <summary>
/// Okuma içe aktarma şablonu üretir (Etap 6.6): zorunlu 3 başlık + örnek satır + açıklama sayfası.
/// </summary>
public static class ReadingImportExcelTemplate
{
    public static byte[] Generate()
    {
        using var workbook = new XLWorkbook();

        var ws = workbook.Worksheets.Add("Okuma Listesi");
        ws.Cell(1, 1).Value = "Kapı No";
        ws.Cell(1, 2).Value = "Okuma Tarihi";
        ws.Cell(1, 3).Value = "Endeks (kWh)";

        ws.Range(1, 1, 1, 3).Style.Font.SetBold();
        ws.Range(1, 1, 1, 3).Style.Fill.SetBackgroundColor(XLColor.LightGray);

        // Örnek satır (kullanıcı silip kendi verisini girer)
        ws.Cell(2, 1).Value = 1;
        ws.Cell(2, 2).Value = "30.11.2021";
        ws.Cell(2, 3).Value = 19126.000;

        ws.Columns().AdjustToContents();

        var notes = workbook.Worksheets.Add("Aciklamalar");
        notes.Cell(1, 1).Value = "AÇIKLAMALAR";
        notes.Cell(1, 1).Style.Font.SetBold();
        notes.Cell(2, 1).Value = "1) Zorunlu sütunlar: Kapı No, Okuma Tarihi, Endeks (kWh).";
        notes.Cell(3, 1).Value = "2) Endeks KÜMÜLATİF değerdir (sayacın gösterdiği toplam); dönem tüketimi sistemce fark hesabıyla bulunur.";
        notes.Cell(4, 1).Value = "3) Okuma Tarihi formatı: GG.AA.YYYY (saat opsiyoneldir).";
        notes.Cell(5, 1).Value = "4) Kapı No, sistemdeki dairelerle eşleşmelidir; eşleşmeyen satırlar raporlanır.";
        notes.Cell(6, 1).Value = "5) Bu örnek satırı silip kendi verilerinizi girin. Başlık satırını (1. satır) silmeyin.";
        notes.Cell(7, 1).Value = "6) Kilitli fatura dönemlerine düşen satırlar reddedilir.";
        notes.Column(1).Width = 100;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}