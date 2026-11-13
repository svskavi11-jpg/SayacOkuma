using FluentValidation;
using MediatR;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Admin.Tenants;

namespace Westalife.Application.Features.Auth.ResetUserPassword;

public enum PasswordResetTarget
{
    Staff,
    Resident
}

public sealed record ResetUserPasswordCommand(Guid TargetId, string NewPassword, PasswordResetTarget Target) : IRequest;

public sealed class ResetUserPasswordCommandValidator : AbstractValidator<ResetUserPasswordCommand>
{
    public ResetUserPasswordCommandValidator()
    {
        RuleFor(command => command.TargetId).NotEmpty();
        RuleFor(command => command.NewPassword).ApplyPasswordRules();
    }
}

public sealed class ResetUserPasswordCommandHandler : IRequestHandler<ResetUserPasswordCommand>
{
    private readonly IAccountSecurity _accounts;

    public ResetUserPasswordCommandHandler(IAccountSecurity accounts) => _accounts = accounts;

    public Task Handle(ResetUserPasswordCommand request, CancellationToken cancellationToken) =>
        request.Target == PasswordResetTarget.Resident
            ? _accounts.ResetResidentPasswordAsync(request.TargetId, request.NewPassword, cancellationToken)
            : _accounts.ResetStaffPasswordAsync(request.TargetId, request.NewPassword, cancellationToken);
}
