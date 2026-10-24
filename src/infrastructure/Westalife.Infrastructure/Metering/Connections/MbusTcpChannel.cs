using System.Net.Sockets;
using Westalife.Infrastructure.Metering.Exceptions;
using Westalife.Infrastructure.Metering.Protocol;

namespace Westalife.Infrastructure.Metering.Connections;

/// <summary>Izar Center Ethernet/TCP baÄŸlantÄ±sÄ± (RS232-TCP Ã§evirici dahil).</summary>
public sealed class MbusTcpChannel : IMbusChannel
{
    private readonly string _host;
    private readonly int _port;
    private readonly int _responseTimeoutMs;
    private TcpClient? _client;
    private NetworkStream? _stream;

    public MbusTcpChannel(string host, int port, int responseTimeoutMs)
    {
        _host = host;
        _port = port;
        _responseTimeoutMs = responseTimeoutMs;
    }

    public bool IsOpen => _client?.Connected ?? false;

    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _client = new TcpClient { NoDelay = true };
            _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_responseTimeoutMs);
            await _client.ConnectAsync(_host, _port, timeoutCts.Token);
            _stream = _client.GetStream();
        }
        catch (OperationCanceledException)
        {
            Dispose();
            throw new MbusConnectionException($"TCP baÄŸlantÄ±sÄ± zaman aÅŸÄ±mÄ±na uÄŸradÄ±: {_host}:{_port}");
        }
        catch (SocketException ex)
        {
            Dispose();
            throw new MbusConnectionException($"TCP baÄŸlantÄ±sÄ± kurulamadÄ±: {_host}:{_port}", ex);
        }
    }

    public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        var stream = RequireStream();
        try
        {
            // Onceki (gec gelen / yarim kalan) yanit baytlari bu istegin yanitina karismasin.
            DiscardPendingInput(stream);
            await stream.WriteAsync(data, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        catch (IOException ex)
        {
            throw new MbusConnectionException("TCP yazma hatasÄ±: baÄŸlantÄ± koptu.", ex);
        }
    }

    public async Task<byte[]> ReceiveAsync(int timeoutMilliseconds, CancellationToken cancellationToken = default)
    {
        var stream = RequireStream();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeoutMilliseconds);

        try
        {
            // Uzun frame: baÅŸlangÄ±Ã§ byte'Ä±nÄ± bekle, L'den toplam boyutu hesapla.
            var startByte = await ReadByteAsync(stream, timeoutCts.Token);

            if (startByte == MbusConstants.SingleCharAck)
            {
                return [MbusConstants.SingleCharAck];
            }

            if (startByte != MbusConstants.FrameStartLong && startByte != MbusConstants.FrameStartShort)
            {
                throw new MbusProtocolException(
                    $"Beklenmeyen baÅŸlangÄ±Ã§ baytÄ±: 0x{startByte:X2}");
            }

            var remaining = new List<byte> { (byte)startByte };

            if (startByte == MbusConstants.FrameStartShort)
            {
                remaining.Add((byte)await ReadByteAsync(stream, timeoutCts.Token)); // C
                remaining.Add((byte)await ReadByteAsync(stream, timeoutCts.Token)); // A
                remaining.Add((byte)await ReadByteAsync(stream, timeoutCts.Token)); // CS
                remaining.Add((byte)await ReadByteAsync(stream, timeoutCts.Token)); // stop
                return remaining.ToArray();
            }

            var length = await ReadByteAsync(stream, timeoutCts.Token);
            remaining.Add((byte)length);
            // Ikinci L baytini tekrar etme: telden gercekten oku (68 L L 68 ...); aksi halde stop baytı okunmadan kalir.
            remaining.Add((byte)await ReadByteAsync(stream, timeoutCts.Token));

            var totalFrameBytes = length + 6; // 68 L L 68 ... CS 16
            while (remaining.Count < totalFrameBytes)
            {
                remaining.Add((byte)await ReadByteAsync(stream, timeoutCts.Token));
            }

            return remaining.ToArray();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new MbusTimeoutException($"TCP yanÄ±tÄ± {timeoutMilliseconds} ms iÃ§inde gelmedi.");
        }
        catch (Exception ex) when (ex is IOException or EndOfStreamException or SocketException or ObjectDisposedException)
        {
            // EndOfStreamException: gateway baglantiyi karsi taraftan kapatti (ReadExactly IOException atmaz).
            throw new MbusConnectionException("TCP okuma hatasi: baglanti koptu.", ex);
        }
    }

    /// <summary>Soketteki okunmamis tum baytlari atar (senkron kaymayi onler).</summary>
    private static void DiscardPendingInput(NetworkStream stream)
    {
        var scratch = new byte[256];
        while (stream.DataAvailable)
        {
            if (stream.Read(scratch, 0, scratch.Length) <= 0)
            {
                break;
            }
        }
    }

    private static async Task<int> ReadByteAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[1];
        await stream.ReadExactlyAsync(buffer, cancellationToken);
        return buffer[0];
    }

    private NetworkStream RequireStream()
    {
        if (!IsOpen || _stream is null)
        {
            throw new MbusConnectionException("TCP kanalÄ± aÃ§Ä±k deÄŸil. Ã–nce OpenAsync Ã§aÄŸrÄ±lmalÄ±dÄ±r.");
        }

        return _stream;
    }

    public void Dispose()
    {
        _stream?.Dispose();
        _stream = null;
        _client?.Dispose();
        _client = null;
    }
}
