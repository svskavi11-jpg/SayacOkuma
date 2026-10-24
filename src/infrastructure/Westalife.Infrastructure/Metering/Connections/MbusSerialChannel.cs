using System.IO.Ports;
using Westalife.Infrastructure.Metering.Exceptions;
using Westalife.Infrastructure.Metering.Protocol;

namespace Westalife.Infrastructure.Metering.Connections;

/// <summary>USB-RS485 adaptÃ¶r Ã¼zerinden M-Bus baÄŸlantÄ±sÄ± (varsayÄ±lan: 2400 baud, 8E1).</summary>
public sealed class MbusSerialChannel : IMbusChannel
{
    private const int WriteTimeoutMs = 5000;

    private readonly string _portName;
    private readonly int _baudRate;
    private SerialPort? _port;

    public MbusSerialChannel(string portName, int baudRate)
    {
        _portName = portName;
        _baudRate = baudRate;
    }

    public bool IsOpen => _port?.IsOpen ?? false;

    public Task OpenAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _port = new SerialPort(_portName, _baudRate, Parity.Even, 8, StopBits.One);
            _port.ReadTimeout = 100; // okumalar ReceiveAsync iÃ§inde poll edilir
            _port.WriteTimeout = WriteTimeoutMs;
            _port.Open();
            return Task.CompletedTask;
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new MbusConnectionException($"Seri port baÅŸka bir uygulama tarafÄ±ndan kullanÄ±lÄ±yor: {_portName}", ex);
        }
        catch (IOException ex)
        {
            throw new MbusConnectionException($"Seri port aÃ§Ä±lamadÄ±: {_portName}", ex);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new MbusConfigurationException($"GeÃ§ersiz seri port ayarÄ±: {_portName} @ {_baudRate}", ex);
        }
    }

    public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        var port = RequirePort();
        try
        {
            await Task.Run(() => port.Write(data, 0, data.Length), cancellationToken);
        }
        catch (TimeoutException ex)
        {
            throw new MbusTimeoutException("Seri porta yazma zaman aÅŸÄ±mÄ±.", ex);
        }
        catch (IOException ex)
        {
            throw new MbusConnectionException("Seri port yazma hatasÄ±.", ex);
        }
    }

    public async Task<byte[]> ReceiveAsync(int timeoutMilliseconds, CancellationToken cancellationToken = default)
    {
        var port = RequirePort();
        var buffer = new List<byte>();
        var deadline = Environment.TickCount64 + timeoutMilliseconds;

        while (Environment.TickCount64 < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (port.BytesToRead > 0)
            {
                buffer.Add((byte)port.ReadByte());

                // Frame tamamlanma kontrolÃ¼: kÄ±sa frame stop, uzun frame L'ye gÃ¶re.
                if (buffer[0] == MbusConstants.SingleCharAck && buffer.Count == 1)
                {
                    return buffer.ToArray();
                }

                if (buffer.Count >= 2 && buffer[0] == MbusConstants.FrameStartShort && buffer.Count == 5)
                {
                    return buffer.ToArray();
                }

                if (buffer.Count >= 2 && buffer[0] == MbusConstants.FrameStartLong)
                {
                    var length = buffer[1];
                    var total = length + 6;
                    if (buffer.Count >= total)
                    {
                        return buffer.ToArray();
                    }
                }
            }
            else
            {
                await Task.Delay(10, cancellationToken);
            }
        }

        throw new MbusTimeoutException($"Seri port yanÄ±tÄ± {timeoutMilliseconds} ms iÃ§inde tamamlanmadÄ± ({buffer.Count} bayt alÄ±ndÄ±).");
    }

    private SerialPort RequirePort()
    {
        if (!IsOpen)
        {
            throw new MbusConnectionException("Seri port aÃ§Ä±k deÄŸil. Ã–nce OpenAsync Ã§aÄŸrÄ±lmalÄ±dÄ±r.");
        }

        return _port!;
    }

    public void Dispose()
    {
        if (_port is not null)
        {
            if (_port.IsOpen)
            {
                _port.Close();
            }

            _port.Dispose();
            _port = null;
        }
    }
}
