namespace Westalife.Application.Common.Interfaces;

public interface IAuthService
{
    Task<LoginSession> LoginAsync(string email, string password, CancellationToken cancellationToken);
}

public sealed record LoginSession(
    string Token,
    DateTimeOffset ExpiresOn,
    Guid UserId,
    Guid? TenantId,
    string Role,
    string FullName,
    bool IsSuperAdmin,
    IReadOnlyList<string> Permissions);

public sealed record AccessProfile(string Role, string FullName, bool IsSuperAdmin, IReadOnlyList<string> Permissions);

public interface IAccessProfileReader
{
    Task<AccessProfile> CurrentAsync(CancellationToken cancellationToken);
}
