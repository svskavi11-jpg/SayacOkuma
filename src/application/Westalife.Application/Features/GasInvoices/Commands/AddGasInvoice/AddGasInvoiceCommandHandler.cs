using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.GasInvoices.Commands.AddGasInvoice;

public sealed class AddGasInvoiceCommandHandler : IRequestHandler<AddGasInvoiceCommand, Guid>
{
    private readonly IApplicationDbContext _db;

    public AddGasInvoiceCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Guid> Handle(AddGasInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoiceNumber = request.InvoiceNumber.Trim();

        if (await _db.GasInvoices.AnyAsync(i => i.InvoiceNumber == invoiceNumber, cancellationToken))
        {
            throw new DomainException(
                $"{DomainConstants.ValidationMessages.InvoiceNumberRequired} zaten kayıtlı: {invoiceNumber}");
        }

        var invoice = new GasInvoice(
            invoiceNumber,
            request.SubscriberNumber,
            request.MasterMeterSerial,
            request.ReadingDateFrom,
            request.ReadingDateTo,
            request.IssuedOn,
            request.CorrectedVolumeM3,
            request.TotalEnergyKWh,
            request.PressureCorrectionFactor,
            request.ConsumptionAmount,
            request.ConsumptionVat,
            request.DistributionAmount,
            request.DistributionVat,
            request.TotalAmount,
            request.Note);

        _db.GasInvoices.Add(invoice);
        await _db.SaveChangesAsync(cancellationToken);

        return invoice.Id;
    }
}
