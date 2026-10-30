using System.Diagnostics;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Westalife.Application.Common.Models;
using Westalife.Application.Features.GasInvoices.Queries.GetNoticeReport;
using Westalife.Infrastructure.Reporting.Pdf;

namespace Westalife.PdfPreviewGui;

/// <summary>
/// PDF tasarım önizleme penceresi: PDF'i Edge (WebView2) görüntüleyicide
/// pencere İÇİNDE gösterir — zoom, sayfa gezinme, yazdırma dahil.
/// F5 → tasarım kodundaki değişiklikten sonra PDF'i yeniden üretip yükler.
/// </summary>
public sealed class PdfPreviewForm : Form
{
    private bool _showAll;
    private WebView2? _web;
    private readonly TextBox _status = new() { Dock = DockStyle.Bottom, BorderStyle = BorderStyle.FixedSingle, ReadOnly = true };
    private readonly Button _reload = new() { Text = "Yeniden Üret (F5)", AutoSize = true };
    private readonly Button _all = new() { AutoSize = true };
    private readonly Button _openFile = new() { Text = "PDF Dosyasını Aç", AutoSize = true };
    private string? _pdfPath;

    public PdfPreviewForm(bool showAll)
    {
        _showAll = showAll;
        Text = "Bireysel Gider Bildirimi — PDF Önizleme (QuestPDF tasarım)";
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(1000, 900);
        KeyPreview = true;

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.LeftToRight, Padding = new Padding(6) };
        top.Controls.AddRange(new Control[] { _reload, _all, _openFile });
        _reload.Click += async (_, _) => await RegenerateAsync();
        _all.Click += async (_, _) => { _showAll = !_showAll; UpdateAllButton(); await RegenerateAsync(); };
        _openFile.Click += (_, _) => { if (_pdfPath is not null) Process.Start(new ProcessStartInfo(_pdfPath) { UseShellExecute = true }); };

        Controls.Add(_webHostPanel());
        Controls.Add(_status);
        Controls.Add(top);
        UpdateAllButton();

        KeyDown += async (_, e) => { if (e.KeyCode == Keys.F5) await RegenerateAsync(); };
        Shown += async (_, _) => await RegenerateAsync();
    }

    private void UpdateAllButton() =>
        _all.Text = _showAll ? "Sadece Daire 1" : "Tüm Daireleri Göster";


    private Panel _webHostPanel()
    {
        var host = new Panel { Dock = DockStyle.Fill };
        host.HandleCreated += async (_, _) =>
        {
            try
            {
                var env = await CoreWebView2Environment.CreateAsync(
                    userDataFolder: Path.Combine(Path.GetTempPath(), "WestalifePdfPreview"));
                _web = new WebView2 { Dock = DockStyle.Fill };
                host.Controls.Add(_web);
                await _web.EnsureCoreWebView2Async(env);
                await RegenerateAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "WebView2 çalışma zamanı bulunamadı (Windows 10/11'de Edge ile birlikte gelir).\n" +
                    "Kurulum: https://developer.microsoft.com/microsoft-edge/webview2/\n\n" + ex.Message,
                    "PDF Önizleme", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        return host;
    }

    private async Task RegenerateAsync()
    {
        var (data, options) = SampleDataBuilder.Build();
        var pdf = _showAll
            ? IndividualNoticePdfDocument.GenerateAll(data, options)
            : IndividualNoticePdfDocument.GenerateSingle(data, options, data.Rows[0].ApartmentId);

        _pdfPath = Path.Combine(Path.GetTempPath(), "WestalifePdfPreview", "Bildirim-Onizleme.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(_pdfPath)!);
        await File.WriteAllBytesAsync(_pdfPath, pdf);

        _status.Text = $"✔ {DateTime.Now:HH:mm:ss} — {(_showAll ? $"{data.Rows.Count} daire (tümü)" : data.Rows[0].ApartmentTitle)} | {pdf.Length:N0} bayt | F5: yeniden üret";
        if (_web?.CoreWebView2 is not null)
            _web.CoreWebView2.Navigate(new Uri(_pdfPath).AbsoluteUri);
    }
}
