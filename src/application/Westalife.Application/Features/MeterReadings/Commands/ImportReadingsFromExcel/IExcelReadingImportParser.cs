namespace Westalife.Application.Features.MeterReadings.Commands.ImportReadingsFromExcel;

/// <summary>
/// Excel satırından çözümlenmiş tek okuma kaydı. Sayaç şu iki yoldan biriyle eşleşir:
/// Kapı No (daire) veya Sayaç Seri No (bağlantısız okuyucu programın ürettiği dosya). Seri no doluysa önceliklidir.
/// </summary>
public sealed record ExcelReadingImportRow(
    int? DoorNumber,
    DateTimeOffset ReadingDateUtc,
    decimal IndexValue,
    string? SerialNumber = null);

/// <summary>Excel okuma içe aktarma parse sonucu: geçerli satırlar + satır bazlı hata mesajları.</summary>
public sealed record ExcelReadingImportResult(
    IReadOnlyList<ExcelReadingImportRow> Rows,
    IReadOnlyList<string> Errors);

/// <summary>
/// Excel (xlsx) içeriğini okuma satırlarına ayrıştırır. Başlıklar DİNAMİK eşleştirilir
/// (case-insensitive, boşluk/nokta/Türkçe karakter duyarsız). Infrastructure'da ClosedXML ile implement edilir.
/// </summary>
public interface IExcelReadingImportParser
{
    ExcelReadingImportResult Parse(Stream stream);
}