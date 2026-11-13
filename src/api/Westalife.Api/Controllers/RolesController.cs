using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Westalife.Api.Infrastructure;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Admin.Tenants;
using Westalife.Application.Features.Auth.ResetUserPassword;
using Westalife.Application.Features.Roles;
using Westalife.Domain.Constants;

namespace Westalife.Api.Controllers;

[ApiController]
[Route("api/v1/roles")]
public sealed class RolesController : ControllerBase
{
    private readonly IMediator _mediator;

    public RolesController(IMediator mediator) => _mediator = mediator;

    [HasPermission(Permissions.Roles.View)]
    [HttpGet]
    public async Task<IReadOnlyList<RoleListItem>> List(CancellationToken cancellationToken) =>
        await _mediator.Send(new ListRolesQuery(), cancellationToken);

    [HasPermission(Permissions.Roles.View)]
    [HttpGet("permissions")]
    public async Task<IReadOnlyList<PermissionCatalogItem>> Catalog(CancellationToken cancellationToken) =>
        await _mediator.Send(new ListPermissionCatalogQuery(), cancellationToken);

    [HasPermission(Permissions.Roles.Manage)]
    [HttpPost]
    public async Task<ActionResult<Guid>> Create(SaveRoleBody body, CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new CreateRoleCommand(body.Name, body.Permissions), cancellationToken);
        return Created($"/api/v1/roles/{id}", id);
    }

    [HasPermission(Permissions.Roles.Manage)]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, SaveRoleBody body, CancellationToken cancellationToken)
    {
        await _mediator.Send(new UpdateRoleCommand(id, body.Name, body.Permissions), cancellationToken);
        return NoContent();
    }

    [HasPermission(Permissions.Roles.Manage)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteRoleCommand(id), cancellationToken);
        return NoContent();
    }

    [HasPermission(Permissions.Settings.ManageUsers)]
    [HttpGet("staff")]
    public async Task<IReadOnlyList<StaffAccountItem>> Staff(CancellationToken cancellationToken) =>
        await _mediator.Send(new ListStaffQuery(), cancellationToken);

    [HasPermission(Permissions.Settings.ManageUsers)]
    [HttpPost("staff/{userId:guid}/reset-password")]
    public async Task<IActionResult> ResetStaffPassword(
        Guid userId, ResetPasswordBody body, CancellationToken cancellationToken)
    {
        await _mediator.Send(
            new ResetUserPasswordCommand(userId, body.NewPassword, PasswordResetTarget.Staff),
            cancellationToken);
        return NoContent();
    }

    [HasPermission(Permissions.Settings.ManageUsers)]
    [HttpPost("staff")]
    public async Task<ActionResult<Guid>> AddStaff(AddStaffBody body, CancellationToken cancellationToken)
    {
        var tenantId = HttpContext.RequestServices.GetRequiredService<ITenantContext>().TenantId;
        var id = await _mediator.Send(
            new AddTenantUserCommand(tenantId, body.FullName, body.Email, body.Password, body.RoleId),
            cancellationToken);
        return Created($"/api/v1/roles/staff/{id}", id);
    }

    public sealed record AddStaffBody(string FullName, string Email, string Password, Guid RoleId);

    public sealed record ResetPasswordBody(string NewPassword);
}
