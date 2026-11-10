using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Westalife.Api.Infrastructure;
using Westalife.Domain.Constants;
using Westalife.Application.Features.Budget.Commands.BudgetPeriod;
using Westalife.Application.Features.Budget.Commands.BudgetTransfer;
using Westalife.Application.Features.Budget.Commands.CreateBudgetItem;
using Westalife.Application.Features.Budget.Commands.CreateExpense;
using Westalife.Application.Features.Budget.Commands.UpdateExpense;
using Westalife.Application.Features.Budget.Commands.DeleteBudgetItem;
using Westalife.Application.Features.Budget.Commands.DeleteExpense;
using Westalife.Application.Features.Budget.Commands.IncomeItems;
using Westalife.Application.Features.Budget.Commands.UpdateBudgetItem;
using Westalife.Application.Features.Budget.Queries.GetBudgetRealization;
using Westalife.Application.Features.Budget.Queries.GetExpenses;
using Westalife.Application.Features.Budget.Queries.GetIncomeExpenseReport;
using Westalife.Infrastructure.Reporting.Excel;
using Westalife.Infrastructure.Reporting.Pdf;

namespace Westalife.Api.Controllers;

/// <summary>
/// Faz 6.10: Bütçe kalemleri, gider girişi ve gelir-gider / gerçekleşme raporları.
/// </summary>
[ApiController]
[Route(ApiConstants.Routes.ApiPrefix + "/budget")]
[Produces("application/json")]
public class BudgetController : ControllerBase
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IMediator _mediator;

    public BudgetController(IMediator mediator) => _mediator = mediator;

    [HasPermission(Permissions.Budget.View)]
    [HttpGet("items")]
    [ProducesResponseType(typeof(BudgetRealizationReport), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetItems([FromQuery] int year, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetBudgetRealizationQuery(year), ct));

    [HasPermission(Permissions.Budget.Create)]
    [HttpPost("items")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateItem([FromBody] CreateBudgetItemRequest request, CancellationToken ct)
    {
        var id = await _mediator.Send(
            new CreateBudgetItemCommand(request.Year, request.Category, request.EstimatedAmount), ct);
        return CreatedAtAction(nameof(GetItems), new { year = request.Year }, id);
    }

    [HasPermission(Permissions.Budget.Edit)]
    [HttpPut("items/{id:guid}")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateItem(
        Guid id, [FromBody] UpdateBudgetItemRequest request, CancellationToken ct) =>
        Ok(await _mediator.Send(new UpdateBudgetItemCommand(id, request.Category, request.EstimatedAmount), ct));

    [HasPermission(Permissions.Budget.Edit)]
    [HttpDelete("items/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteItem(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteBudgetItemCommand(id), ct);
        return NoContent();
    }

    [HasPermission(Permissions.Budget.View)]
    [HttpGet("period")]
    [ProducesResponseType(typeof(BudgetPeriodStatus), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPeriod([FromQuery] int year, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetBudgetPeriodQuery(year), ct));

    [HasPermission(Permissions.Budget.Edit)]
    [HttpPut("period/lock")]
    [ProducesResponseType(typeof(BudgetPeriodStatus), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetPeriodLock([FromBody] SetBudgetPeriodLockRequest request, CancellationToken ct) =>
        Ok(await _mediator.Send(new SetBudgetPeriodLockCommand(request.Year, request.Locked), ct));

    [HasPermission(Permissions.Budget.View)]
    [HttpGet("income-items")]
    [ProducesResponseType(typeof(IReadOnlyList<IncomeItemListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIncomeItems([FromQuery] int year, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetIncomeItemsQuery(year), ct));

    [HasPermission(Permissions.Budget.Create)]
    [HttpPost("income-items")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateIncomeItem([FromBody] CreateBudgetItemRequest request, CancellationToken ct)
    {
        var id = await _mediator.Send(
            new CreateIncomeItemCommand(request.Year, request.Category, request.EstimatedAmount), ct);
        return CreatedAtAction(nameof(GetIncomeItems), new { year = request.Year }, id);
    }

    [HasPermission(Permissions.Budget.Edit)]
    [HttpPut("income-items/{id:guid}")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateIncomeItem(
        Guid id, [FromBody] UpdateBudgetItemRequest request, CancellationToken ct) =>
        Ok(await _mediator.Send(new UpdateIncomeItemCommand(id, request.Category, request.EstimatedAmount), ct));

    [HasPermission(Permissions.Budget.Edit)]
    [HttpDelete("income-items/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteIncomeItem(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteIncomeItemCommand(id), ct);
        return NoContent();
    }

    [HasPermission(Permissions.Budget.View)]
    [HttpGet("transfers")]
    [ProducesResponseType(typeof(IReadOnlyList<BudgetTransferListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransfers([FromQuery] int year, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetBudgetTransfersQuery(year), ct));

    [HasPermission(Permissions.Budget.Transfer)]
    [HttpPost("transfers")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTransfer([FromBody] CreateBudgetTransferRequest request, CancellationToken ct)
    {
        var id = await _mediator.Send(
            new CreateBudgetTransferCommand(
                request.TransferDate, request.Amount, request.FromBudgetItemId, request.ToBudgetItemId, request.Note),
            ct);
        return CreatedAtAction(nameof(GetTransfers), new { year = request.TransferDate.Year }, id);
    }

    [HasPermission(Permissions.Budget.Transfer)]
    [HttpDelete("transfers/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteTransfer(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteBudgetTransferCommand(id), ct);
        return NoContent();
    }

    [HasPermission(Permissions.Budget.View)]
    [HttpGet("expenses")]
    [ProducesResponseType(typeof(IReadOnlyList<ExpenseListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpenses([FromQuery] int year, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetExpensesQuery(year), ct));

    [HasPermission(Permissions.Budget.Create)]
    [HttpPost("expenses")]
    [ProducesResponseType(typeof(CreateExpenseResult), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateExpenseCommand(
                request.ExpenseDate,
                request.Amount,
                request.Description,
                request.ExpenseType,
                request.BudgetItemId,
                request.DocumentNumber,
                request.FundAidatInvoiceId,
                request.BankAccountId),
            ct);
        return CreatedAtAction(nameof(GetExpenses), new { year = request.ExpenseDate.Year }, result);
    }

    [HasPermission(Permissions.Budget.Edit)]
    [HttpPut("expenses/{id:guid}")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateExpense(Guid id, [FromBody] UpdateExpenseRequest request, CancellationToken ct) =>
        Ok(await _mediator.Send(
            new UpdateExpenseCommand(
                id,
                request.ExpenseDate,
                request.Amount,
                request.Description,
                request.ExpenseType,
                request.BudgetItemId,
                request.DocumentNumber,
                request.FundAidatInvoiceId,
                request.BankAccountId),
            ct));

    [HasPermission(Permissions.Budget.Edit)]
    [HttpDelete("expenses/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteExpense(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteExpenseCommand(id), ct);
        return NoContent();
    }

    [HasPermission(Permissions.Budget.View)]
    [HttpGet("realization")]
    [ProducesResponseType(typeof(BudgetRealizationReport), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRealization([FromQuery] int year, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetBudgetRealizationQuery(year), ct));

    [HasPermission(Permissions.Budget.View)]
    [HttpGet("income-expense")]
    [ProducesResponseType(typeof(IncomeExpenseReport), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetIncomeExpense([FromQuery] int year, CancellationToken ct) =>
        Ok(await _mediator.Send(new GetIncomeExpenseReportQuery(year), ct));

    [HasPermission(Permissions.Budget.View)]
    [HttpGet("reports/income-expense.pdf")]
    [Produces("application/pdf")]
    public async Task<IActionResult> IncomeExpensePdf([FromQuery] int year, CancellationToken ct)
    {
        var data = await _mediator.Send(new GetIncomeExpenseReportQuery(year), ct);
        return File(IncomeExpensePdfDocument.Generate(data), "application/pdf", $"GelirGider-{year}.pdf");
    }

    [HasPermission(Permissions.Budget.View)]
    [HttpGet("reports/income-expense.xlsx")]
    [Produces(ExcelContentType)]
    public async Task<IActionResult> IncomeExpenseExcel([FromQuery] int year, CancellationToken ct)
    {
        var data = await _mediator.Send(new GetIncomeExpenseReportQuery(year), ct);
        return File(IncomeExpenseExcelExporter.Generate(data), ExcelContentType, $"GelirGider-{year}.xlsx");
    }

    [HasPermission(Permissions.Budget.View)]
    [HttpGet("reports/realization.pdf")]
    [Produces("application/pdf")]
    public async Task<IActionResult> RealizationPdf([FromQuery] int year, CancellationToken ct)
    {
        var data = await _mediator.Send(new GetBudgetRealizationQuery(year), ct);
        return File(BudgetRealizationPdfDocument.Generate(data), "application/pdf", $"ButceGerceklesme-{year}.pdf");
    }

    [HasPermission(Permissions.Budget.View)]
    [HttpGet("reports/realization.xlsx")]
    [Produces(ExcelContentType)]
    public async Task<IActionResult> RealizationExcel([FromQuery] int year, CancellationToken ct)
    {
        var data = await _mediator.Send(new GetBudgetRealizationQuery(year), ct);
        return File(BudgetRealizationExcelExporter.Generate(data), ExcelContentType, $"ButceGerceklesme-{year}.xlsx");
    }
}

public sealed record CreateBudgetItemRequest(int Year, string Category, decimal EstimatedAmount);

public sealed record UpdateBudgetItemRequest(string Category, decimal EstimatedAmount);

public sealed record CreateExpenseRequest(
    DateOnly ExpenseDate,
    decimal Amount,
    string? Description,
    string? ExpenseType,
    Guid? BudgetItemId,
    string? DocumentNumber = null,
    Guid? FundAidatInvoiceId = null,
    Guid? BankAccountId = null);

public sealed record UpdateExpenseRequest(
    DateOnly ExpenseDate,
    decimal Amount,
    string? Description,
    string? ExpenseType,
    Guid? BudgetItemId,
    string? DocumentNumber,
    Guid? FundAidatInvoiceId,
    Guid? BankAccountId);

public sealed record SetBudgetPeriodLockRequest(int Year, bool Locked);

public sealed record CreateBudgetTransferRequest(
    DateOnly TransferDate,
    decimal Amount,
    Guid FromBudgetItemId,
    Guid ToBudgetItemId,
    string? Note);
