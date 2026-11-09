namespace Westalife.Application.Features.Payments.Commands.ParseBankStatement;

/// <summary>
/// Banka ekstresi (Excel / CSV) dosyasından ham olarak ayrıştırılmış tek satır.
/// </summary>
public sealed record BankStatementRawRow(
    int RowNumber,
    DateOnly? PaymentDate,
    string? RawDateText,
    decimal? Amount,
    string? RawAmountText,
    string Description,
    string? DocumentNumber,
    string? ParseError = null);

/// <summary>
/// Banka ekstresi dosya ayrıştırma sonucu.
/// </summary>
public sealed record BankStatementFileParseResult(
    IReadOnlyList<BankStatementRawRow> Rows,
    IReadOnlyList<string> FileErrors);

/// <summary>
/// Garanti, TEB ve genel banka ekstrelerini (Excel .xlsx/.xls ve .csv) okuyan ayrıştırıcı soyutlaması.
/// </summary>
public interface IBankStatementParser
{
    BankStatementFileParseResult Parse(Stream stream, string fileName);
}

