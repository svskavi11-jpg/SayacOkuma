using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Westalife.Api.Infrastructure;
using Westalife.Domain.Constants;
using Westalife.Application.Features.Notifications;
using Westalife.Application.Features.Reports;
using Westalife.Infrastructure.Reporting.Excel;
using Westalife.Infrastructure.Reporting.Pdf;

namespace Westalife.Api.Controllers;

[ApiController]
[Route(ApiConstants.Routes.ApiPrefix + "/reports")]
[Produces("application/json")]
public class ReportsController : ControllerBase
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IMediator _mediator;

    public ReportsController(IMediator mediator) => _mediator = mediator;

    /// <summary>SMS bildirimlerindeki güvenli token ile bireysel gider bildirimi PDF.</summary>
    [HasPermission(Permissions.Reports.GasNotifications)]
    [HttpGet("notice.pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNoticePdfByToken(
        [FromQuery] Guid token,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetNoticePdfByTokenQuery(token), cancellationToken);
        return File(result.Content, "application/pdf", result.FileName);
    }

    [HasPermission(Permissions.Reports.IncomeExpense)]
    [HttpGet("income-expense")]
    [ProducesResponseType(typeof(AuditIncomeExpenseReport), StatusCodes.Status200OK)]
    public async Task<IActionResult> IncomeExpense(
        [FromQuery] int? year,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var effectiveYear = year ?? from?.Year ?? DateTimeOffset.UtcNow.Year;
        return Ok(await _mediator.Send(new GetAuditIncomeExpenseQuery(effectiveYear, from, to), cancellationToken));
    }

    [HasPermission(Permissions.Reports.IncomeExpense)]
    [HttpGet("income-expense.xlsx")]
    [Produces(ExcelContentType)]
    public async Task<IActionResult> IncomeExpenseExcel(
        [FromQuery] int? year,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
    {
        var effectiveYear = year ?? from?.Year ?? DateTimeOffset.UtcNow.Year;
        var data = await _mediator.Send(new GetAuditIncomeExpenseQuery(effectiveYear, from, to), cancellationToken);
        var suffix = from.HasValue && to.HasValue ? $"{from:yyyyMMdd}-{to:yyyyMMdd}" : $"{effectiveYear}";
        return File(AuditReportExcelExporter.IncomeExpense(data), ExcelContentType, $"GelirGiderOzet-{suffix}.xlsx");
    }

    [HasPermission(Permissions.Reports.GeneralAssembly)]
    [HttpGet("general-assembly")]
    [ProducesResponseType(typeof(GeneralAssemblyReport), StatusCodes.Status200OK)]
    public async Task<IActionResult> GeneralAssembly(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetGeneralAssemblyReportQuery(from, to), cancellationToken));

    [HasPermission(Permissions.Reports.GeneralAssembly)]
    [HttpGet("general-assembly.pdf")]
    [Produces("application/pdf")]
    public async Task<IActionResult> GeneralAssemblyPdf(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken)
    {
        var data = await _mediator.Send(new GetGeneralAssemblyReportQuery(from, to), cancellationToken);
        return File(
            GeneralAssemblyPdfDocument.Generate(data),
            "application/pdf",
            $"GenelKurulDenetimRaporu-{from:yyyyMMdd}-{to:yyyyMMdd}.pdf");
    }

    [HasPermission(Permissions.Reports.GeneralAssembly)]
    [HttpGet("general-assembly.xlsx")]
    [Produces(ExcelContentType)]
    public async Task<IActionResult> GeneralAssemblyExcel(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken)
    {
        var data = await _mediator.Send(new GetGeneralAssemblyReportQuery(from, to), cancellationToken);
        return File(
            AuditReportExcelExporter.GeneralAssembly(data),
            ExcelContentType,
            $"GenelKurulDenetimRaporu-{from:yyyyMMdd}-{to:yyyyMMdd}.xlsx");
    }

    [HasPermission(Permissions.Reports.ApartmentTracking)]
    [HttpGet("apartment-tracking")]
    [ProducesResponseType(typeof(ApartmentTrackingReport), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApartmentTracking(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetApartmentTrackingQuery(from, to), cancellationToken));

    [HasPermission(Permissions.Reports.ApartmentTracking)]
    [HttpGet("apartment-tracking.xlsx")]
    [Produces(ExcelContentType)]
    public async Task<IActionResult> ApartmentTrackingExcel(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        var data = await _mediator.Send(new GetApartmentTrackingQuery(from, to), cancellationToken);
        return File(
            AuditReportExcelExporter.ApartmentTracking(data),
            ExcelContentType,
            $"DaireTakip-{from:yyyyMMdd}-{to:yyyyMMdd}.xlsx");
    }

    [HasPermission(Permissions.Reports.FuelBreakdown)]
    [HttpGet("fuel")]
    [ProducesResponseType(typeof(FuelBreakdownReport), StatusCodes.Status200OK)]
    public async Task<IActionResult> Fuel(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? gasInvoiceId,
        CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetFuelBreakdownQuery(from, to, gasInvoiceId), cancellationToken));

    [HasPermission(Permissions.Reports.FuelBreakdown)]
    [HttpGet("fuel.xlsx")]
    [Produces(ExcelContentType)]
    public async Task<IActionResult> FuelExcel(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? gasInvoiceId,
        CancellationToken cancellationToken)
    {
        var data = await _mediator.Send(new GetFuelBreakdownQuery(from, to, gasInvoiceId), cancellationToken);
        return File(
            AuditReportExcelExporter.Fuel(data),
            ExcelContentType,
            $"YakitDokumu-{from:yyyyMMdd}-{to:yyyyMMdd}.xlsx");
    }

    [HasPermission(Permissions.Reports.BankControl)]
    [HttpGet("bank")]
    [ProducesResponseType(typeof(BankReconciliationReport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Bank(
        [FromQuery] Guid bankAccountId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetBankReconciliationQuery(bankAccountId, from, to), cancellationToken));

    [HasPermission(Permissions.Reports.BankControl)]
    [HttpGet("bank.xlsx")]
    [Produces(ExcelContentType)]
    public async Task<IActionResult> BankExcel(
        [FromQuery] Guid bankAccountId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken)
    {
        var data = await _mediator.Send(new GetBankReconciliationQuery(bankAccountId, from, to), cancellationToken);
        return File(
            AuditReportExcelExporter.Bank(data),
            ExcelContentType,
            $"BankaKontrol-{from:yyyyMMdd}-{to:yyyyMMdd}.xlsx");
    }
}
