using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Westalife.Infrastructure.Reporting.Pdf;

/// <summary>Danfoss formu ortak hücre/başlık stilleri.</summary>
internal static class ReportComposeExtensions
{
    /// <summary>Referans formdaki koyu gri bölüm başlık şeridi.</summary>
    public static IContainer SectionHeader(this IContainer container, string title)
    {
        container.Background("#9E9E9E").Padding(4).Text(t => t.Span(title).Bold().FontSize(9.5f));
        return container;
    }

    public static IContainer HeadCell(this IContainer cell) =>
        cell.Background("#F2F2F2").Border(1).BorderColor("#AAAAAA").Padding(3);
}
