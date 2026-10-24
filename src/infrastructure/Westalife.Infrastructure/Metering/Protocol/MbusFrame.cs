namespace Westalife.Infrastructure.Metering.Protocol;

/// <summary>Doğrulanmış bir M-Bus yanıt frame'i.</summary>
public sealed record MbusFrame
{
    public required byte CField { get; init; }

    public required byte Address { get; init; }

    public required byte CiField { get; init; }

    /// <summary>Veri bölümü (C/A/CI sonrası, checksum'dan önceki ham kayıtlar).</summary>
    public required byte[] Data { get; init; }

    public required string RawHex { get; init; }
}
