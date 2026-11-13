using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;
using System.Text.Json;
using System.Text.Json.Serialization;
using Westalife.Api.Infrastructure;
using Westalife.Application;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Infrastructure;
using Westalife.Infrastructure.Auth;

var builder = WebApplication.CreateBuilder(args);

// Serilog: console (Docker stdout) + daily rolling file (relative path - Kural #2: no absolute paths).
builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection ayari eksik.");

// Infrastructure: DbContext (PostgreSQL UTF-8/tr-TR) + meter reading engine (M-Bus/Simulator).
builder.Services.AddInfrastructure(
    connectionString,
    builder.Configuration["ResidentDocuments:RootPath"]);

// Application: CQRS (MediatR) + FluentValidation pipeline.
builder.Services.AddApplication();

// Faz 6.9: enum'lar JSON'da string olarak serileştirilir/okunur (örn: "TarifeBazli", "Aidat").
// Girdi: frontend string enum gönderir; Çıktı: LedgerLine.Type gibi alanlar string döner.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgresql", tags: ["ready", "db"]);

// CORS: dev ortamında Vite geliştirme sunucusuna izin ver (prod'da nginx aynı origin'den sunar — CORS gerekmez).
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.WithOrigins("http://localhost:5173", "https://localhost:5173")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            // Prod'da nginx reverse proxy aynı origin'den sunar; cross-origin isteğe gerek yok.
            policy.WithOrigins(builder.Configuration["AllowedOrigins"] ?? "")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    }));

// OpenAPI + error standards (RFC 7807 ProblemDetails).
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddSingleton<IMaintenanceMode, FileMaintenanceMode>();

var signingKey = builder.Configuration["Auth:SigningKey"];
if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(signingKey))
{
    throw new InvalidOperationException("Auth:SigningKey ayari prod ortaminda zorunludur.");
}

if (!string.IsNullOrWhiteSpace(signingKey))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Auth:Issuer"] ?? "westalife",
                ValidAudience = builder.Configuration["Auth:Audience"] ?? "westalife",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                RoleClaimType = DomainConstants.Auth.RoleClaim,
                NameClaimType = DomainConstants.Auth.UserIdClaim
            };
        });

    builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
    builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
}

// Prod'da API anahtari zorunlu: eksikse uygulama acilmaz (guvensiz acik API'yi onler).
if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(builder.Configuration[ApiKeyMiddleware.ConfigKey]))
{
    throw new InvalidOperationException($"{ApiKeyMiddleware.ConfigKey} ayari prod ortaminda zorunludur.");
}

var app = builder.Build();

app.UseMiddleware<TraceIdLogEnrichmentMiddleware>();

app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("TraceId", httpContext.TraceIdentifier);
    };
});

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(); // /scalar - dev-only test UI

    // HTTPS redirect yalnız dev'de (prod'da nginx TLS'i sonlandırır).
    app.UseHttpsRedirection();
}

app.UseCors();

app.UseMiddleware<ApiKeyMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<MaintenanceModeMiddleware>();

app.MapControllers();

await IdentitySeeder.SeedAsync(app.Services);
await PermissionSeeder.SeedAsync(app.Services);

// Konteyner HEALTHCHECK: uygulama + PostgreSQL. /health API anahtarından muaftır (/api dışı).
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = WriteHealthResponseAsync,
});

app.Run();

static Task WriteHealthResponseAsync(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json; charset=utf-8";
    context.Response.StatusCode = report.Status == HealthStatus.Healthy
        ? StatusCodes.Status200OK
        : StatusCodes.Status503ServiceUnavailable;

    var payload = new
    {
        status = report.Status.ToString(),
        checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            durationMs = entry.Value.Duration.TotalMilliseconds,
            error = entry.Value.Exception?.Message,
        }),
    };

    return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
}
