using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Westalife.Api.Infrastructure;
using Westalife.Domain.Constants;
using Westalife.Application.Features.Notifications;

namespace Westalife.Api.Controllers;

[ApiController]
[Route(ApiConstants.Routes.ApiPrefix + "/notifications")]
[Produces("application/json")]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public NotificationsController(IMediator mediator) => _mediator = mediator;

    [HasPermission(Permissions.Notifications.View)]
    [HttpGet("placeholders")]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationPlaceholderItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlaceholders(CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetNotificationPlaceholdersQuery(), cancellationToken));

    [HasPermission(Permissions.Notifications.View)]
    [HttpGet("templates")]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationTemplateItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTemplates(CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetNotificationTemplatesQuery(), cancellationToken));

    [HasPermission(Permissions.Notifications.Send)]
    [HttpPut("templates")]
    [ProducesResponseType(typeof(NotificationTemplateItem), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpsertTemplate(
        [FromBody] UpsertNotificationTemplateCommand command, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(command, cancellationToken));

    [HasPermission(Permissions.Notifications.View)]
    [HttpGet("logs")]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationLogItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLogs([FromQuery] int take = 200, CancellationToken cancellationToken = default) =>
        Ok(await _mediator.Send(new GetNotificationLogsQuery(take), cancellationToken));

    [HasPermission(Permissions.Notifications.Send)]
    [HttpPost("preview")]
    [ProducesResponseType(typeof(NotificationPreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Preview(
        [FromBody] PreviewNotificationQuery query, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(query, cancellationToken));

    [HasPermission(Permissions.Notifications.Send)]
    [HttpPost]
    [ProducesResponseType(typeof(SendManualNotificationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Send(
        [FromBody] SendManualNotificationCommand command, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(command, cancellationToken));
}
