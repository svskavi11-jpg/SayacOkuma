using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Westalife.Infrastructure.Reporting.Pdf;

/// <summary>
/// Lato Türkçe metni basar. Onay işareti gibi Lato'da olmayan karakterler
/// gömülü Noto Sans Symbols 2 yedeğine düşer. Docker'da sistem fontu yoktur.
/// </summary>
internal static class PdfFonts
{
    public const string Primary = "Lato";
    public const string Symbols = "Noto Sans Symbols2";

    private static readonly object Gate = new();
    private static bool _ready;

    public static void Ensure()
    {
        if (_ready)
        {
            return;
        }

        lock (Gate)
        {
            if (_ready)
            {
                return;
            }

            QuestPDF.Settings.License = LicenseType.Community;
            var assembly = typeof(PdfFonts).Assembly;
            const string suffix = "NotoSansSymbols2-Regular.ttf";
            var name = assembly.GetManifestResourceNames().Single(resource =>
                resource.EndsWith(suffix, StringComparison.Ordinal));
            using var stream = assembly.GetManifestResourceStream(name)
                ?? throw new InvalidOperationException("Sembol yazı tipi derlemeye gömülü değil.");
            FontManager.RegisterFontFromStream(stream);
            _ready = true;
        }
    }

    public static TextStyle WithFallback(TextStyle style)
    {
        Ensure();
        return style.FontFamily(Primary, Symbols);
    }
}
