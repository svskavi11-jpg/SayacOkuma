using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.GasInvoices.Commands.UpdateGasInvoice;

/// <summary>
/// Fatura düzenleme (Etap 6.7). Kilitli dönemde DomainException; hesaplaması olan faturada
/// yanıt HasCalculationWarning=true döner ve kullanıcı "yeniden hesaplayın" uyarısı görür.
/// </summary>
public sealed record UpdateGasInvoiceCommand(
    Guid InvoiceId,
    string InvoiceNumber,
    string SubscriberNumber,
    string MasterMeterSerial,
    DateTimeOffset ReadingDateFrom,
    DateTimeOffset ReadingDateTo,
    DateTimeOffset IssuedOn,
    decimal CorrectedVolumeM3,
    decimal TotalEnergyKWh,
    decimal PressureCorrectionFactor,
    decimal ConsumptionAmount,
    decimal ConsumptionVat,
    decimal DistributionAmount,
    decimal DistributionVat,
    decimal TotalAmount,
    string? Note) : IRequest<UpdateGasInvoiceResult>;

public sealed record UpdateGasInvoiceResult(Guid InvoiceId, bool HasCalculationWarning);

public sealed class UpdateGasInvoiceCommandValidator : AbstractValidator<UpdateGasInvoiceCommand>
{
    public UpdateGasInvoiceCommandValidator()
    {
        RuleFor(c => c.InvoiceId).NotEmpty();

        RuleFor(c => c.InvoiceNumber)
            .NotEmpty()
            .MaximumLength(DomainConstants.GasInvoice.InvoiceNumberMaxLength)
            .WithMessage(DomainConstants.ValidationMessages.InvoiceNumberRequired);

        RuleFor(c => c.SubscriberNumber)
            .NotEmpty()
            .MaximumLength(DomainConstants.GasInvoice.SubscriberNumberMaxLength);

        RuleFor(c => c.MasterMeterSerial)
            .NotEmpty()
            .MaximumLength(DomainConstants.GasInvoice.MeterSerialMaxLength);

        RuleFor(c => c)
            .Must(c => c.ReadingDateTo > c.ReadingDateFrom)
            .WithMessage(DomainConstants.ValidationMessages.InvoicePeriodInvalid);

        RuleFor(c => c.ConsumptionAmount).GreaterThanOrEqualTo(0m);
        RuleFor(c => c.ConsumptionVat).GreaterThanOrEqualTo(0m);
        RuleFor(c => c.DistributionAmount).GreaterThanOrEqualTo(0m);
        RuleFor(c => c.DistributionVat).GreaterThanOrEqualTo(0m);

        RuleFor(c => c.TotalAmount)
            .GreaterThan(0m)
            .WithMessage(DomainConstants.ValidationMessages.InvoiceTotalAmountMustBePositive);

        RuleFor(c => c.Note)
            .MaximumLength(DomainConstants.GasInvoice.NoteMaxLength);
    }
}

public sealed class UpdateGasInvoiceCommandHandler
    : IRequestHandler<UpdateGasInvoiceCommand, UpdateGasInvoiceResult>
{
    private readonly IApplicationDbContext _db;

    public UpdateGasInvoiceCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<UpdateGasInvoiceResult> Handle(
        UpdateGasInvoiceCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _db.GasInvoices
            .FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken)
            ?? throw new DomainException(DomainConstants.GasInvoice.InvoiceNotFound);

        // Aynı fatura numarası başka bir faturada kullanılamaz (unique index öncesi kontrol).
        var duplicateNumber = await _db.GasInvoices
            .AnyAsync(i => i.InvoiceNumber == request.InvoiceNumber.Trim() && i.Id != request.InvoiceId, cancellationToken);
        if (duplicateNumber)
        {
            throw new DomainException(
                $"{DomainConstants.ValidationMessages.InvoiceNumberRequired} zaten kayıtlı: {request.InvoiceNumber.Trim()}");
        }

        // Domain: kilitli dönemde DomainException fırlatır; tüm alanlar validasyondan geçer.
        invoice.Update(
            request.InvoiceNumber,
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

        await _db.SaveChangesAsync(cancellationToken);

        // Hesaplaması olan fatura düzenlendi → kullanıcıyı yeniden hesaplamaya yönlendir.
        var hasCalculation = await _db.ApartmentInvoices
            .AnyAsync(x => x.GasInvoiceId == invoice.Id && !x.IsSuperseded, cancellationToken);

        return new UpdateGasInvoiceResult(invoice.Id, hasCalculation);
    }
}