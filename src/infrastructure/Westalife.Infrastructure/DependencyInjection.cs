using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Common.Models;
using Westalife.Application.Features.Apartments.Commands.ImportMetersFromExcel;
using Westalife.Application.Features.MeterReadings.Commands.ImportReadingsFromExcel;
using Westalife.Infrastructure.Metering.Readers;
using Westalife.Infrastructure.Persistence.Contexts;

namespace Westalife.Infrastructure;

/// <summary>Infrastructure katmanının servis kayıtları.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null))
                // EF 10'un bilinen yanlış pozitifi ("PendingModelChangesWarning"):
                // migration snapshot ile model eşitliği boş diff migration ile teyit edilmiştir.
                .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

        // Application katmanı DbContext'e IApplicationDbContext soyutlaması üzerinden erişir.
        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());

        services.AddMetering();

        return services;
    }

    /// <summary>
    /// Okuma motoru kayıtları (Factory Pattern). Okuyucular IMeterReaderFactory üzerinden çözülür;
    /// hangi stratejinin (USB/TCP/Simülatör) çalışacağı appsettings "Metering" bölümünden gelir.
    /// </summary>
    private static IServiceCollection AddMetering(this IServiceCollection services)
    {
        services.AddOptions<MeteringOptions>()
            .BindConfiguration(MeteringOptions.SectionName);

        services.AddOptions<ReportingOptions>()
            .BindConfiguration(ReportingOptions.SectionName);

        services.AddScoped<IExcelMeterImportParser, Reporting.Excel.ExcelMeterImportParser>();
        services.AddScoped<IExcelReadingImportParser, Reporting.Excel.ExcelReadingImportParser>();
        services.AddScoped<IMbusConnectionTester, Metering.Connections.MbusConnectionTester>();

        services.AddSingleton<MeterReaderFactory>();
        services.AddSingleton<IMeterReaderFactory>(sp => sp.GetRequiredService<MeterReaderFactory>());

        // Varsayılan okuyucu: yapılandırmadaki stratejiye göre çözülür.
        services.AddScoped<IMeterReader>(sp =>
            sp.GetRequiredService<MeterReaderFactory>().CreateDefault());

        return services;
    }
}
