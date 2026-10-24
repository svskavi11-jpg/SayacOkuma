using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Settings.GetAppSettings;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Settings.UpdateAppSettings;

/// <summary>
/// Sistem ayarlarını günceller. Aynı transaction'da Building tablosuna AYNALANIR
/// (bina adı + sabit gider oranı) — mevcut hesaplama motoru (Faz 3) etkilenmeden yeni oranlar devreye girer.
/// M-Bus donanım ayarları da buradan yönetilir (bağlantı tipi, TCP/USB parametreleri).
/// </summary>
public sealed record UpdateAppSettingsCommand(
    string CompanyName,
    string BuildingName,
    decimal FixedCostRatio,
    decimal ConsumptionRatio,
    string MeterConnectionType,
    string? TcpHost,
    int TcpPort,
    string? UsbPortName,
    int UsbBaudRate) : IRequest<AppSettingsResponse>;

public sealed class UpdateAppSettingsCommandValidator : AbstractValidator<UpdateAppSettingsCommand>
{
    public UpdateAppSettingsCommandValidator()
    {
        RuleFor(c => c.CompanyName)
            .NotEmpty()
            .MaximumLength(DomainConstants.AppSettings.CompanyNameMaxLength);

        RuleFor(c => c.BuildingName)
            .NotEmpty()
            .MaximumLength(DomainConstants.Building.NameMaxLength);

        RuleFor(c => c.FixedCostRatio).GreaterThan(0);
        RuleFor(c => c.ConsumptionRatio).GreaterThan(0);

        RuleFor(c => c)
            .Must(c => decimal.Round(c.FixedCostRatio + c.ConsumptionRatio, 2) == 1m)
            .WithMessage(DomainConstants.AppSettings.RatiosMustSumToOne);

        RuleFor(c => c.MeterConnectionType)
            .Must(t => t == DomainConstants.AppSettings.ConnectionSimulator
                    || t == DomainConstants.AppSettings.ConnectionTcp
                    || t == DomainConstants.AppSettings.ConnectionUsb)
            .WithMessage(DomainConstants.AppSettings.MeterConnectionTypeInvalid);

        RuleFor(c => c.TcpHost)
            .MaximumLength(DomainConstants.AppSettings.TcpHostMaxLength)
            .When(c => c.MeterConnectionType == DomainConstants.AppSettings.ConnectionTcp);

        // Etap 6.7 bug fix: TcpPort/UsbBaudRate kuralları artık yalnız ilgili bağlantı tipinde
        // uygulanır — "Simulator" seçiliyken TCP/USB alanları validasyon hatası VERMEZ.
        RuleFor(c => c.TcpPort)
            .InclusiveBetween(DomainConstants.AppSettings.TcpPortMin, DomainConstants.AppSettings.TcpPortMax)
            .When(c => c.MeterConnectionType == DomainConstants.AppSettings.ConnectionTcp);

        RuleFor(c => c.UsbBaudRate)
            .GreaterThan(0)
            .When(c => c.MeterConnectionType == DomainConstants.AppSettings.ConnectionUsb);

        // Koşullu zorunluluklar
        RuleFor(c => c.TcpHost)
            .NotEmpty()
            .When(c => c.MeterConnectionType == DomainConstants.AppSettings.ConnectionTcp)
            .WithMessage(DomainConstants.AppSettings.TcpHostRequired);

        RuleFor(c => c.UsbPortName)
            .NotEmpty()
            .When(c => c.MeterConnectionType == DomainConstants.AppSettings.ConnectionUsb)
            .WithMessage(DomainConstants.AppSettings.UsbPortNameRequired);
    }
}

public sealed class UpdateAppSettingsCommandHandler
    : IRequestHandler<UpdateAppSettingsCommand, AppSettingsResponse>
{
    private readonly IApplicationDbContext _db;

    public UpdateAppSettingsCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<AppSettingsResponse> Handle(
        UpdateAppSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new Westalife.Domain.Exceptions.NotFoundException("Sistem ayarları bulunamadı.");

        var building = await _db.Buildings
            .FirstOrDefaultAsync(b => b.IsActive, cancellationToken);

        // Domain doğrulaması (oran toplamı %100) — geçersizse hiçbir şey yazılmaz.
        settings.Update(
            request.CompanyName, request.BuildingName,
            request.FixedCostRatio, request.ConsumptionRatio);

        // M-Bus donanım ayarları (bağlantı tipine göre koşullu doğrulama Domain'de).
        settings.UpdateMeterConnection(
            request.MeterConnectionType,
            request.TcpHost, request.TcpPort,
            request.UsbPortName, request.UsbBaudRate);

        // Mirror: hesaplama motoru Building.FixedCostRatio okur; bina adı da senkronize edilir.
        building?.UpdateDetails(settings.BuildingName, building.Address);
        building?.UpdateFixedCostRatio(settings.FixedCostRatio);

        await _db.SaveChangesAsync(cancellationToken);

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
