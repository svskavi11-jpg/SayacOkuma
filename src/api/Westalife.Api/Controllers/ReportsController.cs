using MediatR;
using Microsoft.AspNetCore.Mvc;
using Westalife.Api.Infrastructure;
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

    [HttpGet("general-assembly")]
    [ProducesResponseType(typeof(GeneralAssemblyReport), StatusCodes.Status200OK)]
    public async Task<IActionResult> GeneralAssembly(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetGeneralAssemblyReportQuery(from, to), cancellationToken));

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

    [HttpGet("apartment-tracking")]
    [ProducesResponseType(typeof(ApartmentTrackingReport), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApartmentTracking(
        [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetApartmentTrackingQuery(from, to), cancellationToken));

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

    [HttpGet("fuel")]
    [ProducesResponseType(typeof(FuelBreakdownReport), StatusCodes.Status200OK)]
    public async Task<IActionResult> Fuel(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] Guid? gasInvoiceId,
        CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetFuelBreakdownQuery(from, to, gasInvoiceId), cancellationToken));

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

    [HttpGet("bank")]
    [ProducesResponseType(typeof(BankReconciliationReport), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Bank(
        [FromQuery] Guid bankAccountId,
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        CancellationToken cancellationToken) =>
        Ok(await _mediator.Send(new GetBankReconciliationQuery(bankAccountId, from, to), cancellationToken));

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
