using MediatR;
using Microsoft.AspNetCore.Mvc;
using Westalife.Api.Infrastructure;
using Westalife.Application.Features.Residents.Documents;
using Westalife.Application.Features.Residents.Queries.GetFormerResidentsWithDebt;
using Westalife.Application.Features.Residents.Queries.GetResidentHistory;
using Westalife.Domain.Constants;
using Westalife.Domain.Enums;

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

    /// <summary>
    /// Daire sakinlerinin tamamı: oturmakta olanlar ve çıkmış olanlar.
    /// Tarih filtresi, oturma aralığının seçilen günlerle kesişmesine bakar.
    /// </summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(IReadOnlyList<ResidentHistoryItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory(
        [FromQuery] string? doorNumber,
        [FromQuery] string? name,
        [FromQuery] ResidentType? residentType,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        var result = await _mediator.Send(
            new GetResidentHistoryQuery(doorNumber, name, residentType, from, to), ct);
        return Ok(result);
    }

    [HttpGet("documents")]
    [ProducesResponseType(typeof(IReadOnlyList<ResidentDocumentItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDocuments([FromQuery] Guid apartmentId, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetResidentDocumentsQuery(apartmentId), ct));

    [HttpPost("{ownerId:guid}/documents")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(DomainConstants.ResidentDocument.MaxBytes)]
    [ProducesResponseType(typeof(ResidentDocumentItem), StatusCodes.Status200OK)]
    public async Task<IActionResult> Upload(
        Guid ownerId, IFormFile file, [FromForm] ResidentDocumentType documentType, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest();
        }

        using var memory = new MemoryStream();
        await file.CopyToAsync(memory, ct);
        var result = await _mediator.Send(
            new UploadResidentDocumentCommand(ownerId, documentType, file.FileName, memory.ToArray()), ct);
        return Ok(result);
    }

    [HttpGet("documents/{id:guid}/file")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var file = await _mediator.Send(new OpenResidentDocumentQuery(id), ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpDelete("documents/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteDocument(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteResidentDocumentCommand(id), ct);
        return NoContent();
    }
}
