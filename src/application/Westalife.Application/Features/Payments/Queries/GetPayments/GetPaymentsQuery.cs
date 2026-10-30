using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;

namespace Westalife.Application.Features.Payments.Queries.GetPayments;

/// <summary>
/// Faz 6.8: Bir dairenin tahsilat geçmişini döner (iptal edilenler dahil).
/// </summary>
public sealed record GetPaymentsQuery(Guid ApartmentId) : IRequest<IReadOnlyList<PaymentListItem>>;

public sealed record PaymentListItem(
    Guid Id,
    Guid ApartmentId,
    DateOnly PaymentDate,
    decimal Amount,
    string? Description,
    string ReceivedBy,
    bool IsCancelled,
    DateTimeOffset? CancelledOn,
    string? CancellationReason,
    DateTimeOffset CreatedOn);

public sealed class GetPaymentsQueryHandler : IRequestHandler<GetPaymentsQuery, IReadOnlyList<PaymentListItem>>
{
    private readonly IApplicationDbContext _db;

    public GetPaymentsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<PaymentListItem>> Handle(
        GetPaymentsQuery request, CancellationToken cancellationToken)
    {
        return await _db.Payments
            .AsNoTracking()
            .Where(p => p.ApartmentId == request.ApartmentId)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.CreatedOn)
            .Select(p => new PaymentListItem(
                p.Id,
                p.ApartmentId,
                p.PaymentDate,
                p.Amount,
                p.Description,
                p.ReceivedBy,
                p.IsCancelled,
                p.CancelledOn,
                p.CancellationReason,
                p.CreatedOn))
            .ToListAsync(cancellationToken);
    }
}
