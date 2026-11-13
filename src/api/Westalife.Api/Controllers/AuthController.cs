using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Auth.ChangePassword;
using Westalife.Application.Features.Auth.Login;

namespace Westalife.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IAccessProfileReader _access;

    public AuthController(IMediator mediator, IAccessProfileReader access)
    {
        _mediator = mediator;
        _access = access;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginSession), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<LoginSession> Login(LoginCommand command, CancellationToken cancellationToken) =>
        await _mediator.Send(command, cancellationToken);

    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ChangePassword(ChangePasswordBody body, CancellationToken cancellationToken)
    {
        await _mediator.Send(new ChangePasswordCommand(body.CurrentPassword, body.NewPassword), cancellationToken);
        return NoContent();
    }

    public sealed record ChangePasswordBody(string CurrentPassword, string NewPassword);

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(AccessProfile), StatusCodes.Status200OK)]
    public async Task<AccessProfile> Me(CancellationToken cancellationToken) =>
        await _access.CurrentAsync(cancellationToken);
}
