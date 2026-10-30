using MediatR;
using Microsoft.AspNetCore.Mvc;
using Westalife.Api.Infrastructure;
using Westalife.Application.Features.Payments.Commands.CancelPayment;
using Westalife.Application.Features.Payments.Commands.RecordPayment;
using Westalife.Application.Features.Payments.Queries.GetCashFlow;
using Westalife.Application.Features.Payments.Queries.GetLedger;
using Westalife.Application.Features.Payments.Queries.GetPayments;
using Westalife.Domain.Services.Ledger;

namespace Westalife.Api.Controllers;

/// <summary>
/// Faz 6.8: Tahsilat, Cari Hesap ve Nakit Akış API'si.
/// </summary>
[ApiController]
[Route(ApiConstants.Routes.ApiPrefix + "/payments")]
[Produces("application/json")]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PaymentsController(IMediator mediator) => _mediator = mediator;

    // ── Tahsilat CRUD ─────────────────────────────────────────────────────────

    /// <summary>Daire tahsilat listesi (iptal edilenler dahil).</summary>
    [HttpGet("by-apartment/{apartmentId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPayments(Guid apartmentId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPaymentsQuery(apartmentId), ct);
        return Ok(result);
    }

    /// <summary>Yeni tahsilat kaydeder.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(RecordPaymentResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordPayment(
        [FromBody] RecordPaymentRequest request, CancellationToken ct)
    {
        var command = new RecordPaymentCommand(
            request.ApartmentId,
            request.PaymentDate,
            request.Amount,
            request.Description,
            request.ReceivedBy);

        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetPayments), new { apartmentId = result.ApartmentId }, result);
    }

    /// <summary>Tahsilatı iptal eder (soft-delete).</summary>
    [HttpPost("{paymentId:guid}/cancel")]
    [ProducesResponseType(typeof(CancelPaymentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelPayment(
        Guid paymentId, [FromBody] CancelPaymentRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new CancelPaymentCommand(paymentId, request.Reason), ct);
        return Ok(result);
    }

    // ── Cari Hesap (Ledger) ───────────────────────────────────────────────────

    /// <summary>
    /// Dairenin cari hesabını hesaplar (faiz + FIFO allocation).
    /// asOfDate belirtilmezse bugün kullanılır.
    /// </summary>
    [HttpGet("ledger/{apartmentId:guid}")]
    [ProducesResponseType(typeof(LedgerResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLedger(
        Guid apartmentId,
        [FromQuery] DateOnly? asOfDate,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetLedgerQuery(apartmentId, asOfDate), ct);
        return Ok(result);
    }

    // ── Nakit Akış Raporu ─────────────────────────────────────────────────────

    /// <summary>
    /// Belirtilen dönem için nakit akış raporu.
    /// GELİR = dairelerden tahsilat (Payment.PaymentDate).
    /// GİDER = doğalgaz firmalarına ödeme (GasInvoice.PaidOn).
    /// </summary>
    [HttpGet("cash-flow")]
    [ProducesResponseType(typeof(CashFlowResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCashFlow(
        [FromQuery] DateOnly dateFrom,
        [FromQuery] DateOnly dateTo,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCashFlowQuery(dateFrom, dateTo), ct);
        return Ok(result);
    }
}

// ── Request DTO'ları ──────────────────────────────────────────────────────────

public sealed record RecordPaymentRequest(
    Guid ApartmentId,
    DateOnly PaymentDate,
    decimal Amount,
    string? Description,
    string? ReceivedBy);

public sealed record CancelPaymentRequest(string Reason);
