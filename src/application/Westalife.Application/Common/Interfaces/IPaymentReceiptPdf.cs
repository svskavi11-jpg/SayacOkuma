namespace Westalife.Application.Common.Interfaces;

public sealed record PaymentReceiptLine(string Description, decimal Amount);

public sealed record PaymentReceiptDocument(
    string BuildingName,
    byte[]? Logo,
    int LogoHeightMm,
    string ReceiptNumber,
    DateOnly PaymentDate,
    string DoorNumber,
    string PayerName,
    decimal Amount,
    IReadOnlyList<PaymentReceiptLine> Lines,
    string PerformedBy,
    bool IsCancelled);

public interface IPaymentReceiptPdf
{
    byte[] Generate(PaymentReceiptDocument document);
}
