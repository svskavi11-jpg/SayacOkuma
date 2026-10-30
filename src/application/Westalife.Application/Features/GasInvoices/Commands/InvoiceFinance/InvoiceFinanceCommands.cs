using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.GasInvoices.Commands.InvoiceFinance;

/// <summary>Faz 6.8: faturanın son ödeme tarihini (vade) günceller. Dönem kilidinden bağımsızdır.</summary>
public sealed record SetInvoiceDueDateCommand(Guid InvoiceId, DateOnly DueDate) : IRequest<InvoiceFinanceResult>;

/// <summary>
/// Faz 6.8: faturanın doğalgaz firmasına ödenip ödenmediğini işaretler (nakit çıkışı).
/// IsPaid=true iken PaidOn zorunludur; false ise ödeme tarihi temizlenir.
/// </summary>
public sealed record SetInvoicePaidCommand(Guid InvoiceId, bool IsPaid, DateOnly? PaidOn) : IRequest<InvoiceFinanceResult>;

public sealed record InvoiceFinanceResult(Guid InvoiceId, DateOnly DueDate, bool IsPaid, DateOnly? PaidOn);

public sealed class SetInvoicePaidCommandValidator : AbstractValidator<SetInvoicePaidCommand>
{
    public SetInvoicePaidCommandValidator()
    {
        RuleFor(c => c.PaidOn).NotNull().When(c => c.IsPaid)
            .WithMessage(DomainConstants.GasInvoice.PaidOnRequired);
    }
}

public sealed class SetInvoiceDueDateCommandHandler : IRequestHandler<SetInvoiceDueDateCommand, InvoiceFinanceResult>
{
    private readonly IApplicationDbContext _db;

    public SetInvoiceDueDateCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<InvoiceFinanceResult> Handle(SetInvoiceDueDateCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _db.GasInvoices.FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken)
            ?? throw new NotFoundException("Fatura bulunamadı.");

        invoice.SetDueDate(request.DueDate);
        await _db.SaveChangesAsync(cancellationToken);

        return new InvoiceFinanceResult(invoice.Id, invoice.DueDate, invoice.IsPaid, invoice.PaidOn);
    }
}

public sealed class SetInvoicePaidCommandHandler : IRequestHandler<SetInvoicePaidCommand, InvoiceFinanceResult>
{
    private readonly IApplicationDbContext _db;

    public SetInvoicePaidCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<InvoiceFinanceResult> Handle(SetInvoicePaidCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _db.GasInvoices.FirstOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken)
            ?? throw new NotFoundException("Fatura bulunamadı.");

        if (request.IsPaid)
        {
            invoice.MarkPaid(request.PaidOn!.Value);
        }
        else
        {
            invoice.MarkUnpaid();
        }

        await _db.SaveChangesAsync(cancellationToken);
        return new InvoiceFinanceResult(invoice.Id, invoice.DueDate, invoice.IsPaid, invoice.PaidOn);
    }
}
