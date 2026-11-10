namespace Westalife.Application.Common.Interfaces;

/// <summary>İsteğin bağlı olduğu site. Süper yönetici filtreyi aşabilir.</summary>
public interface ITenantContext
{
    Guid TenantId { get; }

    /// <summary>Doğruysa site filtresi uygulanmaz ve tüm siteler görünür.</summary>
    bool BypassTenantFilter { get; }
}
