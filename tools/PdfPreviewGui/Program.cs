using Westalife.PdfPreviewGui;

namespace Westalife.PdfPreviewGui;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var all = args.Contains("--tum");
        ApplicationConfiguration.Initialize();
        global::System.Windows.Forms.Application.Run(new PdfPreviewForm(showAll: all));
    }
}

