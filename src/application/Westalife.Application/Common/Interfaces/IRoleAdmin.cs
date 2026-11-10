namespace Westalife.Application.Common.Interfaces;

public sealed record RoleListItem(
    Guid Id,
    string Name,
    bool IsSystem,
    int UserCount,
    IReadOnlyList<string> Permissions);

public interface IRoleAdmin
{
    Task<IReadOnlyList<RoleListItem>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<Guid> CreateAsync(
        Guid tenantId,
        string displayName,
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken);

    Task UpdateAsync(
        Guid tenantId,
        Guid roleId,
        string displayName,
        IReadOnlyList<string> permissions,
        CancellationToken cancellationToken);

    Task DeleteAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken);
}
