using System.Globalization;
using ClosedXML.Excel;
using Westalife.Application.Features.MeterReadings.Commands.ImportReadingsFromExcel;

namespace Westalife.Infrastructure.Reporting.Excel;

/// <summary>
/// Okuma içe aktarma Excel parser'ı (Etap 6.6). ExcelMeterImportParser ile aynı dinamik
/// başlık eşlemesi kullanılır: "Kapı No", "Okuma Tarihi", "Endeks (kWh)" (TR tarih/sayı formatı).
/// </summary>
public sealed class ExcelReadingImportParser : IExcelReadingImportParser
{
    private const int MaxRows = 500;

    public ExcelReadingImportResult Parse(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheet(1);
        var usedRows = ws.RowsUsed().ToList();

        var errors = new List<string>();
        var rows = new List<ExcelReadingImportRow>();

        if (usedRows.Count == 0)
        {
            errors.Add("Excel dosyası boş görünüyor (başlık satırı dahi yok).");
            return new ExcelReadingImportResult(rows, errors);
        }

        // --- Dinamik başlık eşlemesi (ExcelMeterImportParser ile aynı normalize) ---
        var headerRow = usedRows[0];
        var columnMap = new Dictionary<string, int>();

        foreach (var cell in headerRow.CellsUsed())
        {
            columnMap[Normalize(cell.GetString())] = cell.Address.ColumnNumber;
        }

        int? GetColumn(params string[] aliases)
        {
            foreach (var alias in aliases)
            {
                if (columnMap.TryGetValue(alias, out var col))
                {
                    return col;
                }
            }

            return null;
        }

        var doorCol = GetColumn("kapino", "daireno", "daire");
        var serialCol = GetColumn("sayacserino", "serino", "sayacno");
        var dateCol = GetColumn("okumatarihi", "tarih", "tarihsaat");
        var indexCol = GetColumn("endekskwh", "endeks", "kumulatifendeks", "kwh");

        if ((doorCol is null && serialCol is null) || dateCol is null || indexCol is null)
        {
            errors.Add(
                "Zorunlu başlıklar bulunamadı. Şablon başlıkları: \"Kapı No\" (veya \"Sayaç Seri No\"), \"Okuma Tarihi\", \"Endeks (kWh)\". " +
                $"Dosyada bulunan başlıklar: {string.Join(", ", columnMap.Keys)}");
            return new ExcelReadingImportResult(rows, errors);
        }

        // --- Veri satırları ---
        foreach (var dataRow in usedRows.Skip(1))
        {
            var rowNumber = dataRow.RowNumber();
            var doorText = doorCol is null ? string.Empty : dataRow.Cell(doorCol.Value).GetString().Trim();
            var serialText = serialCol is null ? string.Empty : dataRow.Cell(serialCol.Value).GetString().Trim();
            var dateCell = dataRow.Cell(dateCol.Value);
            var indexCell = dataRow.Cell(indexCol.Value);
            var dateText = dateCell.GetString().Trim();
            var indexText = indexCell.GetString().Trim();

            if (string.IsNullOrWhiteSpace(doorText) && string.IsNullOrWhiteSpace(serialText)
                && string.IsNullOrWhiteSpace(dateText) && string.IsNullOrWhiteSpace(indexText))
            {
                continue; // tamamen boş satır — sessizce atla
            }

            // Seri no doluysa eşleştirme seri no ile yapılır; kapı no bu durumda opsiyoneldir.
            int? doorNumber = null;
            if (string.IsNullOrWhiteSpace(serialText) || !string.IsNullOrWhiteSpace(doorText))
            {
                if (!int.TryParse(doorText, out var parsedDoor) || parsedDoor < 1)
                {
                    errors.Add($"Satır {rowNumber}: Kapı No sayısal olmalıdır (girilen: \"{doorText}\").");
                    continue;
                }

                doorNumber = parsedDoor;
            }

            DateTimeOffset readingDate;
            if (dateCell.DataType == XLDataType.DateTime && dateCell.TryGetValue(out DateTime cellDate))
            {
                // Hücre gerçek Excel tarih/saat değeri (örn. Izar Center dışa aktarımı) → direkt kullan.
                readingDate = new DateTimeOffset(DateTime.SpecifyKind(cellDate, DateTimeKind.Unspecified), TimeSpan.Zero);
            }
            else if (TryParseReadingDate(dateText, out var parsedDate))
            {
                readingDate = new DateTimeOffset(parsedDate, TimeSpan.Zero);
            }
            else
            {
                errors.Add($"Satır {rowNumber}: Okuma Tarihi geçerli bir tarih olmalıdır (girilen: \"{dateText}\").");
                continue;
            }

            if (!TryParseIndex(indexText, out var index))
            {
                errors.Add($"Satır {rowNumber}: Endeks (kWh) sayısal olmalıdır (girilen: \"{indexText}\").");
                continue;
            }

            if (index < 0m)
            {
                errors.Add($"Satır {rowNumber}: Endeks (kWh) negatif olamaz (girilen: \"{indexText}\").");
                continue;
            }

            rows.Add(new ExcelReadingImportRow(
                doorNumber, readingDate, index, string.IsNullOrWhiteSpace(serialText) ? null : serialText));

            if (rows.Count >= MaxRows)
            {
                errors.Add($"Tek seferde en fazla {MaxRows} satır içe aktarılabilir; kalan satırlar atlandı.");
                break;
            }
        }

        return new ExcelReadingImportResult(rows, errors);
    }

