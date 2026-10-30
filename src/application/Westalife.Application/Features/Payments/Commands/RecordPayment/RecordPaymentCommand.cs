using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Payments.Commands.RecordPayment;

/// <summary>
/// Faz 6.8: Bir daire için tahsilat kaydeder.
/// FIFO allocation işlemi LedgerService ile sorgu anında yapılır;
/// bu komut yalnızca Payment satırını oluşturur.
/// </summary>
public sealed record RecordPaymentCommand(
    Guid ApartmentId,
    DateOnly PaymentDate,
    decimal Amount,
    string? Description,
    string? ReceivedBy) : IRequest<RecordPaymentResult>;

public sealed record RecordPaymentResult(
    Guid PaymentId,
    Guid ApartmentId,
    DateOnly PaymentDate,
    decimal Amount,
    string ReceivedBy);

public sealed class RecordPaymentCommandValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentCommandValidator()
    {
        RuleFor(c => c.ApartmentId).NotEmpty()
            .WithMessage("Daire kimliği zorunludur.");

        RuleFor(c => c.Amount).GreaterThan(0m)
            .WithMessage(DomainConstants.Payment.AmountMustBePositive);

        RuleFor(c => c.Amount).LessThanOrEqualTo(DomainConstants.Payment.AmountMax)
            .WithMessage(DomainConstants.Payment.AmountTooLarge);

        RuleFor(c => c.PaymentDate)
            .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.Today))
            .WithMessage(DomainConstants.Payment.PaymentDateFuture);

        RuleFor(c => c.Description)
            .MaximumLength(DomainConstants.Payment.DescriptionMaxLength)
            .When(c => c.Description is not null);

        RuleFor(c => c.ReceivedBy)
            .MaximumLength(DomainConstants.Payment.ReceivedByMaxLength)
            .When(c => c.ReceivedBy is not null);
    }
}

public sealed class RecordPaymentCommandHandler : IRequestHandler<RecordPaymentCommand, RecordPaymentResult>
{
    private readonly IApplicationDbContext _db;

    public RecordPaymentCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<RecordPaymentResult> Handle(RecordPaymentCommand request, CancellationToken cancellationToken)
    {
        // Daire var mı?
        var apartmentExists = await _db.Apartments
            .AnyAsync(a => a.Id == request.ApartmentId, cancellationToken);

        if (!apartmentExists)
            throw new NotFoundException("Daire bulunamadı.");

        var payment = new Payment(
            request.ApartmentId,
            request.PaymentDate,
            request.Amount,
            request.Description,
            request.ReceivedBy);

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(cancellationToken);

        return new RecordPaymentResult(
            payment.Id,
            payment.ApartmentId,
            payment.PaymentDate,
            payment.Amount,
            payment.ReceivedBy);
    }
}
