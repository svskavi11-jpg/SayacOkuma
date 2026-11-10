using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Westalife.Api.Infrastructure;
using Westalife.Application.Features.ResidentPortal;
using Westalife.Domain.Constants;

namespace Westalife.Api.Controllers;

/// <summary>Sakin portalı. Daire kimliği istemciden alınmaz; kullanıcı kaydından okunur.</summary>
[ApiController]
[Authorize(Roles = DomainConstants.Auth.Resident)]
[Route(ApiConstants.Routes.ApiPrefix + "/resident")]
[Produces("application/json")]
public class ResidentController : ControllerBase
{
    private readonly IMediator _mediator;

    public ResidentController(IMediator mediator) => _mediator = mediator;

    [HttpGet("ledger")]
    [ProducesResponseType(typeof(ResidentLedgerResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ResidentLedgerResponse> Ledger(CancellationToken cancellationToken) =>
        await _mediator.Send(new GetResidentLedgerQuery(), cancellationToken);

    [HttpGet("invoices")]
    [ProducesResponseType(typeof(IReadOnlyList<ResidentInvoiceItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IReadOnlyList<ResidentInvoiceItem>> Invoices(CancellationToken cancellationToken) =>
        await _mediator.Send(new GetResidentInvoicesQuery(), cancellationToken);

    [HttpGet("invoices/{id:guid}/pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Pdf(Guid id, [FromQuery] string? kind, CancellationToken cancellationToken)
    {
        if (!string.Equals(kind, "heating", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = ApiConstants.Titles.ValidationFailed,
                Detail = "PDF yalnızca ısınma bildirimi için hazırdır.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var file = await _mediator.Send(new GetResidentHeatingPdfQuery(id), cancellationToken);
        return File(file.Content, "application/pdf", file.FileName);
    }
}
