namespace Westalife.Application.Common.Interfaces;

public interface IAccountSecurity
{
    Task ChangeOwnPasswordAsync(string currentPassword, string newPassword, CancellationToken cancellationToken);

    Task ResetStaffPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken);

    Task ResetResidentPasswordAsync(Guid apartmentId, string newPassword, CancellationToken cancellationToken);

    Task<IReadOnlyList<StaffAccountItem>> ListStaffAsync(CancellationToken cancellationToken);
}

public sealed record StaffAccountItem(Guid Id, string FullName, string Email, string RoleName);
