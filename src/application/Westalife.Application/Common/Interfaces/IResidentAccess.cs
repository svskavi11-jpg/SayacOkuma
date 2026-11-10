namespace Westalife.Application.Common.Interfaces;

/// <summary>Giriş yapan sakinin dairesi. Her istekte kullanıcı kaydından okunur.</summary>
public interface IResidentAccess
{
    Task<ResidentScope> RequireAsync(CancellationToken cancellationToken);
}

public sealed record ResidentScope(Guid UserId, Guid TenantId, Guid ApartmentId);
