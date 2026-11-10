using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Westalife.Api.Infrastructure;
using Westalife.Application.Features.Admin.Tenants;
using Westalife.Application.Features.Roles;
using Westalife.Domain.Constants;

namespace Westalife.Api.Controllers;

[ApiController]
[Authorize(Roles = DomainConstants.Auth.SuperAdmin)]
[Route(ApiConstants.Routes.ApiPrefix + "/admin/tenants")]
[Produces("application/json")]
public class AdminTenantsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AdminTenantsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IReadOnlyList<TenantListItem>> List(CancellationToken cancellationToken) =>
        await _mediator.Send(new GetTenantsQuery(), cancellationToken);

    [HttpPost]
    public async Task<ActionResult<Guid>> Create(CreateTenantCommand command, CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(command, cancellationToken);
        return Ok(id);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateTenantBody body, CancellationToken cancellationToken)
    {
        await _mediator.Send(new UpdateTenantCommand(id, body.Name, body.Address), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, SetTenantStatusBody body, CancellationToken cancellationToken)
    {
        await _mediator.Send(new SetTenantStatusCommand(id, body.IsActive), cancellationToken);
        return NoContent();
    }

    [HttpGet("{id:guid}/roles")]
    public async Task<IReadOnlyList<AssignableRoleItem>> Roles(Guid id, CancellationToken cancellationToken) =>
        await _mediator.Send(new ListAssignableRolesQuery(id), cancellationToken);

    [HttpPost("{id:guid}/users")]
    public async Task<ActionResult<Guid>> AddUser(Guid id, AddTenantUserBody body, CancellationToken cancellationToken)
    {
        var userId = await _mediator.Send(
            new AddTenantUserCommand(id, body.FullName, body.Email, body.Password, body.RoleId),
            cancellationToken);
        return Ok(userId);
    }

    public sealed record UpdateTenantBody(string Name, string? Address);

    public sealed record SetTenantStatusBody(bool IsActive);

    public sealed record AddTenantUserBody(string FullName, string Email, string Password, Guid RoleId);
}
