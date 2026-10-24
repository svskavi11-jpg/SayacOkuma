using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Westalife.Infrastructure.Reporting.Pdf;

/// <summary>Danfoss formu ortak hücre/başlık stilleri.</summary>
internal static class ReportComposeExtensions
{
    public static IContainer SectionHeader(this IContainer container, string title)
    {
        container.Background("#E4E4E4").Padding(4).Text(t => t.Span(title).Bold().FontSize(9.5f));
        return container;
    }

    public static IContainer HeadCell(this IContainer cell) =>
        cell.Background("#F2F2F2").Border(1).BorderColor("#AAAAAA").Padding(3);
}
