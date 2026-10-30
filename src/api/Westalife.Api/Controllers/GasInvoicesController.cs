using Westalife.Application.Features.GasInvoices.Commands.InvoiceFinance;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Westalife.Api.Infrastructure;
using Westalife.Application.Common.Models;
using Westalife.Application.Features.Dashboard.Queries.GetDashboardSummary;
using Westalife.Application.Features.GasInvoices.Commands.AddGasInvoice;
using Westalife.Application.Features.GasInvoices.Commands.CalculateCostDistribution;
using Westalife.Application.Features.GasInvoices.Commands.LockPeriod;
using Westalife.Application.Features.GasInvoices.Commands.UpdateGasInvoice;
using Westalife.Application.Features.GasInvoices.Queries.GetCalculationResult;
using Westalife.Application.Features.GasInvoices.Queries.GetGasInvoiceDetail;
using Westalife.Application.Features.GasInvoices.Queries.GetGasInvoices;
using Westalife.Application.Features.Buildings.Queries.GetBuildingLossReport;
using Westalife.Application.Features.GasInvoices.Queries.GetNoticeReport;
using Westalife.Infrastructure.Reporting.Excel;
using Westalife.Infrastructure.Reporting.Pdf;

namespace Westalife.Api.Controllers;

[ApiController]
[Route(ApiConstants.Routes.ApiPrefix + "/gas-invoices")]
[Produces("application/json")]
public class GasInvoicesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ReportingOptions _reportingOptions;

    public GasInvoicesController(IMediator mediator, IOptions<ReportingOptions> reportingOptions)
    {
        _mediator = mediator;
        _reportingOptions = reportingOptions.Value;
    }

    /// <summary>Akmercan bina doğalgaz faturasını sisteme girer (Toplam Ödenecek Tutar = 30/70 dağıtımın temeli).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AddGasInvoice([FromBody] AddGasInvoiceCommand command, CancellationToken cancellationToken)
    {
        var invoiceId = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetCalculation), new { id = invoiceId }, invoiceId);
    }

    /// <summary>Fatura listesi (yeniden hesaplama durumuyla birlikte).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<GasInvoiceListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetGasInvoices(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetGasInvoicesQuery(), cancellationToken);
        return Ok(result);
    }

    /// <summary>Tek faturanın tüm düzenlenebilir alanları (Etap 6.7 — "Düzenle" Modal'ı veri kaynağı).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GasInvoiceDetailItem), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvoiceDetail(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetGasInvoiceDetailQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Fatura düzenleme (Etap 6.7). Kilitli dönemde 409; hesaplaması olan faturada
    /// HasCalculationWarning=true döner (kullanıcı "yeniden hesaplayın" uyarısı görür).
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(UpdateGasInvoiceResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateGasInvoice(
        Guid id, [FromBody] UpdateGasInvoiceCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command with { InvoiceId = id }, cancellationToken);
        return Ok(result);
    }

    /// <summary>Dönemi kilitler (Etap 6.6): kilitli döneme okuma eklenemez, dönem yeniden hesaplanamaz.</summary>
    [HttpPost("{id:guid}/lock")]
    [ProducesResponseType(typeof(LockPeriodResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> LockPeriod(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new LockPeriodCommand(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>Dönem kilidini açar (Faz 7'de TenantAdmin yetkisi gerekecektir).</summary>
    [HttpPost("{id:guid}/unlock")]
    [ProducesResponseType(typeof(LockPeriodResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UnlockPeriod(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UnlockPeriodCommand(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>Fatura dönemi için 30/70 gider dağıtımını hesaplar ve kaydeder (yeniden hesaplamada eski satırlar superseded olur).</summary>
    [HttpPost("{id:guid}/calculate")]
    [ProducesResponseType(typeof(CalculateCostDistributionResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Calculate(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CalculateCostDistributionCommand(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>Faturanın güncel daire borç listesi (72 satır + mutabakat kontrolü).</summary>
    [HttpGet("{id:guid}/calculation")]
    [ProducesResponseType(typeof(GetCalculationResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCalculation(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCalculationResultQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>S3: Bina Kayıp-Kaçak / Ortak Alan Tüketimi raporu (yalnızca yönetim panosu).</summary>
    [HttpGet("{id:guid}/loss-report")]
    [ProducesResponseType(typeof(BuildingLossReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLossReport(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetBuildingLossReportQuery(id), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Danfoss formu: Bireysel Gider Bildirimi PDF. apartmentId verilirse tek daire (1 sayfa),
    /// verilmezse tüm bina (72 sayfa). Üretim öncesi mutabakat invariant'ı zorunludur.
    /// </summary>
    [HttpGet("{id:guid}/reports/notice.pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetNoticePdf(Guid id, [FromQuery] Guid? apartmentId, CancellationToken cancellationToken)
    {
        var data = await _mediator.Send(new GetNoticeReportQuery(id), cancellationToken);

        var pdf = apartmentId is null
            ? IndividualNoticePdfDocument.GenerateAll(data, _reportingOptions)
            : IndividualNoticePdfDocument.GenerateSingle(data, _reportingOptions, apartmentId.Value);

        return File(pdf, "application/pdf", $"BireyselGiderBildirimi-{data.PeriodName}.pdf");
    }

    /// <summary>Yönetici borç listesi Excel çıktısı (genel toplam mutabakat satırı dahil).</summary>
    [HttpGet("{id:guid}/reports/borclar.xlsx")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetDebtListExcel(Guid id, CancellationToken cancellationToken)
    {
        var data = await _mediator.Send(new GetNoticeReportQuery(id), cancellationToken);
        var excel = DebtListExcelExporter.Generate(data);
        return File(excel,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Borclistesi-{data.PeriodName}.xlsx");
    }

    /// <summary>Faz 6.8: faturanın son ödeme tarihini (vade) günceller. Dönem kilidinden bağımsızdır.</summary>
    [HttpPut("{id:guid}/due-date")]
    [ProducesResponseType(typeof(InvoiceFinanceResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetDueDate(
        Guid id, [FromBody] SetInvoiceDueDateCommand command, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(command with { InvoiceId = id }, cancellationToken));

    /// <summary>Faz 6.8: faturanın doğalgaz firmasına ödendi bilgisini (ve ödeme tarihini) işaretler.</summary>
    [HttpPut("{id:guid}/paid")]
    [ProducesResponseType(typeof(InvoiceFinanceResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPaid(
        Guid id, [FromBody] SetInvoicePaidCommand command, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(command with { InvoiceId = id }, cancellationToken));
}
