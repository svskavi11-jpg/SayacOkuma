using ClosedXML.Excel;

namespace Westalife.Infrastructure.Reporting.Excel;

/// <summary>
/// Sayaç içe aktarma şablonu üretir: zorunlu 3 başlık + opsiyonel 2 başlık + örnek satır + açıklama sayfası.
/// </summary>
public static class MeterImportExcelTemplate
{
    public static byte[] Generate()
    {
        using var workbook = new XLWorkbook();

        var ws = workbook.Worksheets.Add("Sayac Listesi");
        ws.Cell(1, 1).Value = "Kapı No";
        ws.Cell(1, 2).Value = "Sayaç Seri No";
        ws.Cell(1, 3).Value = "M-Bus Adresi";
        ws.Cell(1, 4).Value = "Başlangıç Endeksi";
        ws.Cell(1, 5).Value = "Başlangıç Tarihi";
        ws.Cell(1, 6).Value = "Kat Maliki Adı Soyadı";
        ws.Cell(1, 7).Value = "m²";

        ws.Range(1, 1, 1, 7).Style.Font.SetBold();
        ws.Range(1, 1, 1, 7).Style.Fill.SetBackgroundColor(XLColor.LightGray);

        // Örnek satır (kullanıcı silip kendi verisini girer)
        ws.Cell(2, 1).Value = 1;
        ws.Cell(2, 2).Value = "55238358";
        ws.Cell(2, 3).Value = 58;
        ws.Cell(2, 4).Value = 19126.000;
        ws.Cell(2, 5).Value = "30.11.2021";
        ws.Cell(2, 6).Value = "Ahmet Yılmaz";
        ws.Cell(2, 7).Value = 120.00;

        ws.Columns().AdjustToContents();

        var notes = workbook.Worksheets.Add("Aciklamalar");
        notes.Cell(1, 1).Value = "AÇIKLAMALAR";
        notes.Cell(1, 1).Style.Font.SetBold();
        notes.Cell(2, 1).Value = "1) Zorunlu sütunlar: Kapı No, Sayaç Seri No, M-Bus Adresi (1-250 arası sayı).";
        notes.Cell(3, 1).Value = "2) Başlangıç Endeksi ve Tarihi opsiyoneldir — girilirse sayaç 'Başlangıç Endeksi' okuması ile kaydedilir (tüketim 0 kabul edilir).";
        notes.Cell(4, 1).Value = "3) Başlangıç Tarihi formatı: GG.AA.YYYY";
        notes.Cell(5, 1).Value = "4) Kat Maliki Adı Soyadı opsiyoneldir — girilirse dairenin aktif kat maliki olarak SCD Type 2 kaydı oluşturulur (geçmiş korunur).";
        notes.Cell(6, 1).Value = "5) m² opsiyoneldir — girilirse dairenin m²'si güncellenir (eski m² kaydı kapatılır, geçmiş korunur).";
        notes.Cell(7, 1).Value = "6) Bu örnek satırı silip kendi verilerinizi girin.";
        notes.Cell(8, 1).Value = "7) Başlık satırını (1. satır) silmeyin veya adlarını değiştirmeyin.";
        notes.Column(1).Width = 100;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
