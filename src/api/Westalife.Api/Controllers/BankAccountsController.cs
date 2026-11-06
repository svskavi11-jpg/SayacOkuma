using MediatR;
using Microsoft.AspNetCore.Mvc;
using Westalife.Api.Infrastructure;
using Westalife.Application.Features.BankAccounts;
using Westalife.Domain.Entities;

namespace Westalife.Api.Controllers;

[ApiController]
[Route(ApiConstants.Routes.ApiPrefix + "/bank-accounts")]
[Produces("application/json")]
public class BankAccountsController : ControllerBase
{
    private readonly IMediator _mediator;

    public BankAccountsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BankAccountListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(await _mediator.Send(new GetBankAccountsQuery(), ct));

    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] SaveBankAccountRequest request, CancellationToken ct)
    {
        var id = await _mediator.Send(new CreateBankAccountCommand(
            request.BankName,
            request.AccountName,
            request.AccountNumber,
            request.Iban,
            request.OpeningBalance,
            request.OpeningDate), ct);
        return CreatedAtAction(nameof(GetAll), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid id, [FromBody] SaveBankAccountRequest request, CancellationToken ct) =>
        Ok(await _mediator.Send(new UpdateBankAccountCommand(
            id,
            request.BankName,
            request.AccountName,
            request.AccountNumber,
            request.Iban,
            request.OpeningBalance,
            request.OpeningDate), ct));

    [HttpPut("{id:guid}/active")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetBankAccountActiveRequest request, CancellationToken ct)
    {
        await _mediator.Send(new SetBankAccountActiveCommand(id, request.IsActive), ct);
        return NoContent();
    }

    [HttpGet("transactions")]
    [ProducesResponseType(typeof(IReadOnlyList<BankTransactionListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransactions(CancellationToken ct) =>
        Ok(await _mediator.Send(new GetBankTransactionsQuery(), ct));

    [HttpPost("transactions")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddTransaction([FromBody] AddBankTransactionRequest request, CancellationToken ct)
    {
        var id = await _mediator.Send(new AddBankTransactionCommand(
            request.BankAccountId,
            request.Type,
            request.Amount,
            request.TransactionDate,
            request.Description), ct);
        return CreatedAtAction(nameof(GetTransactions), new { id }, id);
    }

    [HttpPost("transfers")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> Transfer([FromBody] CreateBankTransferRequest request, CancellationToken ct)
    {
        var id = await _mediator.Send(new CreateBankTransferCommand(
            request.SourceBankAccountId,
            request.TargetBankAccountId,
            request.Amount,
            request.TransactionDate,
            request.Description), ct);
        return CreatedAtAction(nameof(GetTransactions), new { id }, id);
    }

    [HttpDelete("transactions/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteTransaction(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteBankTransactionCommand(id), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteBankAccountCommand(id), ct);
        return NoContent();
    }
}

public sealed record SaveBankAccountRequest(
    string BankName,
    string AccountName,
    string AccountNumber,
    string? Iban,
    decimal OpeningBalance,
    DateOnly OpeningDate);

public sealed record SetBankAccountActiveRequest(bool IsActive);

public sealed record AddBankTransactionRequest(
    Guid BankAccountId,
    BankTransactionType Type,
    decimal Amount,
    DateOnly TransactionDate,
    string? Description);

public sealed record CreateBankTransferRequest(
    Guid SourceBankAccountId,
    Guid TargetBankAccountId,
    decimal Amount,
    DateOnly TransactionDate,
    string? Description);