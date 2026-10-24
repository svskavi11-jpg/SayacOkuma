using System.Diagnostics;
using Westalife.Application.Common.Enums;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Common.Models;
using Westalife.Infrastructure.Metering.Exceptions;

namespace Westalife.Infrastructure.Metering.Connections;

/// <summary>
/// M-Bus bağlantı testi: seçilen tipe göre kanal açma/kapama denemesi yapar.
/// Tcp: gerçek TCP bağlantısı; Usb: COM portu açma; Simulator: her zaman başarılı.
/// </summary>
public sealed class MbusConnectionTester : IMbusConnectionTester
{
    public async Task<MbusConnectionTestResult> TestAsync(MeteringOptions options, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (options.ConnectionType == MeterReaderConnectionType.Simulator)
        {
            stopwatch.Stop();
            return new MbusConnectionTestResult(true, "Simülatör modu aktif — bağlantı testi başarılı.", (int)stopwatch.ElapsedMilliseconds);
        }

        // Tcp/Usb için gerçek kanal açma denemesi (Close → dispose ile port serbest kalır).
        IMbusChannel? channel = null;
        try
        {
            channel = MbusChannelFactory.Create(options);
            await channel.OpenAsync(cancellationToken);
            stopwatch.Stop();
            var message = options.ConnectionType == MeterReaderConnectionType.Tcp
                ? $"Izar Center'a TCP bağlantısı başarılı ({options.Tcp.Host}:{options.Tcp.Port})."
                : $"Seri port açıldı: {options.Usb.PortName} @ {options.Usb.BaudRate} baud.";
            return new MbusConnectionTestResult(true, message, (int)stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex) when (ex is MeteringException)
        {
            stopwatch.Stop();
            return new MbusConnectionTestResult(false, ex.Message, (int)stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            channel?.Dispose();
        }
    }
}
