using MediatR;
using Microsoft.Extensions.Logging;

namespace Westalife.Application.Common.Behaviors;

/// <summary>MediatR pipeline: her istek için başlangıç, süre ve hata loglaması (Serilog Faz 4'te bağlanır).</summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation("İşlem başlıyor: {RequestName}", requestName);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var response = await next();
            stopwatch.Stop();
            _logger.LogInformation("İşlem tamamlandı: {RequestName} ({ElapsedMs} ms)", requestName, stopwatch.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "İşlem başarısız: {RequestName} ({ElapsedMs} ms)", requestName, stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}
