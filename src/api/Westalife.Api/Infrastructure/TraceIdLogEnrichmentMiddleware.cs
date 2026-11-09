using Serilog.Context;

namespace Westalife.Api.Infrastructure;

/// <summary>
/// Her HTTP isteğinde TraceId'yi Serilog LogContext'e yazar.
/// Faz 7'de aynı noktaya UserId / TenantId eklenecek.
/// </summary>
public sealed class TraceIdLogEnrichmentMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        using (LogContext.PushProperty("TraceId", context.TraceIdentifier))
        {
            await next(context);
        }
    }
}
