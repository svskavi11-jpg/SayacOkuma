using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Westalife.Application.Features.Budget.Queries.GetBudgetRealization;

namespace Westalife.Infrastructure.Reporting.Pdf;

/// <summary>Bütçe gerçekleşme raporu. Kalan = tahmini − gerçekleşen.</summary>
public static class BudgetRealizationPdfDocument
{
    private const string FontFamily = "Lato";
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    static BudgetRealizationPdfDocument()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(BudgetRealizationReport data) =>
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontFamily(FontFamily).FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text(data.BuildingName).Bold().FontSize(14);
                    col.Item().Text($"{data.Year} Bütçe Gerçekleşme Raporu").Bold().FontSize(12);
                    col.Item().PaddingTop(6).Text(
                        $"Tahmini: {Money(data.TotalEstimated)}    Gerçekleşen: {Money(data.TotalActual)}    Kalan: {Money(data.TotalRemaining)}    Oran: {Percent(data.UtilizationPercent)}");
                    col.Item().Text(
                        $"Tahmini Gelir: {Money(data.TotalEstimatedIncome)}    Tahmini Gider: {Money(data.TotalEstimated)}    Denge: {Money(data.Balance)}");
                });

                page.Content().PaddingTop(12).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.ConstantColumn(70);
                    });

                    table.Header(header =>
                    {
                        HeaderCell(header.Cell(), "Kategori");
                        HeaderCell(header.Cell(), "Tahmini");
                        HeaderCell(header.Cell(), "Gerçekleşen");
                        HeaderCell(header.Cell(), "Kalan");
                        HeaderCell(header.Cell(), "%");
                    });

                    if (data.Rows.Count == 0)
                    {
                        table.Cell().ColumnSpan(5).Padding(4).Text("Bu yıl için bütçe kalemi yok.");
                    }

                    foreach (var row in data.Rows)
                    {
                        BodyCell(table.Cell(), row.Category);
                        BodyCell(table.Cell(), Money(row.EstimatedAmount));
                        BodyCell(table.Cell(), Money(row.ActualAmount));
                        BodyCell(table.Cell(), Money(row.RemainingAmount));
                        BodyCell(table.Cell(), Percent(row.UtilizationPercent));
                    }

                    HeaderCell(table.Cell(), "TOPLAM");
                    HeaderCell(table.Cell(), Money(data.TotalEstimated));
                    HeaderCell(table.Cell(), Money(data.TotalActual));
                    HeaderCell(table.Cell(), Money(data.TotalRemaining));
                    HeaderCell(table.Cell(), Percent(data.UtilizationPercent));
                });

                page.Footer().AlignRight().Text(t =>
                {
                    t.Span("Sayfa ");
                    t.CurrentPageNumber();
                    t.Span(" / ");
                    t.TotalPages();
                });
            });
        }).GeneratePdf();

    private static void HeaderCell(IContainer cell, string text) =>
        cell.Background("#F2F2F2").Border(0.5f).BorderColor("#AAAAAA").Padding(3).Text(text).Bold();

    private static void BodyCell(IContainer cell, string text) =>
        cell.BorderBottom(0.4f).BorderColor("#DDDDDD").Padding(3).Text(text);

    private static string Money(decimal value) => value.ToString("N2", Tr) + " TL";

    private static string Percent(decimal value) => "%" + value.ToString("N2", Tr);
}
