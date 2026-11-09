using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Westalife.Application.Features.Payments.Commands.ParseBankStatement;

namespace Westalife.Infrastructure.Reporting.Excel;

/// <summary>
/// Garanti BBVA, TEB ve genel banka ekstrelerini (.xlsx ve .csv) ayrıştıran servis.
/// Üstteki banka bilgi satırlarını otomatik atlar, başlık satırını dinamik bulur ve
/// yalnızca gelen (pozitif / alacak) tahsilat hareketlerini ayıklar.
/// </summary>
public sealed class BankStatementParser : IBankStatementParser
{
    private const int MaxRows = 1000;
    private const int MaxHeaderScanRows = 30;

    static BankStatementParser()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static readonly string[] DateAliases =
    [
        "tarih", "islemtarihi", "harekettarihi", "valortarihi", "valor", "kayittarihi", "date"
    ];

    private static readonly string[] DescriptionAliases =
    [
        "aciklama", "islemaciklamasi", "hareketaciklamasi", "detay", "izahat", "not", "description", "islem"
    ];

    private static readonly string[] AmountAliases =
    [
        "tutar", "islemtutari", "harekettutari", "net", "amount", "tl", "tutartl"
    ];

    private static readonly string[] CreditAliases =
    [
        "alacak", "giren", "alacaktutari", "yatirilan"
    ];

    private static readonly string[] DirectionAliases =
    [
        "borcalacak", "ba", "yon", "islemyonu", "tur"
    ];

    private static readonly string[] DocumentNumberAliases =
    [
        "dekontno", "dekontnumarasi", "dekont", "fisno", "referansno", "referans", "islemno", "belgeno", "sorguno"
    ];

    public BankStatementFileParseResult Parse(Stream stream, string fileName)
    {
        var ext = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        try
        {
            return ext == ".csv" || ext == ".txt"
                ? ParseCsv(stream)
                : ParseExcel(stream);
        }
        catch (Exception ex)
        {
            return new BankStatementFileParseResult(
                [],
                [$"Dosya okunamadı ({fileName}): {ex.Message}"]);
        }
    }

    private static BankStatementFileParseResult ParseExcel(Stream stream)
    {
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheets.FirstOrDefault();
        if (ws is null)
        {
            return new BankStatementFileParseResult([], ["Excel dosyasında çalışma sayfası bulunamadı."]);
        }

        var usedRows = ws.RowsUsed().ToList();
        if (usedRows.Count == 0)
        {
            return new BankStatementFileParseResult([], ["Excel dosyası boş görünüyor."]);
        }

        var maxCol = ws.LastColumnUsed()?.ColumnNumber() ?? 15;
        var tableRows = new List<(int RowNumber, IReadOnlyList<CellVal> Cells)>(usedRows.Count);

        foreach (var r in usedRows)
        {
            var rowCells = new List<CellVal>(maxCol);
            for (var c = 1; c <= maxCol; c++)
            {
                var cell = r.Cell(c);
                DateTime? dt = null;
                if (cell.DataType == XLDataType.DateTime && cell.TryGetValue(out DateTime dVal))
                {
                    dt = dVal;
                }

                decimal? num = null;
                if (cell.DataType == XLDataType.Number && cell.TryGetValue(out double nVal))
                {
                    num = Convert.ToDecimal(nVal, CultureInfo.InvariantCulture);
                }

                rowCells.Add(new CellVal(cell.GetString().Trim(), dt, num));
            }

            tableRows.Add((r.RowNumber(), rowCells));
        }

        return ParseTableRows(tableRows);
    }

