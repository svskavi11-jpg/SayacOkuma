using Westalife.Application.Common.Models;

namespace Westalife.Application.Common.Interfaces;

/// <summary>
/// Okuma motoru soyutlaması (Strategy Pattern). Implementasyonlar Infrastructure'da yaşar:
/// gerçek donanım (USB/TCP) veya simülatör. Uygulama katmanı hangisinin çalıştığını bilmek zorunda değildir.
/// </summary>
public interface IMeterReader
{
    /// <summary>
    /// Izar Center üzerinden tüm sayaçları okur. Tek sayacın hatası seansı bozmaz;
    /// her sayaç için ayrı sonuç döner (kısmi başarı modeli).
    /// </summary>
    Task<MeterReadBatch> ReadAllAsync(CancellationToken cancellationToken = default);
}
