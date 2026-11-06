using Westalife.Domain.Constants;
using Westalife.Domain.Services.Ledger;

namespace Westalife.Domain.Tests;

public class ApartmentBalanceTests
{
    [Fact]
    public void Status_PositiveIsDebtor_NegativeIsCreditor_ZeroIsSettled()
    {
        Assert.Equal(DomainConstants.Apartment.BalanceDebtor, ApartmentBalance.Status(1250.50m));
        Assert.Equal(DomainConstants.Apartment.BalanceCreditor, ApartmentBalance.Status(-10m));
        Assert.Equal(DomainConstants.Apartment.BalanceSettled, ApartmentBalance.Status(0m));
    }
}
