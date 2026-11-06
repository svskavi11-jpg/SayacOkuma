using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Westalife.Application.Features.Budget.Queries.GetIncomeExpenseReport;
using Westalife.Domain.Constants;

namespace Westalife.Infrastructure.Reporting.Pdf;

/// <summary>Yıllık gelir-gider raporu. Gelir = iptal edilmemiş tahsilat, gider = Expense.</summary>
public static class IncomeExpensePdfDocument
{
    private const string FontFamily = "Lato";
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    static IncomeExpensePdfDocument()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(IncomeExpenseReport data) =>
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontFamily(FontFamily).FontSize(9));

                page.Header().Column(col =>
                {
                    col.Item().Text(data.BuildingName).Bold().FontSize(14);
                    col.Item().Text($"{data.Year} Gelir-Gider Raporu").Bold().FontSize(12);
                    col.Item().PaddingTop(6).Text(
                        $"Gelir: {Money(data.TotalIncome)}    Gider: {Money(data.TotalExpense)}    Net: {Money(data.Net)}");
                    col.Item().Text(
                        $"Tahmini Gelir: {Money(data.EstimatedIncome)}    Tahmini Gider: {Money(data.EstimatedExpense)}    Denge: {Money(data.EstimatedBalance)}");
                });

                page.Content().PaddingTop(12).Column(col =>
                {
                    col.Item().Text("Aylık özet").Bold().FontSize(11);
                    col.Item().PaddingTop(4).Element(c => MonthTable(c, data));
                    col.Item().PaddingTop(14).Text("Hareketler").Bold().FontSize(11);
                    col.Item().PaddingTop(4).Element(c => LineTable(c, data));
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

    private static void MonthTable(IContainer container, IncomeExpenseReport data)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2);
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            table.Header(header =>
            {
                HeaderCell(header.Cell(), "Ay");
                HeaderCell(header.Cell(), "Gelir");
                HeaderCell(header.Cell(), "Gider");
                HeaderCell(header.Cell(), "Net");
            });

            foreach (var month in data.Months)
            {
                BodyCell(table.Cell(), Tr.DateTimeFormat.GetMonthName(month.Month));
                BodyCell(table.Cell(), Money(month.Income));
                BodyCell(table.Cell(), Money(month.Expense));
                BodyCell(table.Cell(), Money(month.Net));
            }
        });
    }

    private static void LineTable(IContainer container, IncomeExpenseReport data)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(68);
                columns.RelativeColumn(2);
                columns.RelativeColumn(3);
                columns.ConstantColumn(78);
                columns.ConstantColumn(78);
            });

            table.Header(header =>
            {
                HeaderCell(header.Cell(), "Tarih");
                HeaderCell(header.Cell(), "Başlık");
                HeaderCell(header.Cell(), "Açıklama");
                HeaderCell(header.Cell(), DomainConstants.Budget.IncomeLabel);
                HeaderCell(header.Cell(), DomainConstants.Budget.ExpenseLabel);
            });

            if (data.Lines.Count == 0)
            {
                table.Cell().ColumnSpan(5).Padding(4).Text("Bu yıl için hareket yok.");
                return;
            }

            foreach (var line in data.Lines)
            {
                var income = line.Kind == DomainConstants.Budget.IncomeLabel;
                BodyCell(table.Cell(), line.Date.ToString("dd.MM.yyyy", Tr));
                BodyCell(table.Cell(), line.Title);
                BodyCell(table.Cell(), line.Description ?? "—");
                MoneyCell(table.Cell(), income ? Money(line.Amount) : "—");
                MoneyCell(table.Cell(), income ? "—" : Money(line.Amount));
            }

            TotalCell(table.Cell().ColumnSpan(3), DomainConstants.Budget.TotalLabel);
            TotalMoneyCell(table.Cell(), Money(data.TotalIncome));
            TotalMoneyCell(table.Cell(), Money(data.TotalExpense));
        });
    }

    private static void HeaderCell(IContainer cell, string text) =>
        cell.Background("#F2F2F2").Border(0.5f).BorderColor("#AAAAAA").Padding(3).Text(text).Bold();

    private static void BodyCell(IContainer cell, string text) =>
        cell.BorderBottom(0.4f).BorderColor("#DDDDDD").Padding(3).Text(text);

    private static void MoneyCell(IContainer cell, string text) =>
        cell.BorderBottom(0.4f).BorderColor("#DDDDDD").Padding(3).AlignRight().Text(text);

    private static void TotalCell(IContainer cell, string text) =>
        cell.BorderTop(0.8f).BorderColor("#666666").Padding(3).Text(text).Bold();

    private static void TotalMoneyCell(IContainer cell, string text) =>
        cell.BorderTop(0.8f).BorderColor("#666666").Padding(3).AlignRight().Text(text).Bold();

    private static string Money(decimal value) =>
        value.ToString("N2", Tr) + " " + "TL";
}
