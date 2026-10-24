using MediatR;
using Microsoft.AspNetCore.Mvc;
using Westalife.Api.Infrastructure;
using Westalife.Application.Features.MeterReadings.Commands.AddManualMeterReading;
using Westalife.Application.Features.MeterReadings.Commands.ImportReadingsFromExcel;
using Westalife.Application.Features.MeterReadings.Commands.TriggerMeterReadingCycle;
using Westalife.Application.Features.MeterReadings.Commands.UpdateMeterReading;
using Westalife.Application.Features.MeterReadings.Queries.GetMeterReadingAuditLog;
using Westalife.Application.Features.MeterReadings.Queries.GetMeterReadings;
using Westalife.Infrastructure.Reporting.Excel;

namespace Westalife.Api.Controllers;

[ApiController]
[Route(ApiConstants.Routes.ApiPrefix + "/meter-readings")]
[Produces("application/json")]
public class MeterReadingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public MeterReadingsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// M-Bus okuma döngüsünü tetikler (USB/TCP/Simülatör — appsettings "Metering:ConnectionType").
    /// Kısmi başarı modeli: tek sayacın hatası döngüyü bozmaz; özet raporlanır.
    /// </summary>
    [HttpPost("trigger")]
    [ProducesResponseType(typeof(MeterReadingCycleResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> TriggerReadingCycle(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new TriggerMeterReadingCycleCommand(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Manuel okuma girişi (Etap 6.6 — saha arızası B planı). Kayıt Source=ManualEntry ile yapılır.
    /// Dönem dışı tarih engellenmez (uyarı döner); kilitli dönem ve endeks gerilemesi reddedilir.
    /// </summary>
    [HttpPost("manual")]
    [ProducesResponseType(typeof(AddManualMeterReadingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddManualReading(
        [FromBody] AddManualMeterReadingCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Sayaç okuma listesi (Etap 6.7 — okuma düzeltme Modal'ının veri kaynağı).
    /// Her satır kilitli dönem durumu ve denetim kaydı sayısıyla döner.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MeterReadingListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReadings(
        [FromQuery] Guid meterId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetMeterReadingsQuery(meterId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Okuma düzeltme (Etap 6.7 — tek veya toplu). Her düzeltmede zorunlu "Değişiklik Nedeni"
    /// alınır; tüketimler zincirleme yeniden hesaplanır; MeterReadingAuditLog'a denetim kaydı yazılır.
    /// </summary>
    [HttpPut("corrections")]
    [ProducesResponseType(typeof(UpdateMeterReadingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateReadings(
        [FromBody] UpdateMeterReadingCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>Bir okumanın düzeltme (denetim) geçmişi (Etap 6.7).</summary>
    [HttpGet("{readingId:guid}/audit-log")]
    [ProducesResponseType(typeof(IReadOnlyList<MeterReadingAuditLogItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLog(Guid readingId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetMeterReadingAuditLogQuery(readingId), cancellationToken);
        return Ok(result);
    }

    /// <summary>Okuma içe aktarma şablonunu indirir (Kapı No, Okuma Tarihi, Endeks kWh).</summary>
    [HttpGet("import-template")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetImportTemplate()
    {
        var template = ReadingImportExcelTemplate.Generate();
        return File(template,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Okuma-Import-Sablon.xlsx");
    }

    /// <summary>
    /// Excel ile toplu okuma içe aktarma (Etap 6.6). Kayıtlar Source=Import ile yazılır;
    /// kısmi başarı modeli: hatalı satırlar raporlanır, geçerli satırlar kaydedilir.
    /// </summary>
    [HttpPost("import-excel")]
    [ProducesResponseType(typeof(ImportReadingsFromExcelResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportReadingsFromExcel(
        IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = ApiConstants.Titles.ValidationFailed,
                Detail = "Excel dosyası yüklenmedi veya boş.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        using var stream = file.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        var content = memory.ToArray();

        var result = await _mediator.Send(new ImportReadingsFromExcelCommand(content), cancellationToken);
        return Ok(result);
    }
}
