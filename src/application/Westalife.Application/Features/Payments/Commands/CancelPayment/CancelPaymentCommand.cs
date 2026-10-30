using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Payments.Commands.CancelPayment;

/// <summary>
/// Faz 6.8: Bir tahsilatı soft-delete ile iptal eder.
/// Payment.Cancel() domain metodunu çağırır; fiziksel silme yasak.
/// </summary>
public sealed record CancelPaymentCommand(Guid PaymentId, string Reason) : IRequest<CancelPaymentResult>;

public sealed record CancelPaymentResult(Guid PaymentId, bool IsCancelled, DateTimeOffset CancelledOn);

public sealed class CancelPaymentCommandValidator : AbstractValidator<CancelPaymentCommand>
{
    public CancelPaymentCommandValidator()
    {
        RuleFor(c => c.PaymentId).NotEmpty()
            .WithMessage("Tahsilat kimliği zorunludur.");

        RuleFor(c => c.Reason).NotEmpty()
            .WithMessage(DomainConstants.Payment.CancellationReasonRequired);

        RuleFor(c => c.Reason).MaximumLength(DomainConstants.Payment.CancellationReasonMaxLength)
            .When(c => !string.IsNullOrEmpty(c.Reason));
    }
}

public sealed class CancelPaymentCommandHandler : IRequestHandler<CancelPaymentCommand, CancelPaymentResult>
{
    private readonly IApplicationDbContext _db;

    public CancelPaymentCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CancelPaymentResult> Handle(CancelPaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _db.Payments
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId, cancellationToken)
            ?? throw new NotFoundException(DomainConstants.Payment.NotFound);

        payment.Cancel(request.Reason);

        await _db.SaveChangesAsync(cancellationToken);

        return new CancelPaymentResult(
            payment.Id,
            payment.IsCancelled,
            payment.CancelledOn!.Value);
    }
}
