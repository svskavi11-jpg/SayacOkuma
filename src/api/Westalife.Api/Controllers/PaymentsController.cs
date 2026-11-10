using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Westalife.Api.Infrastructure;
using Westalife.Domain.Constants;
using Westalife.Application.Features.Payments.Commands.CancelPayment;
using Westalife.Application.Features.Payments.Commands.CreateBulkPayment;
using Westalife.Application.Features.Payments.Commands.DeletePayment;
using Westalife.Application.Features.Payments.Commands.ParseBankStatement;
using Westalife.Application.Features.Payments.Commands.RecordPayment;
using Westalife.Application.Features.Payments.Commands.UpdatePayment;
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
    [HasPermission(Permissions.Payments.View)]
    [HttpGet("by-apartment/{apartmentId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<PaymentListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPayments(Guid apartmentId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPaymentsQuery(apartmentId), ct);
        return Ok(result);
    }

    /// <summary>Yeni tahsilat kaydeder.</summary>
    [HasPermission(Permissions.Payments.Create)]
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
            request.ReceivedBy,
            request.TargetDebtType,
            request.TargetOwnerId,
            request.BankAccountId);

        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GetPayments), new { apartmentId = result.ApartmentId }, result);
    }

    /// <summary>
    /// Banka ekstresini (.xlsx / .csv) ayrıştırır, daire ve borç türü tahminleriyle
    /// önizleme listesi döner. Veritabanına kayıt YAZMAZ.
    /// </summary>
    [HasPermission(Permissions.Payments.Create)]
    [HttpPost("bank-statement/parse")]
    [ProducesResponseType(typeof(BankStatementParseResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ParseBankStatement(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = ApiConstants.Titles.ValidationFailed,
                Detail = "Ekstre dosyası yüklenmedi veya boş.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        await using var stream = file.OpenReadStream();
        var result = await _mediator.Send(new ParseBankStatementCommand(stream, file.FileName), ct);
        return Ok(result);
    }

    /// <summary>
    /// Önizleme ekranında onaylanan banka ekstresi satırlarını toplu tahsilat olarak kaydeder.
    /// </summary>
    [HasPermission(Permissions.Payments.Create)]
    [HttpPost("bulk")]
    [ProducesResponseType(typeof(CreateBulkPaymentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateBulkPayments(
        [FromBody] CreateBulkPaymentCommand command,
        CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>Yanlış girilen tahsilatı düzeltir. Mahsup cari hesapta yeniden hesaplanır.</summary>
    [HasPermission(Permissions.Payments.Create)]
    [HttpPut("{paymentId:guid}")]
    [ProducesResponseType(typeof(UpdatePaymentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePayment(
        Guid paymentId, [FromBody] UpdatePaymentRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new UpdatePaymentCommand(
                paymentId,
                request.PaymentDate,
                request.Amount,
                request.Description,
                request.ReceivedBy,
                request.TargetDebtType,
                request.BankAccountId),
            ct);
        return Ok(result);
    }

    /// <summary>Yanlış girilen tahsilatı kalıcı siler.</summary>
    [HasPermission(Permissions.Payments.Cancel)]
    [HttpDelete("{paymentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePayment(Guid paymentId, CancellationToken ct)
    {
        await _mediator.Send(new DeletePaymentCommand(paymentId), ct);
        return NoContent();
    }

    /// <summary>Tahsilatı iptal eder (soft-delete).</summary>
    [HasPermission(Permissions.Payments.Cancel)]
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
    [HasPermission(Permissions.Payments.View)]
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
    [HasPermission(Permissions.Payments.View)]
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
    string? ReceivedBy,
    LedgerDebtType? TargetDebtType = null,
    Guid? TargetOwnerId = null,
    Guid? BankAccountId = null);

public sealed record UpdatePaymentRequest(
    DateOnly PaymentDate,
    decimal Amount,
    string? Description,
    string? ReceivedBy,
    LedgerDebtType? TargetDebtType = null,
    Guid? BankAccountId = null);

public sealed record CancelPaymentRequest(string Reason);
