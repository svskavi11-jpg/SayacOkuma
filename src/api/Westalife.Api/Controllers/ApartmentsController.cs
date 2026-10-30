using Westalife.Application.Features.Apartments.Commands.AddApartments;
using Westalife.Application.Features.Apartments.Commands.SetOpeningBalance;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Westalife.Api.Infrastructure;
using Westalife.Application.Features.Apartments.Queries.GetApartments;
using Westalife.Application.Features.Apartments.Queries.GetApartmentConsumptionAnalysis;
using Westalife.Application.Features.Apartments.Queries.GetApartmentOwnerHistory;
using Westalife.Application.Features.Apartments.Commands.UpdateApartmentDimension;
using Westalife.Application.Features.Apartments.Commands.UpdateApartmentOwner;
using Westalife.Application.Features.Apartments.Commands.ImportMetersFromExcel;
using Westalife.Infrastructure.Reporting.Excel;

namespace Westalife.Api.Controllers;

[ApiController]
[Route(ApiConstants.Routes.ApiPrefix + "/apartments")]
[Produces("application/json")]
public class ApartmentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ApartmentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Aktif daireler + dönemsel m² (SCD) + bağlı sayaçlar (Faz 5 grid zemin).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ApartmentListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetApartments(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetApartmentsQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Daire m²'sini günceller — SCD Type 2: eski kayıt kapatılır, yenisi açılır (veri ezme yok).
    /// </summary>
    [HttpPut("{id:guid}/dimensions")]
    [ProducesResponseType(typeof(UpdateApartmentDimensionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateDimension(
        Guid id, [FromBody] UpdateApartmentDimensionCommand command, CancellationToken cancellationToken)
    {
        var updated = await _mediator.Send(command with { ApartmentId = id }, cancellationToken);
        return Ok(updated);
    }

    /// <summary>
    /// Kat malikini günceller (Etap 6.7 — SCD Type 2): eski malik kaydı kapatılır, yenisi açılır;
    /// geçmiş korunur. Ad değişmediyse Changed=false döner (no-op).
    /// </summary>
    [HttpPut("{id:guid}/owner")]
    [ProducesResponseType(typeof(UpdateApartmentOwnerResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateOwner(
        Guid id, [FromBody] UpdateApartmentOwnerCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command with { ApartmentId = id }, cancellationToken);
        return Ok(result);
    }

    /// <summary>Dairenin kat maliki geçmişi (Etap 6.7 — SCD Type 2, kapalı kayıtlar dahil).</summary>
    [HttpGet("{id:guid}/owner/history")]
    [ProducesResponseType(typeof(IReadOnlyList<ApartmentOwnerHistoryItem>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOwnerHistory(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetApartmentOwnerHistoryQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Daire tüketim analizi (Etap 6.7): yıl VEYA tarih aralığı filtreli aylık tüketim serisi,
    /// toplam ve aylık ortalama. year verilmezse from/to; ikisi de verilmezse bu yıl kullanılır.
    /// </summary>
    [HttpGet("{id:guid}/consumption-analysis")]
    [ProducesResponseType(typeof(ApartmentConsumptionAnalysisResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConsumptionAnalysis(
        Guid id, [FromQuery] int? year, [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetApartmentConsumptionAnalysisQuery(id, year, from, to), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Excel şablonunu indirir (Kapı No, Sayaç Seri No, M-Bus Adresi + opsiyonel Başlangıç Endeksi,
    /// Başlangıç Tarihi, Kat Maliki Adı Soyadı, m² sütunları — Etap 6.7).
    /// </summary>
    [HttpGet("import-template")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetImportTemplate()
    {
        var template = MeterImportExcelTemplate.Generate();
        return File(template,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "Sayac-Import-Sablon.xlsx");
    }

    /// <summary>
    /// Excel ile toplu sayaç içe aktarma. Başlıklar dinamik eşleştirilir; satır bazlı
    /// hatalar raporlanır. Opsiyonel başlangıç endeksi sütunu doluysa IsInitialReading okuması da oluşturulur.
    /// </summary>
    [HttpPost("import-meters-excel")]
    [ProducesResponseType(typeof(ImportMetersFromExcelResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImportMetersFromExcel(
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

        var result = await _mediator.Send(new ImportMetersFromExcelCommand(content), cancellationToken);
        return Ok(result);
    }

    /// <summary>Toplu daire ekleme: Count kadar yeni daire, mevcut son kapı numarasından devam eder.</summary>
    [HttpPost("bulk")]
    [ProducesResponseType(typeof(AddApartmentsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddApartments(
        [FromBody] AddApartmentsCommand command, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(command, cancellationToken));

    /// <summary>Faz 6.8: devir (açılış) bakiyesi girişi. + borçlu, - alacaklı; 0 = temizle.</summary>
    [HttpPut("{id:guid}/opening-balance")]
    [ProducesResponseType(typeof(SetOpeningBalanceResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetOpeningBalance(
        Guid id, [FromBody] SetOpeningBalanceCommand command, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(command with { ApartmentId = id }, cancellationToken));
}