    private static BankStatementFileParseResult ParseCsv(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var bytes = ms.ToArray();

        if (bytes.Length == 0)
        {
            return new BankStatementFileParseResult([], ["CSV dosyası boş görünüyor."]);
        }

        var text = DecodeCsvBytes(bytes);
        var lines = SplitCsvLines(text);
        if (lines.Count == 0)
        {
            return new BankStatementFileParseResult([], ["CSV dosyasında satır bulunamadı."]);
        }

        var delimiter = DetectCsvDelimiter(lines);
        var tableRows = new List<(int RowNumber, IReadOnlyList<CellVal> Cells)>(lines.Count);

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = ParseCsvLine(line, delimiter);
            var cells = fields.Select(f => new CellVal(f.Trim(), null, null)).ToList();
            tableRows.Add((i + 1, cells));
        }

        return ParseTableRows(tableRows);
    }

    private static BankStatementFileParseResult ParseTableRows(
        IReadOnlyList<(int RowNumber, IReadOnlyList<CellVal> Cells)> tableRows)
    {
        var errors = new List<string>();
        var resultRows = new List<BankStatementRawRow>();

        // İlk 30 satır içinde başlık satırını dinamik olarak bul
        var headerIndex = -1;
        ColumnMapping? mapping = null;

        var scanLimit = Math.Min(tableRows.Count, MaxHeaderScanRows);
        for (var i = 0; i < scanLimit; i++)
        {
            var candidate = TryBuildColumnMapping(tableRows[i].Cells);
            if (candidate is not null)
            {
                headerIndex = i;
                mapping = candidate;
                break;
            }
        }

        if (headerIndex < 0 || mapping is null)
        {
            errors.Add(
                "Ekstre başlık satırı bulunamadı. Dosyada en az \"Tarih\", \"Açıklama\" ve \"Tutar\" (veya \"Alacak\") sütunları yer almalıdır.");
            return new BankStatementFileParseResult(resultRows, errors);
        }

        for (var i = headerIndex + 1; i < tableRows.Count; i++)
        {
            var (rowNum, cells) = tableRows[i];
            var dateCell = GetCell(cells, mapping.DateCol);
            var descCell = GetCell(cells, mapping.DescriptionCol);
            var amountCell = mapping.AmountCol.HasValue ? GetCell(cells, mapping.AmountCol.Value) : default;
            var creditCell = mapping.CreditCol.HasValue ? GetCell(cells, mapping.CreditCol.Value) : default;
            var dirCell = mapping.DirectionCol.HasValue ? GetCell(cells, mapping.DirectionCol.Value) : default;
            var docCell = mapping.DocumentNumberCol.HasValue ? GetCell(cells, mapping.DocumentNumberCol.Value) : default;

            var dateText = dateCell.Text;
            var descText = descCell.Text;
            var rawAmountText = !string.IsNullOrWhiteSpace(creditCell.Text) ? creditCell.Text : amountCell.Text;
            var docText = string.IsNullOrWhiteSpace(docCell.Text) ? null : docCell.Text.Trim();

            // Tamamen boş satırları veya alt toplam / özet satırlarını atla
            if (string.IsNullOrWhiteSpace(dateText) &&
                string.IsNullOrWhiteSpace(descText) &&
                string.IsNullOrWhiteSpace(rawAmountText))
            {
                continue;
            }

            var normDesc = NormalizeHeader(descText);
            if (string.IsNullOrWhiteSpace(dateText) &&
                (normDesc.StartsWith("toplam", StringComparison.Ordinal) ||
                 normDesc.StartsWith("devir", StringComparison.Ordinal) ||
                 normDesc.StartsWith("bakiye", StringComparison.Ordinal)))
            {
                continue;
            }

            // Borç/Alacak yön sütunu varsa ve "B" / "BORÇ" / "ÇIKAN" ise giden ödemedir -> atla
            if (!string.IsNullOrWhiteSpace(dirCell.Text))
            {
                var normDir = NormalizeHeader(dirCell.Text);
                if (normDir is "b" or "borc" or "cikan" or "cikis" or "gider" or "odeme")
                {
                    continue;
                }
            }

            // Tutar ayrıştırma
            decimal? parsedAmount = null;
            var activeAmountCell = !string.IsNullOrWhiteSpace(creditCell.Text) || creditCell.NumericValue.HasValue
                ? creditCell
                : amountCell;

            if (activeAmountCell.NumericValue.HasValue)
            {
                parsedAmount = decimal.Round(activeAmountCell.NumericValue.Value, 2);
            }
            else if (TryParseSignedMoney(rawAmountText, out var money))
            {
                parsedAmount = decimal.Round(money, 2);
            }

            // Giden ödeme (negatif tutar) veya alacak sütunu 0 olan borç hareketlerini sessizce atla
            if (parsedAmount.HasValue && parsedAmount.Value < 0m)
            {
                continue;
            }

            if (mapping.CreditCol.HasValue && parsedAmount == 0m)
            {
                continue;
            }

            // Tarih ayrıştırma
            DateOnly? parsedDate = null;
            if (dateCell.DateValue.HasValue)
            {
                parsedDate = DateOnly.FromDateTime(dateCell.DateValue.Value);
            }
            else if (TryParseStatementDate(dateText, out var dt))
            {
                parsedDate = DateOnly.FromDateTime(dt);
            }

            string? parseError = null;
            if (parsedDate is null)
            {
                parseError = $"Satır {rowNum}: Tarih okunamadı (\"{dateText}\").";
            }
            else if (parsedAmount is null || parsedAmount.Value <= 0m)
            {
                parseError = $"Satır {rowNum}: Tutar geçerli bir pozitif sayı olmalıdır (\"{rawAmountText}\").";
            }

            resultRows.Add(new BankStatementRawRow(
                RowNumber: rowNum,
                PaymentDate: parsedDate,
                RawDateText: dateText,
                Amount: parsedAmount,
                RawAmountText: rawAmountText,
                Description: descText,
                DocumentNumber: docText,
                ParseError: parseError));

            if (resultRows.Count >= MaxRows)
            {
                errors.Add($"Tek seferde en fazla {MaxRows} hareket işlenebilir; sonraki satırlar atlandı.");
                break;
            }
        }

        return new BankStatementFileParseResult(resultRows, errors);
    }

    private static ColumnMapping? TryBuildColumnMapping(IReadOnlyList<CellVal> row)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < row.Count; i++)
        {
            var norm = NormalizeHeader(row[i].Text);
            if (!string.IsNullOrEmpty(norm) && !map.ContainsKey(norm))
            {
                map[norm] = i;
            }
        }

        int? FindCol(string[] aliases)
        {
            foreach (var alias in aliases)
            {
                if (map.TryGetValue(alias, out var idx))
                {
                    return idx;
                }
            }

            return null;
        }

        var dateCol = FindCol(DateAliases);
        var descCol = FindCol(DescriptionAliases);
        var amountCol = FindCol(AmountAliases);
        var creditCol = FindCol(CreditAliases);

        if (dateCol is null || descCol is null || (amountCol is null && creditCol is null))
        {
            return null;
        }

        var dirCol = FindCol(DirectionAliases);
        var docCol = FindCol(DocumentNumberAliases);

        return new ColumnMapping(
            DateCol: dateCol.Value,
            DescriptionCol: descCol.Value,
            AmountCol: amountCol,
            CreditCol: creditCol,
            DirectionCol: dirCol,
            DocumentNumberCol: docCol);
    }

    private static CellVal GetCell(IReadOnlyList<CellVal> cells, int index) =>
        index >= 0 && index < cells.Count ? cells[index] : new CellVal(string.Empty, null, null);

    private static bool TryParseStatementDate(string text, out DateTime value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var cleaned = text.Trim();
        string[] formats =
        [
            "dd.MM.yyyy HH:mm:ss", "dd.MM.yyyy HH:mm", "dd.MM.yyyy", "d.M.yyyy",
            "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "dd/MM/yyyy", "d/M/yyyy",
            "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd",
            "dd-MM-yyyy HH:mm:ss", "dd-MM-yyyy HH:mm", "dd-MM-yyyy"
        ];

        foreach (var fmt in formats)
        {
            if (DateTime.TryParseExact(cleaned, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
            {
                return true;
            }
        }

        return DateTime.TryParse(cleaned, CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.None, out value)
            || DateTime.TryParse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
    }

    public static bool TryParseSignedMoney(string? text, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var cleaned = text
            .Replace("\u00A0", string.Empty)
            .Replace("TL", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("TRY", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("₺", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty)
            .Trim();

        var isNegative = false;
        if (cleaned.StartsWith('(') && cleaned.EndsWith(')'))
        {
            isNegative = true;
            cleaned = cleaned[1..^1];
        }
        else if (cleaned.StartsWith('-'))
        {
            isNegative = true;
            cleaned = cleaned[1..];
        }
        else if (cleaned.EndsWith('-'))
        {
            isNegative = true;
            cleaned = cleaned[..^1];
        }
        else if (cleaned.StartsWith('+'))
        {
            cleaned = cleaned[1..];
        }

        var hasDot = cleaned.Contains('.');
        var hasComma = cleaned.Contains(',');

        if (hasDot && hasComma)
        {
            var decimalChar = cleaned.LastIndexOf('.') > cleaned.LastIndexOf(',') ? '.' : ',';
            var thousandsChar = decimalChar == '.' ? ',' : '.';
            cleaned = cleaned.Replace(thousandsChar.ToString(), string.Empty).Replace(decimalChar, '.');
        }
        else if (hasDot && cleaned.Count(c => c == '.') > 1)
        {
            cleaned = cleaned.Replace(".", string.Empty);
        }
        else if (hasComma && cleaned.Count(c => c == ',') > 1)
        {
            cleaned = cleaned.Replace(",", string.Empty);
        }
        else if (hasComma)
        {
            cleaned = cleaned.Replace(',', '.');
        }

        if (!decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        value = isNegative ? -parsed : parsed;
        return true;
    }

    private static string DecodeCsvBytes(byte[] bytes)
    {
        var utf8 = Encoding.UTF8.GetString(bytes);
        if (!utf8.Contains('\uFFFD'))
        {
            return utf8.TrimStart('\uFEFF');
        }

        return Encoding.GetEncoding(1254).GetString(bytes);
    }

    private static List<string> SplitCsvLines(string text)
    {
        var lines = new List<string>();
        using var reader = new StringReader(text);
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return lines;
    }

    private static char DetectCsvDelimiter(IReadOnlyList<string> lines)
    {
        var sample = lines.Take(10).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
        var semicolons = sample.Sum(l => l.Count(c => c == ';'));
        var commas = sample.Sum(l => l.Count(c => c == ','));
        var tabs = sample.Sum(l => l.Count(c => c == '\t'));

        if (tabs > semicolons && tabs > commas) return '\t';
        return semicolons >= commas ? ';' : ',';
    }

    private static List<string> ParseCsvLine(string line, char delimiter)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (ch == delimiter && !inQuotes)
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(ch);
            }
        }

        fields.Add(sb.ToString());
        return fields;
    }

    private static string NormalizeHeader(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(header.Length);
        foreach (var ch in header.ToLowerInvariant())
        {
            var mapped = ch switch
            {
                'ı' or 'i' or 'İ' => 'i',
                'ş' or 's' or 'Ş' => 's',
                'ğ' or 'g' or 'Ğ' => 'g',
                'ü' or 'u' or 'Ü' => 'u',
                'ö' or 'o' or 'Ö' => 'o',
                'ç' or 'c' or 'Ç' => 'c',
                _ => ch
            };

            if (char.IsLetterOrDigit(mapped))
            {
                sb.Append(mapped);
            }
        }

        return sb.ToString();
    }

    private readonly record struct CellVal(string Text, DateTime? DateValue, decimal? NumericValue);

    private sealed record ColumnMapping(
        int DateCol,
        int DescriptionCol,
        int? AmountCol,
        int? CreditCol,
        int? DirectionCol,
        int? DocumentNumberCol);
}

