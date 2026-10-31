using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;
using Westalife.Domain.Services.Ledger;

namespace Westalife.Application.Features.Payments.Commands.RecordPayment;

/// <summary>
/// Faz 6.8: Bir daire için tahsilat kaydeder.
/// FIFO allocation işlemi LedgerService ile sorgu anında yapılır;
/// bu komut yalnızca Payment satırını oluşturur.
/// <para>
/// Faz 7.1 — Hedefli Tahsilat: TargetDebtType/TargetOwnerId verilirse FIFO tahsilatı
/// yalnızca o türdeki ve o sakinin borçlarına mahsup edilir. Aksi halde tüm borçlara
/// normal FIFO uygulanır. Ödeme sahipliği (Hibrit Model/KMK m.22) otomatik çözülür.
/// </para>
/// </summary>
public sealed record RecordPaymentCommand(
    Guid ApartmentId,
    DateOnly PaymentDate,
    decimal Amount,
    string? Description,
    string? ReceivedBy,
    LedgerDebtType? TargetDebtType = null,
    Guid? TargetOwnerId = null) : IRequest<RecordPaymentResult>;

public sealed record RecordPaymentResult(
    Guid PaymentId,
    Guid ApartmentId,
    DateOnly PaymentDate,
    decimal Amount,
    string ReceivedBy,
    Guid? ApartmentOwnerId = null);

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

        // Faz 7.1: Hedefli tahsilat — borç türü enum doğrulaması (verildiyse).
        RuleFor(c => c.TargetDebtType)
            .IsInEnum()
            .When(c => c.TargetDebtType.HasValue);
    }
}

public sealed class RecordPaymentCommandHandler : IRequestHandler<RecordPaymentCommand, RecordPaymentResult>
{
    private readonly IApplicationDbContext _db;

    public RecordPaymentCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<RecordPaymentResult> Handle(RecordPaymentCommand request, CancellationToken cancellationToken)
    {
        // Daire + sakinler (SCD Type 2) — Faz 7.1 Hibrit Model sahiplik çözümü.
        var apartment = await _db.Apartments
            .Include(a => a.Owners)
            .FirstOrDefaultAsync(a => a.Id == request.ApartmentId, cancellationToken)
            ?? throw new NotFoundException("Daire bulunamadı.");

        var now = DateTimeOffset.UtcNow;

        // Ödeyen sakin: hedef sakin verilirse o; yoksa o anki borç sahibi (kiracı ?? ev sahibi).
        var payerId = request.TargetOwnerId ?? apartment.ResolveDebtOwner(now)?.Id;

        // KMK m.22: müteselsil sorumlu ev sahibi (ödeyen kiracıysa rücu edilebilir).
        var legalOwnerId = apartment.GetActiveResident(ResidentType.EvSahibi, now)?.Id;

        var payment = new Payment(
            request.ApartmentId,
            request.PaymentDate,
            request.Amount,
            request.Description,
            request.ReceivedBy,
            payerId,
            legalOwnerId,
            request.TargetDebtType,
            request.TargetOwnerId);

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(cancellationToken);

        return new RecordPaymentResult(
            payment.Id,
            payment.ApartmentId,
            payment.PaymentDate,
            payment.Amount,
            payment.ReceivedBy,
            payment.ApartmentOwnerId);
    }
}
