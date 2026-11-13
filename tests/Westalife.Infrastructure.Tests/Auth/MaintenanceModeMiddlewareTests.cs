using Microsoft.AspNetCore.Http;
using Westalife.Api.Infrastructure;

namespace Westalife.Infrastructure.Tests.Auth;

public sealed class MaintenanceModeMiddlewareTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("westalife-maintenance-").FullName;

    [Fact]
    public async Task PaymentPostReturns503WhileFlagExists()
    {
        var gate = new FileMaintenanceMode(Path.Combine(_directory, "maintenance.on"));
        gate.Enable();
        var restarted = new FileMaintenanceMode(Path.Combine(_directory, "maintenance.on"));
        Assert.True(restarted.IsEnabled);

        var blocked = await SendAsync(restarted, HttpMethods.Post, "/api/v1/payments");
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, blocked.Status);
        Assert.Contains("Sistem bakımda", blocked.Body);

        var read = await SendAsync(restarted, HttpMethods.Get, "/api/v1/payments");
        Assert.Equal(StatusCodes.Status204NoContent, read.Status);

        var login = await SendAsync(restarted, HttpMethods.Post, "/api/auth/login");
        Assert.Equal(StatusCodes.Status204NoContent, login.Status);

        var toggle = await SendAsync(restarted, HttpMethods.Post, "/api/v1/admin/migration/maintenance");
        Assert.Equal(StatusCodes.Status204NoContent, toggle.Status);

        restarted.Disable();
        var open = await SendAsync(restarted, HttpMethods.Post, "/api/v1/payments");
        Assert.Equal(StatusCodes.Status204NoContent, open.Status);
        Assert.False(File.Exists(Path.Combine(_directory, "maintenance.on")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static async Task<(int Status, string Body)> SendAsync(
        FileMaintenanceMode gate,
        string method,
        string path)
    {
        var middleware = new MaintenanceModeMiddleware(_ =>
        {
            _.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(context, gate);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return (context.Response.StatusCode, await reader.ReadToEndAsync());
    }
}
