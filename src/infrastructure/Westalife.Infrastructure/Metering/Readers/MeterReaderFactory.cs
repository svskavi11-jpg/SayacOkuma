using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Westalife.Application.Common.Enums;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Common.Models;
using Westalife.Infrastructure.Metering.Connections;
using Westalife.Infrastructure.Metering.Exceptions;

namespace Westalife.Infrastructure.Metering.Readers;

/// <summary>
/// Factory Pattern: yapılandırma ("Metering:ConnectionType") hangi okuma stratejisini
/// (USB / TCP / Simülatör) çalıştıracağını belirler. Uygulama katmanı bu seçimden habersizdir.
/// </summary>
public sealed class MeterReaderFactory : IMeterReaderFactory
{
    private readonly IOptions<MeteringOptions> _options;
    private readonly ILoggerFactory _loggerFactory;

    public MeterReaderFactory(IOptions<MeteringOptions> options, ILoggerFactory loggerFactory)
    {
        _options = options;
        _loggerFactory = loggerFactory;
    }

    public IMeterReader Create(MeteringOptions options)
    {
        return options.ConnectionType switch
        {
            MeterReaderConnectionType.Simulator => new SimulatedMeterReader(options),

            MeterReaderConnectionType.Tcp => CreateRealReader(options),

            MeterReaderConnectionType.Usb => CreateRealReader(options),

            _ => throw new MbusConfigurationException($"Bilinmeyen bağlantı tipi: {options.ConnectionType}")
        };
    }

    /// <summary>DI varsayılanı: yapılandırmadaki stratejiye göre okuyucu üretir.</summary>
    public IMeterReader CreateDefault() => Create(_options.Value);

    private IMeterReader CreateRealReader(MeteringOptions options)
    {
        var channel = MbusChannelFactory.Create(options);
        var logger = _loggerFactory.CreateLogger<MbusMeterReader>();

        // Faz 2: birincil adresler yapılandırmadan gelir (boşsa reader uyarı verip boş batch döner).
        // Faz 3: adresler Meters tablosundan CQRS komutu ile geçilecektir.
        var addresses = options.PrimaryAddresses.Select(a => (byte)a).ToList();

        return new MbusMeterReader(channel, options, addresses, logger);
    }
}
