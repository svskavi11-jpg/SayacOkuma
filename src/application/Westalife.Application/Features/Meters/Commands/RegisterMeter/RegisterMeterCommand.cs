using MediatR;

namespace Westalife.Application.Features.Meters.Commands.RegisterMeter;

/// <summary>Tek bir kalorimetrenin sisteme kaydı (daireye bağlanır).</summary>
public sealed record RegisterMeterCommand(
    string SerialNumber,
    int DoorNumber,
    int PrimaryAddress,
    string Manufacturer,
    DateTimeOffset InstallationDateUtc) : IRequest<Guid>;
