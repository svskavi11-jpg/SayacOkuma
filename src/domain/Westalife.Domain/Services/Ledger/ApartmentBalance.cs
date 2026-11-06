using Westalife.Domain.Constants;

namespace Westalife.Domain.Services.Ledger;

/// <summary>
/// Daire listesindeki guncel bakiye durumu. Pozitif borc, negatif alacak, sifir kapali hesap.
/// </summary>
public static class ApartmentBalance
{
    public static string Status(decimal balance) =>
        balance > 0m ? DomainConstants.Apartment.BalanceDebtor
        : balance < 0m ? DomainConstants.Apartment.BalanceCreditor
        : DomainConstants.Apartment.BalanceSettled;
}
