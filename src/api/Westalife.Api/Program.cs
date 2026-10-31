using Scalar.AspNetCore;
using Serilog;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Westalife.Api.Infrastructure;
using Westalife.Application;
using Westalife.Infrastructure;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Serilog: console (Docker stdout) + daily rolling file (relative path - Kural #2: no absolute paths).
builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// Infrastructure: DbContext (PostgreSQL UTF-8/tr-TR) + meter reading engine (M-Bus/Simulator).
builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection ayari eksik."));

// Application: CQRS (MediatR) + FluentValidation pipeline.
builder.Services.AddApplication();

// Faz 6.9: enum'lar JSON'da string olarak serileştirilir/okunur (örn: "TarifeBazli", "Aidat").
// Girdi: frontend string enum gönderir; Çıktı: LedgerLine.Type gibi alanlar string döner.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

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

// Prod'da API anahtari zorunlu: eksikse uygulama acilmaz (guvensiz acik API'yi onler).
if (!builder.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(builder.Configuration[ApiKeyMiddleware.ConfigKey]))
{
    throw new InvalidOperationException($"{ApiKeyMiddleware.ConfigKey} ayari prod ortaminda zorunludur.");
}

var app = builder.Build();

app.UseSerilogRequestLogging();

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

app.UseAuthorization();

app.MapControllers();

// Konteyner HEALTHCHECK icin minimal saglik ucu (db bagimliligi yok - hizli yanit).
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();