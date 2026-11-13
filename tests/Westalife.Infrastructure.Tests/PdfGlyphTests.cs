using QuestPDF.Fluent;
using QuestPDF.Helpers;
using Westalife.Infrastructure.Reporting.Pdf;

namespace Westalife.Infrastructure.Tests;

public class PdfGlyphTests
{
    [Fact]
    public void Bold_checkmark_and_dashes_render_without_throwing()
    {
        PdfFonts.Ensure();
        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A6);
                page.Margin(16);
                page.DefaultTextStyle(style => PdfFonts.WithFallback(style).FontSize(11));
                page.Content().Text("Mutabakat: KURUŞU KURUŞUNA MUTABIK ✔ — –").Bold();
            });
        }).GeneratePdf();

        Assert.NotEmpty(pdf);
    }
}
