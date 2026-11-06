using Westalife.Domain.Entities;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;
using Westalife.Domain.Services.Ledger;

namespace Westalife.Domain.Tests;

public class ResidentPlacementTests
{
    private static readonly DateTimeOffset Y2020 = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Y2021 = new(2021, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Y2024 = new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ClosedResident_DoesNotReplaceTheActiveOne()
    {
        var apartment = new Apartment(Guid.NewGuid(), "1", "Daire 1");
        var active = apartment.UpdateResident(ResidentType.EvSahibi, "Ayşe", null, null, null, null, Y2024);
        var past = apartment.UpdateResident(
            ResidentType.EvSahibi, "Ali", null, null, null, null, Y2020,
            validTo: Y2021);

        Assert.Null(active.ValidTo);
        Assert.Equal(Y2021, past.ValidTo);
        Assert.Equal(2, apartment.Owners.Count);
    }

    [Fact]
    public void OverlappingStay_IsRejected()
    {
        var apartment = new Apartment(Guid.NewGuid(), "1", "Daire 1");
        apartment.UpdateResident(ResidentType.EvSahibi, "Ayşe", null, null, null, null, Y2024);

        var error = Assert.Throws<DomainException>(() => apartment.UpdateResident(
            ResidentType.EvSahibi, "Ali", null, null, null, null, Y2024,
            validTo: Y2024.AddYears(1)));

        Assert.Equal("Bu rolde girilen tarihler mevcut bir sakin kaydıyla çakışıyor.", error.Message);
    }

    [Fact]
    public void SamePerson_CanMoveTheOpenStartEarlier()
    {
        var apartment = new Apartment(Guid.NewGuid(), "1", "Daire 1");
        var resident = apartment.UpdateResident(ResidentType.EvSahibi, "Ayşe", null, null, null, null, Y2024);

        apartment.UpdateResident(ResidentType.EvSahibi, "Ayşe", "555", null, null, null, Y2020);

        Assert.Equal(Y2020, resident.ValidFrom);
        Assert.Equal("555", resident.PhoneNumber);
        Assert.Single(apartment.Owners);
    }

    [Fact]
    public void OpeningBalance_BelongsToTheSelectedResident()
    {
        var apartment = new Apartment(Guid.NewGuid(), "1", "Daire 1");
        apartment.UpdateResident(ResidentType.EvSahibi, "Ayşe", null, null, null, null, Y2024);
        var past = apartment.UpdateResident(
            ResidentType.EvSahibi, "Ali", null, null, null, null, Y2020, validTo: Y2021);

        apartment.SetOpeningBalance(1_000m, 0m, new DateOnly(2020, 1, 1), past.Id);
        var ledger = LedgerService.Calculate(apartment, [], [], 0m, new DateOnly(2026, 1, 1));

        Assert.Equal(past.Id, ledger.Lines.Single(l => l.Type == LedgerDebtType.OpeningBalancePrincipal).OwnerId);
        Assert.Equal("Ali", ledger.Lines.Single(l => l.Type == LedgerDebtType.OpeningBalancePrincipal).OwnerName);
    }
}
