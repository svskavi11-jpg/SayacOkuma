using FluentValidation;
using MediatR;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Admin.Tenants;

namespace Westalife.Application.Features.Auth.ChangePassword;

public sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword) : IRequest;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(command => command.CurrentPassword).NotEmpty().WithMessage("Mevcut şifre zorunludur.");
        RuleFor(command => command.NewPassword).ApplyPasswordRules();
    }
}

public sealed class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand>
{
    private readonly IAccountSecurity _accounts;

    public ChangePasswordCommandHandler(IAccountSecurity accounts) => _accounts = accounts;

    public Task Handle(ChangePasswordCommand request, CancellationToken cancellationToken) =>
        _accounts.ChangeOwnPasswordAsync(request.CurrentPassword, request.NewPassword, cancellationToken);
}
