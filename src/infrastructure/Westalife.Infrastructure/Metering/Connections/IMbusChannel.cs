namespace Westalife.Infrastructure.Metering.Connections;

/// <summary>
/// Transport soyutlaması: USB (Serial) ve TCP (Ethernet) aynı protokol kodunu
/// bu arayüz üzerinden kullanır. Donanım farkı sadece bu katmanda bellenir.
/// </summary>
public interface IMbusChannel : IDisposable
{
    bool IsOpen { get; }

    Task OpenAsync(CancellationToken cancellationToken = default);

    Task SendAsync(byte[] data, CancellationToken cancellationToken = default);

    /// <summary>Belirtilen zaman aşımı içinde yanıt bekler; süre dolarsa MbusTimeoutException fırlatır.</summary>
    Task<byte[]> ReceiveAsync(int timeoutMilliseconds, CancellationToken cancellationToken = default);
}
