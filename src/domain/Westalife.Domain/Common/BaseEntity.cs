namespace Westalife.Domain.Common;

/// <summary>
/// Tüm varlıkların temel sınıfı. Zaman bilgileri UTC (timestamptz) tutulur.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset CreatedOn { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastModifiedOn { get; set; }

    /// <summary>Kaydı oluşturan kişi. Eski satırlarda boş kalır.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Kaydı son değiştiren kişi. Eski satırlarda boş kalır.</summary>
    public string? LastModifiedBy { get; set; }
}
