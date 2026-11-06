using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.BankAccounts;

namespace Westalife.Application.Features.Dashboard.Queries.GetBankDashboard;

public sealed record GetBankDashboardQuery : IRequest<BankDashboardResponse>;

public sealed record BankDashboardResponse(
    int Year,
    decimal TotalOpeningBalance,
    decimal TotalMoneyIn,
    decimal TotalMoneyOut,
    decimal TotalCurrentBalance,
    IReadOnlyList<BankDashboardAccountItem> Accounts,
    IReadOnlyList<BankDashboardMonthItem> Months);

public sealed record BankDashboardAccountItem(
    Guid Id,
    string BankName,
    string AccountName,
    decimal OpeningBalance,
    decimal MoneyIn,
    decimal MoneyOut,
    decimal CurrentBalance);

public sealed record BankDashboardMonthItem(int Month, decimal MoneyIn, decimal MoneyOut);

public sealed class GetBankDashboardQueryHandler : IRequestHandler<GetBankDashboardQuery, BankDashboardResponse>
{
    private readonly IApplicationDbContext _db;

    public GetBankDashboardQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<BankDashboardResponse> Handle(GetBankDashboardQuery request, CancellationToken cancellationToken)
    {
        var year = DateTime.Today.Year;
        var accounts = await _db.BankAccounts.AsNoTracking()
            .OrderBy(a => a.BankName)
            .ThenBy(a => a.AccountName)
            .ToListAsync(cancellationToken);

        var ids = accounts.Select(a => a.Id).ToList();
        var flows = await BankMovementTotals.ByAccountAsync(_db, ids, cancellationToken);

        var items = accounts.Select(a =>
        {
            var flow = flows.GetValueOrDefault(a.Id);
            var incoming = flow.Incoming;
            var outgoing = flow.Outgoing;
            return new BankDashboardAccountItem(
                a.Id,
                a.BankName,
                a.AccountName,
                a.OpeningBalance,
                incoming,
                outgoing,
                a.OpeningBalance + incoming - outgoing);
        }).ToList();

        var monthFlows = await BankMovementTotals.ByMonthAsync(_db, year, cancellationToken);
        var incomingByMonth = monthFlows.ToDictionary(x => x.Key, x => x.Value.Incoming);
        var outgoingByMonth = monthFlows.ToDictionary(x => x.Key, x => x.Value.Outgoing);
        var lastMonth = Math.Max(DateTime.Today.Month, Math.Max(
            incomingByMonth.Keys.DefaultIfEmpty(0).Max(),
            outgoingByMonth.Keys.DefaultIfEmpty(0).Max()));

        var months = Enumerable.Range(1, lastMonth)
            .Select(month => new BankDashboardMonthItem(
                month,
                incomingByMonth.GetValueOrDefault(month),
                outgoingByMonth.GetValueOrDefault(month)))
            .ToList();

        return new BankDashboardResponse(
            year,
            items.Sum(a => a.OpeningBalance),
            items.Sum(a => a.MoneyIn),
            items.Sum(a => a.MoneyOut),
            items.Sum(a => a.CurrentBalance),
            items,
            months);
    }
}
