using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Enums;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Common.Models;
using Westalife.Domain.Constants;

namespace Westalife.Application.Features.Settings.TestMbusConnection;

/// <summary>
/// Ayarlar sayfasındaki "Bağlantıyı Test Et" butonu: DB'deki M-Bus ayarlarıyla
/// Izar Center'a (TCP), COM portuna (USB) ulaşmayı dener; Simulator her zaman başarılı döner.
/// </summary>
public sealed record TestMbusConnectionCommand : IRequest<MbusConnectionTestResult>;

public sealed class TestMbusConnectionCommandHandler
    : IRequestHandler<TestMbusConnectionCommand, MbusConnectionTestResult>
{
    private readonly IApplicationDbContext _db;
    private readonly IMbusConnectionTester _tester;

    public TestMbusConnectionCommandHandler(IApplicationDbContext db, IMbusConnectionTester tester)
    {
        _db = db;
        _tester = tester;
    }

    public async Task<MbusConnectionTestResult> Handle(
        TestMbusConnectionCommand request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new Westalife.Domain.Exceptions.NotFoundException("Sistem ayarları bulunamadı.");

        var options = new MeteringOptions
        {
            ConnectionType = settings.MeterConnectionType switch
            {
                DomainConstants.AppSettings.ConnectionTcp => MeterReaderConnectionType.Tcp,
                DomainConstants.AppSettings.ConnectionUsb => MeterReaderConnectionType.Usb,
                _ => MeterReaderConnectionType.Simulator
            },
            Tcp = { Host = settings.TcpHost ?? string.Empty, Port = settings.TcpPort },
            Usb = { PortName = settings.UsbPortName ?? string.Empty, BaudRate = settings.UsbBaudRate },
        };

        return await _tester.TestAsync(options, cancellationToken);
    }
}
