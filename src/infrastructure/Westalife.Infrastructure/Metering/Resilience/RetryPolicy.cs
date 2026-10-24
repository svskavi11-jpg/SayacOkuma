using Microsoft.Extensions.Logging;
using Westalife.Infrastructure.Metering.Exceptions;

namespace Westalife.Infrastructure.Metering.Resilience;

/// <summary>
/// Geçici hatalar için manuel exponential backoff (harici bağımlılık yok).
/// Gecikmeler: taban, taban*2, taban*4 ...
/// </summary>
public static class RetryPolicy
{
    public static async Task<T> ExecuteAsync<T>(
        Func<int, Task<T>> action,
        int maxRetryCount,
        int baseDelayMs,
        ILogger logger,
        string operationName,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await action(attempt);
            }
            catch (Exception ex) when (attempt < maxRetryCount && IsTransient(ex))
            {
                var delay = baseDelayMs * (int)Math.Pow(2, attempt);
                logger.LogWarning(ex,
                    "{Operation} denemesi {Attempt}/{MaxRetry} başarısız oldu; {Delay} ms sonra tekrar denenecek.",
                    operationName, attempt + 1, maxRetryCount + 1, delay);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    /// <summary>Yalnızca geçici hatalar retry edilir; protokol/konfigürasyon hataları anında fırlatılır.</summary>
    public static bool IsTransient(Exception exception) =>
        exception is MbusTimeoutException or MbusConnectionException;
}
