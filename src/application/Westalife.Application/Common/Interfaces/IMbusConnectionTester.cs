using Westalife.Application.Common.Models;

namespace Westalife.Application.Common.Interfaces;

/// <summary>M-Bus bağlantı testi sonucu.</summary>
public sealed record MbusConnectionTestResult(bool Success, string Message, int DurationMs);

/// <summary>
/// M-Bus bağlantı testi soyutlaması — Ayarlar sayfasındaki "Bağlantıyı Test Et" butonunun arkası.
/// Tcp: TCP bağlantı açma/kapama; Usb: COM portu açma/kapama; Simulator: her zaman başarılı.
/// </summary>
public interface IMbusConnectionTester
{
    Task<MbusConnectionTestResult> TestAsync(MeteringOptions options, CancellationToken cancellationToken = default);
}
