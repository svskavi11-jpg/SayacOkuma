using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;
using Westalife.Domain.Services.Ledger;
using Xunit;

namespace Westalife.Domain.Tests;

public class PaymentCorrectionTests
{
    [Fact]
    public void Update_ChangesAmountDateAndTarget()
    {
        var payment = new Payment(
            Guid.NewGuid(),
            new DateOnly(2026, 10, 1),
            100m,
            "eski",
            "Ops");

        payment.Update(
            new DateOnly(2026, 10, 2),
            3_385m,
            "Aidat tahsilatı",
            "Ops",
            LedgerDebtType.Aidat);

        Assert.Equal(new DateOnly(2026, 10, 2), payment.PaymentDate);
        Assert.Equal(3_385m, payment.Amount);
        Assert.Equal("Aidat tahsilatı", payment.Description);
        Assert.Equal(LedgerDebtType.Aidat, payment.TargetDebtType);
    }

    [Fact]
    public void Update_CancelledPayment_Throws()
    {
        var payment = new Payment(
            Guid.NewGuid(),
            new DateOnly(2026, 10, 1),
            100m,
            null,
            "Ops");
        payment.Cancel("Yanlış giriş");

        var error = Assert.Throws<DomainException>(() =>
            payment.Update(
                new DateOnly(2026, 10, 1),
                100m,
                null,
                "Ops",
                LedgerDebtType.Aidat));

        Assert.Equal("İptal edilmiş tahsilat değiştirilemez.", error.Message);
    }
}
