using System.Globalization;
using System.IO.Ports;
using Westalife.Application.Common.Enums;
using Westalife.Application.Common.Models;

namespace Westalife.MbusReader;

/// <summary>
/// Basit okuyucu penceresi: bağlantıyı seç → Oku → sonuçları gör → Excel'e Kaydet.
/// Tüm kontroller kodla kurulur (tasarımcı dosyası yok).
/// </summary>
public sealed class MainForm : Form
{
    private const int DefaultBaudRate = 2400;
    private const int DefaultTcpPort = 2000;
    private const int DefaultTimeoutMs = 700;

    private readonly RadioButton _usbRadio = new() { Text = "USB / Seri port", Checked = true, AutoSize = true };
    private readonly RadioButton _tcpRadio = new() { Text = "Ağ geçidi (TCP)", AutoSize = true };
    private readonly ComboBox _portBox = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 110 };
    private readonly Button _refreshPortsButton = new() { Text = "Yenile", AutoSize = true };
    private readonly NumericUpDown _baudBox = new() { Minimum = 300, Maximum = 115200, Value = DefaultBaudRate, Width = 80 };
    private readonly TextBox _hostBox = new() { Width = 140 };
    private readonly NumericUpDown _tcpPortBox = new() { Minimum = 1, Maximum = 65535, Value = DefaultTcpPort, Width = 80 };
    private readonly NumericUpDown _fromBox = new() { Minimum = MbusScanner.MinAddress, Maximum = MbusScanner.MaxAddress, Value = MbusScanner.MinAddress, Width = 60 };
    private readonly NumericUpDown _toBox = new() { Minimum = MbusScanner.MinAddress, Maximum = MbusScanner.MaxAddress, Value = MbusScanner.MaxAddress, Width = 60 };
    private readonly NumericUpDown _timeoutBox = new() { Minimum = 100, Maximum = 5000, Increment = 100, Value = DefaultTimeoutMs, Width = 70 };

    private readonly Button _testButton = new() { Text = "Bağlantıyı Test Et", Width = 150, Height = 34 };
    private readonly Button _readButton = new() { Text = "Oku", Width = 110, Height = 34 };
    private readonly Button _stopButton = new() { Text = "Durdur", Width = 90, Height = 34, Enabled = false };
    private readonly Button _saveButton = new() { Text = "Excel'e Kaydet", Width = 130, Height = 34, Enabled = false };
    private readonly ProgressBar _progress = new() { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100 };
    private readonly Label _status = new() { Text = "Hazır.", AutoSize = true };
    private readonly ListView _list = new()
    {
        Dock = DockStyle.Fill,
        View = View.Details,
        FullRowSelect = true,
        GridLines = true,
    };

    private readonly List<MeterReadResult> _results = [];
    private DateTime _readAtLocal;
    private CancellationTokenSource? _cts;

    public MainForm()
    {
        Text = "Sayaç Okuyucu (M-Bus → Excel)";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(820, 560);
        Size = new Size(900, 640);
        Font = new Font("Segoe UI", 9.5f);

        _list.Columns.Add("M-Bus Adresi", 100);
        _list.Columns.Add("Sayaç Seri No", 150);
        _list.Columns.Add("Endeks (kWh)", 120, HorizontalAlignment.Right);
        _list.Columns.Add("Hacim (m³)", 100, HorizontalAlignment.Right);
        _list.Columns.Add("Durum", 280);

        BuildLayout();
        RefreshPorts();

        _refreshPortsButton.Click += (_, _) => RefreshPorts();
        // Radyolar farklı panellerde olduğundan otomatik tekil seçim çalışmaz: karşılıklı dışlama elle yapılır.
        _usbRadio.CheckedChanged += (_, _) =>
        {
            if (_usbRadio.Checked)
            {
                _tcpRadio.Checked = false;
            }

            UpdateConnectionControls();
        };
        _tcpRadio.CheckedChanged += (_, _) =>
        {
            if (_tcpRadio.Checked)
            {
                _usbRadio.Checked = false;
            }

            UpdateConnectionControls();
        };
        _readButton.Click += async (_, _) => await ReadAsync();
        _testButton.Click += async (_, _) => await TestAsync();
        _stopButton.Click += (_, _) => _cts?.Cancel();
        _saveButton.Click += (_, _) => SaveExcel();
        FormClosing += (_, _) => _cts?.Cancel();

        UpdateConnectionControls();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // --- Bağlantı ayarları ---
        var connection = new GroupBox { Text = "Bağlantı", Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8) };
        var grid = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };

        var usbRow = Row(_usbRadio, Caption("Port:"), _portBox, _refreshPortsButton, Caption("Hız:"), _baudBox);
        var tcpRow = Row(_tcpRadio, Caption("Adres:"), _hostBox, Caption("Port:"), _tcpPortBox);
        var scanRow = Row(
            Caption("Taranacak M-Bus adresleri:"), _fromBox, Caption("–"), _toBox,
            Caption("Yanıt bekleme (ms):"), _timeoutBox);

        grid.Controls.AddRange([usbRow, tcpRow, scanRow]);
        connection.Controls.Add(grid);

        // --- Düğmeler ---
        var buttons = Row(_testButton, _readButton, _stopButton, _saveButton);
        buttons.Margin = new Padding(0, 8, 0, 8);

        var statusPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        statusPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        statusPanel.Controls.Add(_progress, 0, 0);
        statusPanel.Controls.Add(_status, 1, 0);
        _status.Margin = new Padding(10, 4, 0, 0);

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        top.Controls.Add(connection);
        top.Controls.Add(buttons);

        root.Controls.Add(top, 0, 0);
        root.Controls.Add(new Label { Text = string.Empty, Height = 1 }, 0, 1);
        root.Controls.Add(statusPanel, 0, 2);
        root.Controls.Add(_list, 0, 3);
        Controls.Add(root);
    }

    private static Label Caption(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(8, 6, 4, 0) };

    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        panel.Controls.AddRange(controls);
        return panel;
    }

    private void RefreshPorts()
    {
        var selected = _portBox.Text;
        _portBox.Items.Clear();
        _portBox.Items.AddRange(SerialPort.GetPortNames().OrderBy(p => p, StringComparer.OrdinalIgnoreCase).Cast<object>().ToArray());
        _portBox.Text = !string.IsNullOrWhiteSpace(selected)
            ? selected
            : _portBox.Items.Count > 0 ? _portBox.Items[0]!.ToString() : string.Empty;
    }

    private void UpdateConnectionControls()
    {
        var usb = _usbRadio.Checked;
        _portBox.Enabled = _refreshPortsButton.Enabled = _baudBox.Enabled = usb;
        _hostBox.Enabled = _tcpPortBox.Enabled = !usb;
    }

    private void SetBusy(bool busy)
    {
        _readButton.Enabled = !busy;
        _stopButton.Enabled = busy;
        _testButton.Enabled = !busy;
        _saveButton.Enabled = !busy && _results.Any(r => r.IsSuccess);
        foreach (var control in new Control[] { _usbRadio, _tcpRadio, _portBox, _refreshPortsButton, _baudBox, _hostBox, _tcpPortBox, _fromBox, _toBox, _timeoutBox })
        {
            control.Enabled = !busy;
        }

        if (!busy)
        {
            UpdateConnectionControls();
        }
    }

    /// <summary>Arayüzdeki ayarları doğrular; hatalıysa uyarı gösterip null döner.</summary>
    private ScanOptions? BuildScanOrWarn()
    {
        var from = (int)_fromBox.Value;
        var to = (int)_toBox.Value;
        if (from > to)
        {
            MessageBox.Show(this, "Başlangıç adresi bitiş adresinden büyük olamaz.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        var usb = _usbRadio.Checked;
        if (usb && string.IsNullOrWhiteSpace(_portBox.Text))
        {
            MessageBox.Show(this, "Seri port seçin (örn. COM3). Port görünmüyorsa M-Bus dönüştürücüyü takıp Yenile'ye basın.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        if (!usb && string.IsNullOrWhiteSpace(_hostBox.Text))
        {
            MessageBox.Show(this, "Ağ geçidinin IP adresini girin.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        return new ScanOptions(
            usb ? MeterReaderConnectionType.Usb : MeterReaderConnectionType.Tcp,
            _portBox.Text.Trim(),
            (int)_baudBox.Value,
            _hostBox.Text.Trim(),
            (int)_tcpPortBox.Value,
            from,
            to,
            (int)_timeoutBox.Value);
    }

    /// <summary>"Bağlantıyı Test Et": port açılabiliyor / geçide ulaşılabiliyor mu — tarama yapmadan.</summary>
    private async Task TestAsync()
    {
        var scan = BuildScanOrWarn();
        if (scan is null)
        {
            return;
        }

        SetBusy(true);
        _stopButton.Enabled = false;
        _status.Text = "Bağlantı test ediliyor...";
        try
        {
            var (ok, message) = await Task.Run(() => MbusScanner.TestConnectionAsync(scan, CancellationToken.None));
            _status.Text = ok ? "Bağlantı başarılı." : "Bağlantı kurulamadı.";
            MessageBox.Show(
                this,
                ok ? $"{message}\n\nNot: Sayaçların cevabı ancak 'Oku' ile tarama sırasında anlaşılır." : message,
                Text, MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ReadAsync()
    {
        var scan = BuildScanOrWarn();
        if (scan is null)
        {
            return;
        }

        _results.Clear();
        _list.Items.Clear();
        _progress.Value = 0;
        _readAtLocal = DateTime.Now;
        _cts = new CancellationTokenSource();
        SetBusy(true);

        // Tarama dakikalar sürebilir: önce bağlantıyı test et, sorun varsa kullanıcı boşuna beklemesin.
        _status.Text = "Bağlantı test ediliyor...";
        var (connected, connectMessage) = await Task.Run(() => MbusScanner.TestConnectionAsync(scan, _cts.Token));
        if (!connected)
        {
            _cts.Dispose();
            _cts = null;
            SetBusy(false);
            _status.Text = "Bağlantı kurulamadı.";
            MessageBox.Show(this, $"Bağlantı kurulamadı:\n\n{connectMessage}", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _status.Text = "Bağlanıldı, taranıyor...";

        // Senkron aktarım (Invoke): Report döndüğünde arayüz güncellenmiş olur; sonuç listesi tutarlı kalır.
        var progress = new SyncProgress<ScanProgress>(this, p =>
        {
            _progress.Value = p.Total == 0 ? 0 : Math.Min(100, p.Done * 100 / p.Total);
            _status.Text = $"Adres {p.Address} taranıyor ({p.Done}/{p.Total}) — bulunan: {p.Found}";
        });
        var found = new SyncProgress<MeterReadResult>(this, AddRow);

        try
        {
            await Task.Run(() => new MbusScanner().ScanAsync(scan, progress, found, _cts.Token));
            _status.Text = $"Tamamlandı: {_results.Count(r => r.IsSuccess)} sayaç okundu, {_results.Count(r => !r.IsSuccess)} hatalı.";
            if (_results.Count == 0)
            {
                MessageBox.Show(
                    this,
                    "Hiçbir sayaç cevap vermedi.\n\nKontrol edin:\n• M-Bus hattı beslemesi/kablolar\n• Hız (genelde 2400)\n• Taranan adres aralığı\n• 'Yanıt bekleme' süresi (yavaş hatta 1500 ms deneyin)",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (OperationCanceledException)
        {
            _status.Text = $"Durduruldu. Şimdiye kadar {_results.Count} kayıt (kaydedilebilir).";
        }
        catch (Exception ex)
        {
            _status.Text = "Hata oluştu.";
            MessageBox.Show(this, $"Okuma başarısız:\n\n{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
            SetBusy(false);
        }

        if (_results.Any(r => r.IsSuccess))
        {
            _progress.Value = 100;
            SaveExcel();
        }
    }

    private void AddRow(MeterReadResult result)
    {
        _results.Add(result);

        var snapshot = result.Snapshot;
        var item = new ListViewItem(result.PrimaryAddress.ToString(CultureInfo.InvariantCulture));
        item.SubItems.Add(snapshot?.SerialNumber ?? result.SerialNumber ?? string.Empty);
        item.SubItems.Add(snapshot is null ? string.Empty : snapshot.CumulativeEnergyKWh.ToString("N3", CultureInfo.GetCultureInfo("tr-TR")));
        item.SubItems.Add(snapshot is null ? string.Empty : snapshot.CumulativeVolumeM3.ToString("N3", CultureInfo.GetCultureInfo("tr-TR")));
        item.SubItems.Add(result.IsSuccess ? "Okundu" : $"Hata: {result.ErrorMessage}");
        if (!result.IsSuccess)
        {
            item.ForeColor = Color.Firebrick;
        }

        _list.Items.Add(item);
        item.EnsureVisible();
    }

    private void SaveExcel()
    {
        if (!_results.Any(r => r.IsSuccess))
        {
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Okumaları Excel'e kaydet",
            Filter = "Excel dosyası (*.xlsx)|*.xlsx",
            FileName = $"Okuma_{_readAtLocal:yyyyMMdd_HHmm}.xlsx",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            _saveButton.Enabled = true;
            return;
        }

        try
        {
            ReadingExcelWriter.Save(dialog.FileName, _results, _readAtLocal);
            _status.Text = $"Kaydedildi: {dialog.FileName}";
            _saveButton.Enabled = true;
            MessageBox.Show(
                this,
                $"{_results.Count(r => r.IsSuccess)} sayaç okuması kaydedildi.\n\n{dialog.FileName}\n\nBu dosyayı flash diskle ana bilgisayara götürüp 'Okumalar → Excel'den içe aktar' ile yükleyin.",
                Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (IOException ex)
        {
            MessageBox.Show(this, $"Dosya yazılamadı (açık olabilir):\n\n{ex.Message}", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
