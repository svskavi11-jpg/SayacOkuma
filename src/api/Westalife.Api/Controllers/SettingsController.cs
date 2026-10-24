using MediatR;
using Microsoft.AspNetCore.Mvc;
using Westalife.Api.Infrastructure;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Settings.GetAppSettings;
using Westalife.Application.Features.Settings.TestMbusConnection;
using Westalife.Application.Features.Settings.UpdateAppSettings;

namespace Westalife.Api.Controllers;

[ApiController]
[Route(ApiConstants.Routes.ApiPrefix + "/settings")]
[Produces("application/json")]
public class SettingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SettingsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Sistem ayarlarını döndürür (şirket adı, bina adı, 30/70 oranları, M-Bus donanım ayarları).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(AppSettingsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAppSettingsQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Sistem ayarlarını günceller. Sabit + Tüketim oranı tam %100 olmalıdır;
    /// aynı transaction'da Building tablosuna aynalanır (bina adı + sabit gider oranı).
    /// M-Bus donanım ayarları da buradan güncellenir (bağlantı tipi, TCP/USB parametreleri).
    /// </summary>
    [HttpPut]
    [ProducesResponseType(typeof(AppSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateSettings(
        [FromBody] UpdateAppSettingsCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// "Bağlantıyı Test Et": DB'deki M-Bus ayarlarıyla Izar Center'a (TCP) / COM portuna (USB)
    /// ulaşılmaya çalışılır; Simulator her zaman başarılı döner.
    /// </summary>
    [HttpPost("test-mbus-connection")]
    [ProducesResponseType(typeof(MbusConnectionTestResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> TestMbusConnection(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new TestMbusConnectionCommand(), cancellationToken);
        return Ok(result);
    }
}
