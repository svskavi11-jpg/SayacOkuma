using System.Globalization;
using ClosedXML.Excel;
using Westalife.Application.Features.Apartments.Commands.ImportMetersFromExcel;

namespace Westalife.Infrastructure.Reporting.Excel;

/// <summary>
/// ClosedXML ile Excel içe aktarma parser'ı. Başlık eşlemesi DİNAMİKTİR:
/// hücre değerleri normalize edilip (küçük harf, boşluk/nokta temizli) bilinen başlık
/// varyasyonlarına ("Kapı No", "Kapi No", "kapino", "Daire No"...) eşlenir.
/// </summary>
public sealed class ExcelMeterImportParser : IExcelMeterImportParser
{
    private const int MaxRows = 500;

    public ExcelMeterImportResult Parse(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheet(1);
        var usedRows = ws.RowsUsed().ToList();

        var errors = new List<string>();
        var rows = new List<ExcelMeterImportRow>();

        if (usedRows.Count == 0)
        {
            errors.Add("Excel dosyası boş görünüyor (başlık satırı dahi yok).");
            return new ExcelMeterImportResult(rows, errors);
        }

        // --- Dinamik başlık eşlemesi ---
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
        var addressCol = GetColumn("mbusadresi", "birinciladres", "adres");
        var initialIndexCol = GetColumn("baslangicendeksi", "ilkendeks", "endeks");
        var initialDateCol = GetColumn("baslangictarihi", "baslangicendekstitarihi", "ilkokumatarihi");
        // Etap 6.7: opsiyonel kat maliki ve m² sütunları.
        var ownerCol = GetColumn("katmalikiadisoyadi", "katmaliki", "malik", "sahibi", "adisoyadi");
        var squareMetersCol = GetColumn("m2", "metrekare", "alan");

        if (doorCol is null || serialCol is null || addressCol is null)
        {
            errors.Add(
                "Zorunlu başlıklar bulunamadı. Şablon başlıkları: \"Kapı No\", \"Sayaç Seri No\", \"M-Bus Adresi\" " +
                "(opsiyonel: \"Başlangıç Endeksi\", \"Başlangıç Tarihi\"). " +
                $"Dosyada bulunan başlıklar: {string.Join(", ", columnMap.Keys)}");
            return new ExcelMeterImportResult(rows, errors);
        }

        // --- Veri satırları ---
        foreach (var dataRow in usedRows.Skip(1))
        {
            var rowNumber = dataRow.RowNumber();
            var doorText = dataRow.Cell(doorCol.Value).GetString().Trim();
            var serial = dataRow.Cell(serialCol.Value).GetString().Trim();
            var addressText = dataRow.Cell(addressCol.Value).GetString().Trim();

            if (string.IsNullOrWhiteSpace(doorText) && string.IsNullOrWhiteSpace(serial))
            {
                continue; // tamamen boş satır — sessizce atla
            }

            if (!int.TryParse(doorText, out var doorNumber) || doorNumber < 1)
            {
                errors.Add($"Satır {rowNumber}: Kapı No sayısal olmalıdır (girilen: \"{doorText}\").");
                continue;
            }

            if (string.IsNullOrWhiteSpace(serial))
            {
                errors.Add($"Satır {rowNumber}: Sayaç Seri No boş.");
                continue;
            }

            if (!int.TryParse(addressText, out var primaryAddress) ||
                primaryAddress < 1 || primaryAddress > 250)
            {
                errors.Add($"Satır {rowNumber}: M-Bus Adresi 1-250 arasında sayı olmalıdır (girilen: \"{addressText}\").");
                continue;
            }

            decimal? initialIndex = null;
            DateTimeOffset? initialDate = null;

            if (initialIndexCol is not null)
            {
                var indexCell = dataRow.Cell(initialIndexCol.Value);
                var indexText = indexCell.GetString().Trim();
                if (indexCell.DataType == ClosedXML.Excel.XLDataType.Number && indexCell.TryGetValue(out decimal indexNumber))
                {
                    // Gerçek sayı hücresi: metne çevirmeden oku (ondalık/binlik ayraç karmaşası olmaz).
                    initialIndex = indexNumber;
                }
                else if (!string.IsNullOrWhiteSpace(indexText))
                {
                    if (decimal.TryParse(indexText, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), out var parsed) ||
                        decimal.TryParse(indexText, NumberStyles.Any, CultureInfo.InvariantCulture, out parsed))
                    {
                        initialIndex = parsed;
                    }
                    else
                    {
                        errors.Add($"Satır {rowNumber}: Başlangıç Endeksi sayısal olmalıdır (girilen: \"{indexText}\").");
                        continue;
                    }
                }
            }

            if (initialDateCol is not null)
            {
                var dateCell = dataRow.Cell(initialDateCol.Value);
                var dateText = dateCell.GetString().Trim();

                if (dateCell.DataType == ClosedXML.Excel.XLDataType.DateTime && dateCell.TryGetValue(out DateTime cellDate))
                {
                    // Gerçek Excel tarih hücresi: metne çevirmeden doğrudan kullan (gün/ay kayması olmaz).
                    initialDate = new DateTimeOffset(DateTime.SpecifyKind(cellDate.Date, DateTimeKind.Unspecified), TimeSpan.Zero);
                }
                else if (!string.IsNullOrWhiteSpace(dateText))
                {
                    string[] formats =
                    [
                        "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy HH:mm", "dd.MM.yyyy",
                        "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "dd/MM/yyyy",
                        "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd",
                    ];

                    if (DateTime.TryParseExact(dateText, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate) ||
                        DateTime.TryParse(dateText, CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.None, out parsedDate))
                    {
                        initialDate = new DateTimeOffset(parsedDate, TimeSpan.Zero);
                    }
                    else
                    {
                        errors.Add($"Satır {rowNumber}: Başlangıç Tarihi geçerli bir tarih olmalıdır (girilen: \"{dateText}\").");
                        continue;
                    }
                }
            }

            decimal? squareMeters = null;
            string? ownerName = null;

            // Etap 6.7: opsiyonel m² sütunu (SCD Type 2 dimension güncellemesi için).
            if (squareMetersCol is not null)
            {
                var m2Cell = dataRow.Cell(squareMetersCol.Value);
                var m2Text = m2Cell.GetString().Trim();
                if (m2Cell.DataType == ClosedXML.Excel.XLDataType.Number && m2Cell.TryGetValue(out decimal m2Number))
                {
                    // Gerçek sayı hücresi: 47,4 → 47.4 (metne çevrilip tr-TR ile 474 okunmaz).
                    squareMeters = m2Number > 0m ? m2Number : null;
                }
                else if (!string.IsNullOrWhiteSpace(m2Text))
                {
                    if (decimal.TryParse(m2Text, NumberStyles.Any, CultureInfo.GetCultureInfo("tr-TR"), out var m2)
                        || decimal.TryParse(m2Text, NumberStyles.Any, CultureInfo.InvariantCulture, out m2))
                    {
                        squareMeters = m2 > 0m ? m2 : null;
                    }
                    else
                    {
                        errors.Add($"Satır {rowNumber}: m² sayısal olmalıdır (girilen: \"{m2Text}\").");
                        continue;
                    }
                }
            }

            // Etap 6.7: opsiyonel kat maliki adı soyadı (SCD Type 2 owner kaydı için).
            if (ownerCol is not null)
            {
                var ownerText = dataRow.Cell(ownerCol.Value).GetString().Trim();
                ownerName = string.IsNullOrWhiteSpace(ownerText) ? null : ownerText;
            }

            rows.Add(new ExcelMeterImportRow(
                doorNumber, serial, primaryAddress, initialIndex, initialDate, ownerName, squareMeters));

            if (rows.Count >= MaxRows)
            {
                errors.Add($"Tek seferde en fazla {MaxRows} satır içe aktarılabilir; kalan satırlar atlandı.");
                break;
            }
        }

        return new ExcelMeterImportResult(rows, errors);
    }

    private static string Normalize(string header)
    {
        // Türkçe karakterler eşlenir: Kapı No → kapino, Başlangıç Endeksi → baslangicendeksi
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
                '²' => '2', // "m²" → "m2" (Etap 6.7 başlık eşlemesi)
                '³' => '3',
                _ => ch
            });
        }

        var chars = mapped.ToString().Where(char.IsLetterOrDigit).ToArray();
        return new string(chars);
    }
}
