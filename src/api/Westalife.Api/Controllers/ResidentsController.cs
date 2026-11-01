using MediatR;
using Microsoft.AspNetCore.Mvc;
using Westalife.Api.Infrastructure;
using Westalife.Application.Features.Residents.Queries.GetFormerResidentsWithDebt;

namespace Westalife.Api.Controllers;

/// <summary>
/// Faz 7.1: Sakin API'si — geçmiş sakinler (borçlu eski kiracılar/ev sahipleri) ve
/// Hibrit Model (KMK m.22) rücu uyarıları.
/// </summary>
[ApiController]
[Route(ApiConstants.Routes.ApiPrefix + "/residents")]
[Produces("application/json")]
public class ResidentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ResidentsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Dairede oturmayı bitirmiş (ValidTo dolu) ve cari hesabında kalan borcu olan eski sakinler.
    /// Kiracı satırlarında RucuPossible=true + EvSahibiName döner (müteselsil sorumluluk uyarısı).
    /// </summary>
    [HttpGet("former-with-debt")]
    [ProducesResponseType(typeof(IReadOnlyList<FormerResidentDebtItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFormerWithDebt(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetFormerResidentsWithDebtQuery(), ct);
        return Ok(result);
    }
}
