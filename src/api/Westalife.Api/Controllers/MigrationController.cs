using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Westalife.Api.Infrastructure;
using Westalife.Application.Features.Admin.Migration;
using Westalife.Domain.Constants;

namespace Westalife.Api.Controllers;

[ApiController]
[Authorize(Roles = DomainConstants.Auth.SuperAdmin)]
[Route(ApiConstants.Routes.ApiPrefix + "/admin/migration")]
[Produces("application/json")]
public sealed class MigrationController : ControllerBase
{
    private readonly IMediator _mediator;

    public MigrationController(IMediator mediator) => _mediator = mediator;

    [HttpGet("status")]
    public async Task<MaintenanceStatus> Status(CancellationToken cancellationToken) =>
        await _mediator.Send(new GetMaintenanceStatusQuery(), cancellationToken);

    [HttpPost("maintenance")]
    public async Task<MaintenanceStatus> Set(SetMaintenanceBody body, CancellationToken cancellationToken) =>
        await _mediator.Send(new SetMaintenanceModeCommand(body.Enabled), cancellationToken);

    public sealed record SetMaintenanceBody(bool Enabled);
}
