using FluentValidation;
using MediatR;
using Westalife.Application.Common.Interfaces;

namespace Westalife.Application.Features.Auth.Login;

public sealed record LoginCommand(string Email, string Password) : IRequest<LoginSession>;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().WithMessage("E-posta zorunludur.");
        RuleFor(c => c.Password).NotEmpty().WithMessage("Şifre zorunludur.");
    }
}

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, LoginSession>
{
    private readonly IAuthService _auth;

    public LoginCommandHandler(IAuthService auth) => _auth = auth;

    public Task<LoginSession> Handle(LoginCommand request, CancellationToken cancellationToken) =>
        _auth.LoginAsync(request.Email, request.Password, cancellationToken);
}
