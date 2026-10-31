using MediatR;
using Microsoft.AspNetCore.Mvc;
using Westalife.Api.Infrastructure;
using Westalife.Application.Features.Aidat.Commands.CreateAidatInvoice;
using Westalife.Application.Features.Aidat.Commands.CreateAidatTariff;
using Westalife.Application.Features.Aidat.Commands.UpdateAidatTariff;
using Westalife.Application.Features.Aidat.Queries.GetAidatInvoicePreview;
using Westalife.Application.Features.Aidat.Queries.GetAidatInvoices;
using Westalife.Application.Features.Aidat.Queries.GetAidatTariffs;
using Westalife.Domain.Entities;

namespace Westalife.Api.Controllers;

/// <summary>
/// Faz 6.9: Aidat (ortak gider) API'si — tarife CRUD, tahakkuk oluşturma, önizleme ve dönem listesi.
/// </summary>
[ApiController]
[Route(ApiConstants.Routes.ApiPrefix + "/aidat")]
[Produces("application/json")]
public class AidatController : ControllerBase
{
    private readonly IMediator _mediator;

    public AidatController(IMediator mediator) => _mediator = mediator;

    // ── Aidat Tarifeleri ──────────────────────────────────────────────────────

    /// <summary>Aidat tarife listesi (atanmış daire sayısı ile).</summary>
    [HttpGet("tariffs")]
    [ProducesResponseType(typeof(IReadOnlyList<Westalife.Application.Features.Aidat.Queries.GetAidatTariffs.AidatTariffListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTariffs([FromQuery] bool? isActive, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAidatTariffsQuery(isActive), ct);
        return Ok(result);
    }

    /// <summary>Yeni aidat tarifesı tanımlar (ad benzersiz olmalıdır).</summary>
    [HttpPost("tariffs")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTariff(
        [FromBody] CreateAidatTariffRequest request, CancellationToken ct)
    {
        var tariffId = await _mediator.Send(
            new CreateAidatTariffCommand(request.Name, request.SquareMeters, request.Amount), ct);
        return CreatedAtAction(nameof(GetTariffs), new { tariffId }, tariffId);
    }

    /// <summary>Aidat tarifesini günceller (geçmiş tahakkuk satırları etkilenmez).</summary>
    [HttpPut("tariffs/{id:guid}")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateTariff(
        Guid id, [FromBody] UpdateAidatTariffRequest request, CancellationToken ct)
    {
        var tariffId = await _mediator.Send(
            new UpdateAidatTariffCommand(id, request.Name, request.SquareMeters, request.Amount), ct);
        return Ok(tariffId);
    }

    // ── Aidat Tahakkukları ────────────────────────────────────────────────────

    /// <summary>Geçmiş aidat dönemleri (tahsilat oranlarıyla).</summary>
    [HttpGet("invoices")]
    [ProducesResponseType(typeof(IReadOnlyList<Westalife.Application.Features.Aidat.Queries.GetAidatInvoices.AidatInvoiceListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvoices([FromQuery] DateOnly? asOfDate, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAidatInvoicesQuery(asOfDate), ct);
        return Ok(result);
    }

    /// <summary>
    /// Yeni aidat tahakkuku oluşturur — aktif tüm daireleri gezip borç satırları yazar.
    /// Tarifesiz daireler atlanır ve sonuçta raporlanır.
    /// </summary>
    [HttpPost("invoices")]
    [ProducesResponseType(typeof(CreateAidatInvoiceResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateInvoice(
        [FromBody] CreateAidatInvoiceRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateAidatInvoiceCommand(
                request.PeriodStart,
                request.PeriodEnd,
                request.DueDate,
                request.DistributionMethod,
                request.EqualAmount,
                request.Note),
            ct);
        return CreatedAtAction(nameof(GetInvoices), new { id = result.AidatInvoiceId }, result);
    }

    /// <summary>
    /// Tahakkuk oluşturmadan önce daire bazında borç önizlemesi (kayıt yazmaz).
    /// </summary>
    [HttpPost("invoices/preview")]
    [ProducesResponseType(typeof(AidatInvoicePreviewResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewInvoice(
        [FromBody] CreateAidatInvoiceRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new GetAidatInvoicePreviewQuery(
                request.PeriodStart,
                request.PeriodEnd,
                request.DueDate,
                request.DistributionMethod,
                request.EqualAmount),
            ct);
        return Ok(result);
    }
}

// ── Request DTO'ları ──────────────────────────────────────────────────────────

public sealed record CreateAidatTariffRequest(
    string Name,
    decimal SquareMeters,
    decimal Amount);

public sealed record UpdateAidatTariffRequest(
    string Name,
    decimal SquareMeters,
    decimal Amount);

public sealed record CreateAidatInvoiceRequest(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly DueDate,
    AidatDistributionMethod DistributionMethod,
    decimal? EqualAmount = null,
    string? Note = null);
