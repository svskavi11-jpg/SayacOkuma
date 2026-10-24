namespace Westalife.Application.Common.Models;

/// <summary>Bir okuma seansının toplu sonucu. Tek sayacın hatası seansı bozmaz (kısmi başarı modeli).</summary>
public sealed record MeterReadBatch
{
    public required DateTimeOffset StartedAtUtc { get; init; }

    public required IReadOnlyList<MeterReadResult> Results { get; init; }

    public int SuccessCount => Results.Count(r => r.IsSuccess);

    public int FailureCount => Results.Count(r => !r.IsSuccess);

    public bool IsFullSuccess => FailureCount == 0 && Results.Count > 0;
}
