namespace Westalife.Application.Common.Interfaces;

/// <summary>Bakım bayrağı. Açıkken yazma istekleri durur. API yeniden başlasa da durum korunur.</summary>
public interface IMaintenanceMode
{
    bool IsEnabled { get; }

    DateTimeOffset? EnabledOn { get; }

    void Enable();

    void Disable();
}
