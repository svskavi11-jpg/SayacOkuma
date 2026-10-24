using Westalife.Application.Common.Enums;
using Westalife.Application.Common.Models;
using Westalife.Infrastructure.Metering.Exceptions;

namespace Westalife.Infrastructure.Metering.Connections;

/// <summary>
/// Factory Pattern: yapılandırma tipine göre uygun transport kanalını üretir.
/// </summary>
public static class MbusChannelFactory
{
    public static IMbusChannel Create(MeteringOptions options)
    {
        return options.ConnectionType switch
        {
            MeterReaderConnectionType.Tcp => new MbusTcpChannel(
                RequireHost(options), options.Tcp.Port, options.ResponseTimeoutMs),

            MeterReaderConnectionType.Usb => new MbusSerialChannel(
                RequirePortName(options), options.Usb.BaudRate),

            _ => throw new MbusConfigurationException(
                $"Bu bağlantı tipi transport kanalı gerektirmez: {options.ConnectionType}")
        };
    }

    private static string RequireHost(MeteringOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Tcp.Host))
        {
            throw new MbusConfigurationException("Metering:Tcp:Host yapılandırılmamış (TCP bağlantı tipi seçili).");
        }

        return options.Tcp.Host;
    }

    private static string RequirePortName(MeteringOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Usb.PortName))
        {
            throw new MbusConfigurationException("Metering:Usb:PortName yapılandırılmamış (USB bağlantı tipi seçili).");
        }

        return options.Usb.PortName;
    }
}
