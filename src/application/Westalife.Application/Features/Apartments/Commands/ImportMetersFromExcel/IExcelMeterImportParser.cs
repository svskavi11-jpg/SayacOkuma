namespace Westalife.Application.Features.Apartments.Commands.ImportMetersFromExcel;

/// <summary>
/// Excel satırından çözümlenmiş tek sayaç kaydı (Etap 6.7): opsiyonel başlangıç endeksi +
/// opsiyonel "Kat Maliki Adı Soyadı" ve "m²" (SCD Type 2 uyumlu owner/dimension kaydı için).
/// </summary>
public sealed record ExcelMeterImportRow(
    int DoorNumber,
    string SerialNumber,
    int PrimaryAddress,
    decimal? InitialIndexValue,
    DateTimeOffset? InitialReadingDate,
    string? OwnerName,
    decimal? SquareMeters);

/// <summary>Excel içe aktarma parse sonucu: geçerli satırlar + satır bazlı hata mesajları.</summary>
public sealed record ExcelMeterImportResult(
    IReadOnlyList<ExcelMeterImportRow> Rows,
    IReadOnlyList<string> Errors);

/// <summary>
/// Excel (xlsx) içeriğini sayaç satırlarına ayrıştırır. Başlıklar DİNAMİK eşleştirilir
/// (case-insensitive, boşluk/nokta duyarsız). Infrastructure'da ClosedXML ile implement edilir.
/// </summary>
public interface IExcelMeterImportParser
{
    ExcelMeterImportResult Parse(Stream stream);
}
