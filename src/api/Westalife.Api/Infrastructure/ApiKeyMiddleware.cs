using System.Security.Cryptography;
using System.Text;

namespace Westalife.Api.Infrastructure;

/// <summary>
/// Minimal API anahtari korumasi (Faz 7'de JWT ile degistirilecek).
/// /api ile baslayan isteklerde <see cref="HeaderName"/> basligini sabit-sureli karsilastirir.
/// /health, OpenAPI ve Scalar (dev) muaf tutulur.
/// </summary>
public sealed class ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
{
    public const string HeaderName = "X-Api-Key";
    public const string ConfigKey = "Security:ApiKey";
    private const string ProtectedPrefix = "/api";

    private readonly byte[]? _expectedKey = string.IsNullOrWhiteSpace(configuration[ConfigKey])
        ? null
        : Encoding.UTF8.GetBytes(configuration[ConfigKey]!);

    public async Task InvokeAsync(HttpContext context)
    {
        // Anahtar tanimli degilse (yalniz Development'ta mumkun) koruma devre disi.
        if (_expectedKey is null || !context.Request.Path.StartsWithSegments(ProtectedPrefix)
            || context.Request.Method == HttpMethods.Options)
        {
            await next(context);
            return;
        }

        var provided = context.Request.Headers[HeaderName].ToString();
        var providedBytes = Encoding.UTF8.GetBytes(provided);

        if (CryptographicOperations.FixedTimeEquals(providedBytes, _expectedKey))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new
        {
            status = StatusCodes.Status401Unauthorized,
            title = "Gecersiz veya eksik API anahtari."
        });
    }
}
