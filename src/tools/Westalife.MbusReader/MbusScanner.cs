using Westalife.Application.Common.Enums;
using Westalife.Application.Common.Models;
using Westalife.Infrastructure.Metering.Connections;
using Westalife.Infrastructure.Metering.Exceptions;
using Westalife.Infrastructure.Metering.Protocol;

namespace Westalife.MbusReader;

/// <summary>Tarama ayarları (arayüzden gelir).</summary>
public sealed record ScanOptions(
    MeterReaderConnectionType ConnectionType,
    string PortName,
    int BaudRate,
    string Host,
    int TcpPort,
    int FromAddress,
    int ToAddress,
    int TimeoutMs);

/// <summary>İlerleme bildirimi: taranan adres, biten/toplam adres, bulunan sayaç sayısı.</summary>
public sealed record ScanProgress(int Address, int Done, int Total, int Found);

/// <summary>
/// M-Bus birincil adres taraması: aralıktaki her adrese SND_NKE gönderir; ACK veren adres bir sayaçtır,
/// ardından REQ_UD2 ile veri alınır. Aynı protokol akışı API'deki MbusMeterReader ile birebirdir
/// (SND_NKE → REQ_UD2 → RSP_UD); fark: adres listesi yerine aralık taranır ve cevap vermeyen adres hata sayılmaz.
/// </summary>
public sealed class MbusScanner
{
    public const int MinAddress = 1;
    public const int MaxAddress = 250;

    private const int ConnectTimeoutMs = 4000;

    private static MeteringOptions BuildOptions(ScanOptions scan) => new()
    {
        ConnectionType = scan.ConnectionType,
        ResponseTimeoutMs = scan.TimeoutMs,
        MaxRetryCount = 0,
        Usb = new MeteringOptions.UsbOptions { PortName = scan.PortName, BaudRate = scan.BaudRate },
        Tcp = new MeteringOptions.TcpOptions { Host = scan.Host, Port = scan.TcpPort },
    };

    /// <summary>
    /// Taramadan ÖNCE hızlı bağlantı testi: seri port gerçekten var mı / açılabiliyor mu, TCP geçidine ulaşılabiliyor mu.
    /// Hata varsa kullanıcı dakikalarca tarama beklemeden anında ve anlaşılır bir mesaj görür.
    /// </summary>
    public static async Task<(bool Ok, string Message)> TestConnectionAsync(ScanOptions scan, CancellationToken cancellationToken)
    {
        if (scan.ConnectionType == MeterReaderConnectionType.Usb)
        {
            var ports = System.IO.Ports.SerialPort.GetPortNames();
            if (!ports.Contains(scan.PortName, StringComparer.OrdinalIgnoreCase))
            {
                var available = ports.Length == 0 ? "hiç seri port yok (M-Bus dönüştürücü takılı mı?)" : string.Join(", ", ports);
                return (false, $"{scan.PortName} portu bulunamadı. Mevcut portlar: {available}.");
            }
        }

        try
        {
            using var channel = MbusChannelFactory.Create(BuildOptions(scan));
            var open = channel.OpenAsync(cancellationToken);
            if (await Task.WhenAny(open, Task.Delay(ConnectTimeoutMs, cancellationToken)) != open)
            {
                return (false, $"Bağlantı {ConnectTimeoutMs / 1000} saniyede kurulamadı. Adresi/portu ve ağ bağlantısını kontrol edin.");
            }

            await open; // açma hatasını yüzeye çıkar
            return (true, scan.ConnectionType == MeterReaderConnectionType.Usb
                ? $"{scan.PortName} portu açıldı ({scan.BaudRate} baud)."
                : $"Geçide bağlanıldı ({scan.Host}:{scan.TcpPort}).");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (false, ex.Message);
        }
    }


    public async Task<IReadOnlyList<MeterReadResult>> ScanAsync(
        ScanOptions scan,
        IProgress<ScanProgress> progress,
        IProgress<MeterReadResult> onFound,
        CancellationToken cancellationToken)
    {
        var options = BuildOptions(scan);

        var results = new List<MeterReadResult>();
        var total = scan.ToAddress - scan.FromAddress + 1;
        var done = 0;

        using var channel = MbusChannelFactory.Create(options);
        await channel.OpenAsync(cancellationToken);

        for (var address = scan.FromAddress; address <= scan.ToAddress; address++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await ProbeAsync(channel, (byte)address, scan.TimeoutMs, cancellationToken);
            if (result is not null)
            {
                results.Add(result);
                onFound.Report(result);
            }

            done++;
            progress.Report(new ScanProgress(address, done, total, results.Count));
        }

        return results;
    }

    /// <summary>Adres cevap vermezse null (sayaç yok); cevap verip okunamazsa hata sonucu döner.</summary>
    private static async Task<MeterReadResult?> ProbeAsync(
        IMbusChannel channel, byte address, int timeoutMs, CancellationToken cancellationToken)
    {
        var acknowledged = false;
        try
        {
            await channel.SendAsync(MbusFrameBuilder.BuildSndNke(address), cancellationToken);
            var ack = await channel.ReceiveAsync(timeoutMs, cancellationToken);
            if (!MbusFrameParser.IsAck(ack))
            {
                return null; // hat gürültüsü / sayaç değil
            }

            acknowledged = true;

            await channel.SendAsync(MbusFrameBuilder.BuildReqUd2(address, frameCountBitSet: true), cancellationToken);
            var response = await channel.ReceiveAsync(timeoutMs, cancellationToken);
            var frame = MbusFrameParser.ParseResponse(response);
            var records = MbusDataRecordParser.Parse(frame.Data);

            var snapshot = DanfossRecordMapper.MapToSnapshot(records, frame.RawHex, DateTimeOffset.UtcNow)
                with { PrimaryAddress = address };

            return snapshot.HasDataError
                ? MeterReadResult.Failure(
                    snapshot.SerialNumber, address, MeterReadErrorCode.ProtocolError, "Cihaz veri hatası bildirdi.")
                : MeterReadResult.Success(snapshot);
        }
        catch (MbusTimeoutException ex)
        {
            // ACK öncesi zaman aşımı = o adreste sayaç yok. ACK sonrası = sayaç var ama veri vermedi.
            return acknowledged
                ? MeterReadResult.Failure(null, address, MeterReadErrorCode.Timeout, ex.Message)
                : null;
        }
        catch (MbusProtocolException ex)
        {
            return MeterReadResult.Failure(null, address, MeterReadErrorCode.ProtocolError, ex.Message);
        }
    }
}
