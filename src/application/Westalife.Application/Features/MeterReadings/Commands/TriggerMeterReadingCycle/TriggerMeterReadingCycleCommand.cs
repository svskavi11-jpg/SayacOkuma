using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Enums;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Common.Models;
using Westalife.Application.Features.MeterReadings.Commands.RecordMeterReadings;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Enums;

namespace Westalife.Application.Features.MeterReadings.Commands.TriggerMeterReadingCycle;

/// <summary>
/// Tek seferlik okuma döngüsü: M-Bus bağlantı ayarları ARTIK VERİTABANINDAN OKUNUR (Ayarlar sayfası),
/// MeterReaderFactory ile uygun okuyucu (USB/TCP/Simülatör) ANLIK üretilir — restart gerektirmez.
/// Infrastructure okuma motoruna yalnızca Application arayüzü üzerinden dokunulur (izolasyon kuralı).
/// </summary>
public sealed record TriggerMeterReadingCycleCommand : IRequest<MeterReadingCycleResult>;

public sealed record MeterReadingCycleResult(
    string ConnectionType,
    int TotalMeters,
    int ReadSuccessCount,
    int ReadFailureCount,
    int InsertedCount,
    int SkippedDuplicateCount,
    int SkippedDeviceErrorCount,
    int UnmatchedSerialCount,
    int LockedPeriodSkippedCount);

public sealed class TriggerMeterReadingCycleCommandHandler
    : IRequestHandler<TriggerMeterReadingCycleCommand, MeterReadingCycleResult>
{
    private readonly IMeterReaderFactory _meterReaderFactory;
    private readonly IApplicationDbContext _db;
    private readonly IMediator _mediator;

    public TriggerMeterReadingCycleCommandHandler(
        IMeterReaderFactory meterReaderFactory, IApplicationDbContext db, IMediator mediator)
    {
        _meterReaderFactory = meterReaderFactory;
        _db = db;
        _mediator = mediator;
    }

    public async Task<MeterReadingCycleResult> Handle(
        TriggerMeterReadingCycleCommand request, CancellationToken cancellationToken)
    {
        // M-Bus ayarları DB'den okunur (Ayarlar sayfasından yönetilir) → anlık okuyucu üretimi.
        var settings = await _db.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        var options = BuildOptions(settings);
        var reader = _meterReaderFactory.Create(options);

        var batch = await reader.ReadAllAsync(cancellationToken);

        var snapshots = batch.Results
            .Where(r => r.Snapshot is not null)
            .Select(r => r.Snapshot!)
            .ToList();

        // Kilitli dönem koruması (Etap 6.6): kilitli fatura penceresine düşen okumalar kaydedilmez,
        // sayılarak raporlanır (kısmi başarı modeli bozulmaz).
        var lockedPeriods = await _db.GasInvoices
            .AsNoTracking()
            .Where(i => i.IsLocked)
            .Select(i => new { i.ReadingDateFrom, i.ReadingDateTo })
            .ToListAsync(cancellationToken);

        var lockedPeriodSkippedCount = 0;
        if (lockedPeriods.Count > 0)
        {
            var acceptedSnapshots = new List<MeterSnapshot>();
            foreach (var snapshot in snapshots)
            {
                if (lockedPeriods.Any(p => p.ReadingDateFrom <= snapshot.ReadingTimestampUtc
                    && snapshot.ReadingTimestampUtc < p.ReadingDateTo))
                {
                    lockedPeriodSkippedCount++;
                    continue;
                }

                acceptedSnapshots.Add(snapshot);
            }

            snapshots = acceptedSnapshots;
        }

        var recordResult = await _mediator.Send(
            new RecordMeterReadingsCommand(snapshots, MapSource(options.ConnectionType)),
            cancellationToken);

        return new MeterReadingCycleResult(
            options.ConnectionType.ToString(),
            batch.Results.Count,
            batch.SuccessCount,
            batch.FailureCount,
            recordResult.InsertedCount,
            recordResult.SkippedDuplicateCount,
            recordResult.SkippedDeviceErrorCount,
            recordResult.UnmatchedSerialCount,
            lockedPeriodSkippedCount);
    }

    /// <summary>DB ayarlarını MeteringOptions'a çevirir; kayıt yoksa/appsettings defaults kullanılır.</summary>
    private static MeteringOptions BuildOptions(Domain.Entities.AppSettings? settings)
    {
        var options = new MeteringOptions(); // defaults: Simulator, 3000ms, 2 retry

        if (settings is null)
        {
            return options;
        }

        options.ConnectionType = settings.MeterConnectionType switch
        {
            DomainConstants.AppSettings.ConnectionTcp => MeterReaderConnectionType.Tcp,
            DomainConstants.AppSettings.ConnectionUsb => MeterReaderConnectionType.Usb,
            _ => MeterReaderConnectionType.Simulator
        };

        options.Tcp.Host = settings.TcpHost ?? string.Empty;
        options.Tcp.Port = settings.TcpPort;
        options.Usb.PortName = settings.UsbPortName ?? string.Empty;
        options.Usb.BaudRate = settings.UsbBaudRate;

        return options;
    }

    /// <summary>Bağlantı tipini okuma kaynağına çevirir (ReadingSource.Simulator Domain'de tanımlı).</summary>
    private static ReadingSource MapSource(MeterReaderConnectionType connectionType) => connectionType switch
    {
        MeterReaderConnectionType.Usb => ReadingSource.MbusUsb,
        MeterReaderConnectionType.Tcp => ReadingSource.MbusEthernet,
        MeterReaderConnectionType.Simulator => ReadingSource.Simulator,
        _ => ReadingSource.ManualEntry
    };
}
