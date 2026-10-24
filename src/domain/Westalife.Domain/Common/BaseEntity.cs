namespace Westalife.Domain.Common;

/// <summary>
/// Tüm varlıkların temel sınıfı. Zaman bilgileri UTC (timestamptz) tutulur.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastModifiedOn { get; set; }
}
