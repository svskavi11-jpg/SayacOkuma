using Westalife.Application.Common.Interfaces;

namespace Westalife.Api.Infrastructure;

/// <summary>
/// Bakım açıkken yazma istekleri 503 döner. Okuma, sağlık, giriş ve bakım ucu çalışmaya devam eder.
/// </summary>
public sealed class MaintenanceModeMiddleware(RequestDelegate next)
{
    public const string Message = "Sistem bakımda. Kısa süre sonra yeniden deneyin.";

    public async Task InvokeAsync(HttpContext context, IMaintenanceMode maintenance)
    {
        if (!maintenance.IsEnabled || IsAllowed(context.Request))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new
        {
            title = "Sistem bakımda",
            status = StatusCodes.Status503ServiceUnavailable,
            detail = Message,
        });
    }

    internal static bool IsAllowed(HttpRequest request)
    {
        if (HttpMethods.IsGet(request.Method)
            || HttpMethods.IsHead(request.Method)
            || HttpMethods.IsOptions(request.Method))
        {
            return true;
        }

        if (!HttpMethods.IsPost(request.Method))
        {
            return false;
        }

        var path = request.Path.Value ?? string.Empty;
        return path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/v1/admin/migration/maintenance", StringComparison.OrdinalIgnoreCase);
    }
}