    /// <summary>
    /// Tarih metni ayrıştırma: önce bilinen biçimler InvariantCulture ile (örn. "2026-04-08 00:00:00",
    /// "08.04.2026"), sonra genel tr-TR ve InvariantCulture TryParse. Hiçbiri uymazsa false — asla fırlatmaz.
    /// </summary>
    private static bool TryParseReadingDate(string text, out DateTime value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string[] formats =
        [
            "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd",
            "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy HH:mm", "dd.MM.yyyy",
            "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "dd/MM/yyyy",
        ];

        foreach (var format in formats)
        {
            if (DateTime.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
            {
                return true;
            }
        }

        return DateTime.TryParse(text, CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.None, out value)
            || DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }

    /// <summary>
    /// Endeks ayrıştırma: hem TR biçimi ("1000,500" ondalık / "1.234.567" binlik) hem Invariant
    /// biçimi ("2929.888" ondalık — Izar Center dışa aktarımı) desteklenir. Kural: her iki ayraç da
    /// varsa sonda görünen ondalık ayracıdır; tek ayraç nokta ise ondalık, ardışık çoklu ayraç binliktir.
    /// </summary>
    private static bool TryParseIndex(string text, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        text = text.Replace("\u00A0", string.Empty).Replace(" ", string.Empty).Trim();

        var hasDot = text.Contains('.');
        var hasComma = text.Contains(',');

        if (hasDot && hasComma)
        {
            var decimalChar = text.LastIndexOf('.') > text.LastIndexOf(',') ? '.' : ',';
            var thousandsChar = decimalChar == '.' ? ',' : '.';
            text = text.Replace(thousandsChar.ToString(), string.Empty).Replace(decimalChar, '.');
        }
        else if (hasDot && text.Count(c => c == '.') > 1)
        {
            text = text.Replace(".", string.Empty); // "1.234.567" → binlik ayraç
        }
        else if (hasComma && text.Count(c => c == ',') > 1)
        {
            text = text.Replace(",", string.Empty); // "1,234,567" → binlik ayraç
        }
        else if (hasComma)
        {
            text = text.Replace(',', '.'); // "1000,500" → "1000.500"
        }

        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value)
            && value >= 0m;
    }

    private static string Normalize(string header)
    {
        // Türkçe karakterler eşlenir: Kapı No → kapino, Okuma Tarihi → okumatarihi
        var mapped = new System.Text.StringBuilder(header.Length);
        foreach (var ch in header.ToLowerInvariant())
        {
            mapped.Append(ch switch
            {
                'ı' or 'i' or 'İ' => 'i',
                'ş' or 's' => 's',
                'ğ' or 'g' => 'g',
                'ü' or 'u' => 'u',
                'ö' or 'o' => 'o',
                'ç' or 'c' => 'c',
                _ => ch
            });
        }

        var chars = mapped.ToString().Where(char.IsLetterOrDigit).ToArray();
        return new string(chars);
    }
}