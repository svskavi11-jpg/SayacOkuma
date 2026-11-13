using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Entities;
using Westalife.Infrastructure.Auth;
using Westalife.Application.Common.Models;
using Westalife.Application.Features.Apartments.Commands.ImportMetersFromExcel;
using Westalife.Application.Features.MeterReadings.Commands.ImportReadingsFromExcel;
using Westalife.Application.Features.Payments.Commands.ParseBankStatement;
using Westalife.Infrastructure.Metering.Readers;
using Westalife.Infrastructure.Notifications;
using Westalife.Infrastructure.Persistence.Contexts;
using Westalife.Infrastructure.Persistence.Interceptors;
using Westalife.Infrastructure.Storage;

namespace Westalife.Infrastructure;

/// <summary>Infrastructure katmanının servis kayıtları.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, string connectionString, string? residentDocumentRoot = null)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<AuditStampInterceptor>();
        services.AddDbContext<ApplicationDbContext>((sp, options) =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null))
                // EF 10'un bilinen yanlış pozitifi ("PendingModelChangesWarning"):
                // migration snapshot ile model eşitliği boş diff migration ile teyit edilmiştir.
                .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
                .AddInterceptors(sp.GetRequiredService<AuditStampInterceptor>()));

        // Application katmanı DbContext'e IApplicationDbContext soyutlaması üzerinden erişir.
        services.AddScoped<IApplicationDbContext>(sp =>
            sp.GetRequiredService<ApplicationDbContext>());

        services.AddScoped<ITenantContext, HttpTenantContext>();
        services.AddOptions<AuthOptions>().BindConfiguration(AuthOptions.SectionName);
        services.AddScoped<IAuthService, JwtAuthService>();
        services.AddScoped<IAccountSecurity, AccountSecurity>();
        services.AddScoped<StaffAccessReader>();
        services.AddScoped<IAccessProfileReader>(sp => sp.GetRequiredService<StaffAccessReader>());
        services.AddScoped<ITenantUserProvisioner, TenantUserProvisioner>();
        services.AddScoped<IRoleAdmin, RoleAdmin>();
        services.AddScoped<IResidentAccess, ResidentAccess>();
        services.AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<AppRole>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        services.AddMetering();

        var documentRoot = string.IsNullOrWhiteSpace(residentDocumentRoot)
            ? Path.Combine(AppContext.BaseDirectory, "data", "resident-documents")
            : residentDocumentRoot;
        services.AddSingleton<IResidentDocumentStore>(_ => new LocalResidentDocumentStore(documentRoot));

        services.AddHttpClient(nameof(SmsNotificationService));
        services.AddHttpClient(nameof(WhatsAppNotificationService));
        services.AddScoped<EmailNotificationService>();
        services.AddScoped<SmsNotificationService>();
        services.AddScoped<WhatsAppNotificationService>();
        services.AddScoped<INotificationService>(sp => sp.GetRequiredService<EmailNotificationService>());
        services.AddScoped<INotificationService>(sp => sp.GetRequiredService<SmsNotificationService>());
        services.AddScoped<INotificationService>(sp => sp.GetRequiredService<WhatsAppNotificationService>());
        services.AddScoped<INoticePdfGenerator, Reporting.Pdf.NoticePdfGenerator>();
        services.AddSingleton<IPaymentReceiptPdf, Reporting.Pdf.PaymentReceiptPdfDocument>();

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
        services.AddScoped<IBankStatementParser, Reporting.Excel.BankStatementParser>();
        services.AddScoped<IMbusConnectionTester, Metering.Connections.MbusConnectionTester>();

        services.AddSingleton<MeterReaderFactory>();
        services.AddSingleton<IMeterReaderFactory>(sp => sp.GetRequiredService<MeterReaderFactory>());

        // Varsayılan okuyucu: yapılandırmadaki stratejiye göre çözülür.
        services.AddScoped<IMeterReader>(sp =>
            sp.GetRequiredService<MeterReaderFactory>().CreateDefault());

        return services;
    }
}
