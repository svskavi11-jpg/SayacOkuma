using Westalife.Application.Features.ResidentPortal;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Services.Ledger;
using Xunit;

namespace Westalife.Infrastructure.Tests;

public class ResidentLedgerMapperTests
{
    [Fact]
    public void Map_BuildsStatementRowsAndLastPayment()
    {
        var paymentId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var ledger = new LedgerResult(
            Guid.NewGuid(),
            "Daire 1",
            "1",
            [
                new LedgerLine(
                    LedgerDebtType.Invoice,
                    invoiceId.ToString(),
                    "Nisan 2026",
                    new DateOnly(2026, 4, 15),
                    100m,
                    40m,
                    60m,
                    5m,
                    65m,
                    [new AllocationDetail(paymentId, new DateOnly(2026, 4, 20), 40m, AllocationTarget.Invoice)])
            ],
            60m,
            5m,
            65m,
            new DateOnly(2026, 11, 10));

        var mapped = ResidentLedgerMapper.Map(ledger);

        Assert.Equal(DomainConstants.Apartment.BalanceDebtor, mapped.BalanceStatus);
        Assert.True(mapped.HasDebt);
        Assert.Equal(40m, mapped.LastPayment!.Amount);
        Assert.Contains(mapped.Rows, row => row.Kind == "Isınma" && row.Amount == 100m && row.Remaining == 60m);
        Assert.Contains(mapped.Rows, row => row.Kind == "Faiz" && row.Amount == 5m);
        Assert.Contains(mapped.Rows, row => row.Kind == "Tahsilat" && row.Amount == 40m);
    }
}
