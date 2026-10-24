using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.GasInvoices.Commands.LockPeriod;

/// <summary>Dönemi kilitler (Etap 6.6): kilitli döneme okuma eklenemez, dönem yeniden hesaplanamaz.</summary>
public sealed record LockPeriodCommand(Guid GasInvoiceId) : IRequest<LockPeriodResult>;

public sealed record LockPeriodResult(Guid GasInvoiceId, bool IsLocked, DateTimeOffset? LockedOn);

/// <summary>Dönem kilidini açar. Faz 7'de yetki kontrolü TenantAdmin rolüyle yapılacaktır.</summary>
public sealed record UnlockPeriodCommand(Guid GasInvoiceId) : IRequest<LockPeriodResult>;

public sealed class LockPeriodCommandHandler : IRequestHandler<LockPeriodCommand, LockPeriodResult>
{
    private readonly IApplicationDbContext _db;

    public LockPeriodCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<LockPeriodResult> Handle(
        LockPeriodCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _db.GasInvoices
            .FirstOrDefaultAsync(i => i.Id == request.GasInvoiceId, cancellationToken)
            ?? throw new DomainException(DomainConstants.GasInvoice.InvoiceNotFound);

        // Kullanıcı kararı: yalnız HESAPLANMIŞ fatura kilitlenebilir.
        var hasCalculation = await _db.ApartmentInvoices
            .AnyAsync(x => x.GasInvoiceId == invoice.Id && !x.IsSuperseded, cancellationToken);

        if (!hasCalculation)
        {
            throw new DomainException(DomainConstants.GasInvoice.LockRequiresCalculation);
        }

        invoice.Lock();
        await _db.SaveChangesAsync(cancellationToken);

        return new LockPeriodResult(invoice.Id, invoice.IsLocked, invoice.LockedOn);
    }
}

public sealed class UnlockPeriodCommandHandler : IRequestHandler<UnlockPeriodCommand, LockPeriodResult>
{
    private readonly IApplicationDbContext _db;

    public UnlockPeriodCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<LockPeriodResult> Handle(
        UnlockPeriodCommand request, CancellationToken cancellationToken)
    {
        var invoice = await _db.GasInvoices
            .FirstOrDefaultAsync(i => i.Id == request.GasInvoiceId, cancellationToken)
            ?? throw new DomainException(DomainConstants.GasInvoice.InvoiceNotFound);

        invoice.Unlock();
        await _db.SaveChangesAsync(cancellationToken);

        return new LockPeriodResult(invoice.Id, invoice.IsLocked, invoice.LockedOn);
    }
}