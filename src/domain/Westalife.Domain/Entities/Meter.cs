using Westalife.Domain.Common;
using Westalife.Domain.Constants;
using Westalife.Domain.Enums;

namespace Westalife.Domain.Entities;

/// <summary>
/// Sayaç. İki tip vardır: daire kalorimetresi (Calorimeter, kWh) ve bina ana doğalgaz sayacı (GasMasterMeter, m³).
/// </summary>
public class Meter : BaseEntity
{
    public string SerialNumber { get; private set; } = string.Empty;

    public MeterType Type { get; private set; }

    /// <summary>M-Bus birincil adresi (Izar Center dışa aktarımındaki "Birincil adres"; 1–250).</summary>
    public int? MbusPrimaryAddress { get; private set; }

    public string Manufacturer { get; private set; } = string.Empty;

    /// <summary>Calorimeter için dolu, GasMasterMeter için NULL.</summary>
    public Guid? ApartmentId { get; private set; }

    public Apartment? Apartment { get; private set; }

    public DateTimeOffset InstallationDateUtc { get; private set; }

    public bool IsActive { get; private set; } = true;

    private readonly List<MeterReading> _readings = [];
    public IReadOnlyCollection<MeterReading> Readings => _readings.AsReadOnly();

    private Meter() { } // EF Core

    private Meter(string serialNumber, MeterType type, string manufacturer, DateTimeOffset installationDateUtc, Guid? apartmentId, int? mbusPrimaryAddress)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.SerialNumberRequired, nameof(serialNumber));
        }

        if (mbusPrimaryAddress is < 1 or > 250)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.MeterPrimaryAddressInvalid, nameof(mbusPrimaryAddress));
        }

        SerialNumber = serialNumber.Trim();
        Type = type;
        MbusPrimaryAddress = mbusPrimaryAddress;
        Manufacturer = manufacturer.Trim();
        InstallationDateUtc = installationDateUtc;
        ApartmentId = apartmentId;
    }

    public static Meter CreateCalorimeter(
        string serialNumber, Guid apartmentId, int primaryAddress, string manufacturer, DateTimeOffset installationDateUtc)
    {
        if (apartmentId == Guid.Empty)
        {
            throw new ArgumentException(DomainConstants.ValidationMessages.MeterApartmentRequiredForCalorimeter, nameof(apartmentId));
        }

        return new Meter(serialNumber, MeterType.Calorimeter, manufacturer, installationDateUtc, apartmentId, primaryAddress);
    }

    public static Meter CreateGasMasterMeter(string serialNumber, int primaryAddress, string manufacturer, DateTimeOffset installationDateUtc) =>
        new(serialNumber, MeterType.GasMasterMeter, manufacturer, installationDateUtc, null, primaryAddress);
}
