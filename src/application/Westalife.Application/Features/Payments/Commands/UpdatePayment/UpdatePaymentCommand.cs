using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;
using Westalife.Application.Features.BankAccounts;
using Westalife.Domain.Services.Ledger;

namespace Westalife.Application.Features.Payments.Commands.UpdatePayment;

/// <summary>
/// Yanlış girilen tahsilatın tarih, tutar, açıklama ve ödeme hedefini düzeltir.
/// Mahsup, cari hesap sorgusunda bu alanlardan yeniden hesaplanır.
/// </summary>
public sealed record UpdatePaymentCommand(
    Guid PaymentId,
    DateOnly PaymentDate,
    decimal Amount,
    string? Description,
    string? ReceivedBy,
    LedgerDebtType? TargetDebtType,
    Guid? BankAccountId = null) : IRequest<UpdatePaymentResult>;

public sealed record UpdatePaymentResult(
    Guid PaymentId,
    DateOnly PaymentDate,
    decimal Amount,
    LedgerDebtType? TargetDebtType);

public sealed class UpdatePaymentCommandValidator : AbstractValidator<UpdatePaymentCommand>
{
    public UpdatePaymentCommandValidator()
    {
        RuleFor(c => c.PaymentId).NotEmpty()
            .WithMessage("Tahsilat kimliği zorunludur.");

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

        RuleFor(c => c.TargetDebtType)
            .IsInEnum()
            .Must(t => t != LedgerDebtType.WaiverOffset)
            .WithMessage(DomainConstants.RoleAssignment.NotCollectionTarget)
            .When(c => c.TargetDebtType.HasValue);
    }
}

public sealed class UpdatePaymentCommandHandler : IRequestHandler<UpdatePaymentCommand, UpdatePaymentResult>
{
    private readonly IApplicationDbContext _db;

    public UpdatePaymentCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<UpdatePaymentResult> Handle(UpdatePaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _db.Payments
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId, cancellationToken)
            ?? throw new NotFoundException(DomainConstants.Payment.NotFound);

        var bankAccountId = await BankAccountLookup.ResolveAsync(
            _db, request.BankAccountId, payment.BankAccountId, cancellationToken);

        payment.Update(
            request.PaymentDate,
            request.Amount,
            request.Description,
            request.ReceivedBy,
            request.TargetDebtType,
            bankAccountId);

        await _db.SaveChangesAsync(cancellationToken);

        return new UpdatePaymentResult(
            payment.Id,
            payment.PaymentDate,
            payment.Amount,
            payment.TargetDebtType);
    }
}
