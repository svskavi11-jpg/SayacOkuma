using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;

namespace Westalife.Application.Features.Settings.GetAppSettings;

/// <summary>Sistem ayarlarını (tek satır) döndürür — Ayarlar sayfasının veri kaynağı. M-Bus donanım ayarları dahil.</summary>
public sealed record GetAppSettingsQuery : IRequest<AppSettingsResponse>;

public sealed record AppSettingsResponse(
    string CompanyName,
    string BuildingName,
    decimal FixedCostRatio,
    decimal ConsumptionRatio,
    string MeterConnectionType,
    string? TcpHost,
    int TcpPort,
    string? UsbPortName,
    int UsbBaudRate);

public sealed class GetAppSettingsQueryHandler : IRequestHandler<GetAppSettingsQuery, AppSettingsResponse>
{
    private readonly IApplicationDbContext _db;

    public GetAppSettingsQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AppSettingsResponse> Handle(GetAppSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new Westalife.Domain.Exceptions.NotFoundException("Sistem ayarları bulunamadı.");

        return new AppSettingsResponse(
            settings.CompanyName,
            settings.BuildingName,
            settings.FixedCostRatio,
            settings.ConsumptionRatio,
            settings.MeterConnectionType,
            settings.TcpHost,
            settings.TcpPort,
            settings.UsbPortName,
            settings.UsbBaudRate);
    }
}
