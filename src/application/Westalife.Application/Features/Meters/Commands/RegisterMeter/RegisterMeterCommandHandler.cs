using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Meters.Commands.RegisterMeter;

public sealed class RegisterMeterCommandHandler : IRequestHandler<RegisterMeterCommand, Guid>
{
    private readonly IApplicationDbContext _db;

    public RegisterMeterCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Guid> Handle(RegisterMeterCommand request, CancellationToken cancellationToken)
    {
        var serialNumber = request.SerialNumber.Trim();

        if (await _db.Meters.AnyAsync(m => m.SerialNumber == serialNumber, cancellationToken))
        {
            throw new DomainException(DomainConstants.ValidationMessages.MeterSerialAlreadyRegistered);
        }

        var apartment = await _db.Apartments
            .FirstOrDefaultAsync(a => a.DoorNumber == request.DoorNumber.ToString(), cancellationToken)
            ?? throw new DomainException(
                $"{DomainConstants.ValidationMessages.ApartmentNotFoundForDoorNumber} (Kapı: {request.DoorNumber})");

        var meter = Meter.CreateCalorimeter(
            serialNumber, apartment.Id, request.PrimaryAddress,
            request.Manufacturer, request.InstallationDateUtc);

        _db.Meters.Add(meter);
        await _db.SaveChangesAsync(cancellationToken);

        return meter.Id;
    }
}
