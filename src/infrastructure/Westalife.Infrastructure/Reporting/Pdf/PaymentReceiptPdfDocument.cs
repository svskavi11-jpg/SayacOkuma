using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Westalife.Application.Common.Interfaces;

namespace Westalife.Infrastructure.Reporting.Pdf;

public sealed class PaymentReceiptPdfDocument : IPaymentReceiptPdf
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public byte[] Generate(PaymentReceiptDocument data)
    {
        PdfFonts.Ensure();
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(style => PdfFonts.WithFallback(style).FontSize(11));

                if (data.IsCancelled)
                {
                    page.Foreground()
                        .AlignCenter()
                        .AlignMiddle()
                        .Text("İPTAL")
                        .FontSize(72)
                        .FontColor(Colors.Red.Lighten2)
                        .Bold();
                }

                page.Header().Column(column =>
                {
                    if (data.Logo is { Length: > 0 })
                    {
                        column.Item().MaxHeight(data.LogoHeightMm, Unit.Millimetre).Image(data.Logo).FitArea();
                    }

                    column.Item().PaddingTop(4).Text(data.BuildingName).Bold().FontSize(16);
                    column.Item().Text("Tahsilat Makbuzu").FontSize(13).FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingTop(16).Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text(text =>
                        {
                            text.Span("Makbuz No: ").FontColor(Colors.Grey.Darken1);
                            text.Span(data.ReceiptNumber).Bold();
                        });
                        row.RelativeItem().AlignRight().Text(text =>
                        {
                            text.Span("Tarih: ").FontColor(Colors.Grey.Darken1);
                            text.Span(data.PaymentDate.ToString("dd.MM.yyyy", Tr)).Bold();
                        });
                    });

                    column.Item().PaddingTop(14).Text(text =>
                    {
                        text.Span("Daire: ").FontColor(Colors.Grey.Darken1);
                        text.Span(data.DoorNumber).Bold();
                    });
                    column.Item().PaddingTop(4).Text(text =>
                    {
                        text.Span("Ödeyen: ").FontColor(Colors.Grey.Darken1);
                        text.Span(data.PayerName).Bold();
                    });

                    column.Item().PaddingTop(18).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(12)
                        .AlignCenter()
                        .Text(Money(data.Amount))
                        .Bold()
                        .FontSize(22);

                    column.Item().PaddingTop(16).Text("Borca sayılan").Bold();
                    column.Item().PaddingTop(6).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(4);
                            columns.RelativeColumn(2);
                        });
                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text("Kalem").Bold();
                            header.Cell().Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Tutar").Bold();
                        });
                        foreach (var line in data.Lines)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(line.Description);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignRight().Text(Money(line.Amount));
                        }
                    });

                    column.Item().PaddingTop(18).Text(text =>
                    {
                        text.Span("İşlemi yapan: ").FontColor(Colors.Grey.Darken1);
                        text.Span(data.PerformedBy);
                    });
                });

                page.Footer().AlignCenter().Text("Bu belge sistem tarafından üretilmiş tahsilat makbuzudur.").FontSize(8).FontColor(Colors.Grey.Medium);
            });
        }).GeneratePdf();
    }

    private static string Money(decimal amount) =>
        amount.ToString("N2", Tr) + " TL";
}
