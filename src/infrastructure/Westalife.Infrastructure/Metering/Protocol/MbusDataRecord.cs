namespace Westalife.Infrastructure.Metering.Protocol;

/// <summary>Çözümlenmiş tek bir M-Bus veri kaydı (DIF/VIF/Value).</summary>
public sealed record MbusDataRecord
{
    public required byte Dif { get; init; }

    public required byte Vif { get; init; }

    /// <summary>Ham tam sayı değeri (ölçekleme mapper'da yapılır).</summary>
    public required long RawValue { get; init; }

    /// <summary>Telegrams içindeki sıra (0'dan başlar) — gerçek Izar Center dışa aktarımındaki 00–09 kayıt sırası.</summary>
    public required int RecordIndex { get; init; }
}
