using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Payments.Commands.DeletePayment;

/// <summary>
/// Yanlış girilen tahsilatı kalıcı siler. Cari hesap bu kaydı bir daha görmez.
/// </summary>
public sealed record DeletePaymentCommand(Guid PaymentId) : IRequest;

public sealed class DeletePaymentCommandValidator : AbstractValidator<DeletePaymentCommand>
{
    public DeletePaymentCommandValidator()
    {
        RuleFor(c => c.PaymentId).NotEmpty()
            .WithMessage("Tahsilat kimliği zorunludur.");
    }
}

public sealed class DeletePaymentCommandHandler : IRequestHandler<DeletePaymentCommand>
{
    private readonly IApplicationDbContext _db;

    public DeletePaymentCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task Handle(DeletePaymentCommand request, CancellationToken cancellationToken)
    {
        var payment = await _db.Payments
            .Include(p => p.Allocations)
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId, cancellationToken)
            ?? throw new NotFoundException(DomainConstants.Payment.NotFound);

        _db.Payments.Remove(payment);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
