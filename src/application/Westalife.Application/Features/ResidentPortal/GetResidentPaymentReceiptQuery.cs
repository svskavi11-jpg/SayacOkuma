using MediatR;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Payments.Queries.GetPaymentReceipt;

namespace Westalife.Application.Features.ResidentPortal;

public sealed record GetResidentPaymentReceiptQuery(Guid PaymentId) : IRequest<PaymentReceiptFile>;

public sealed class GetResidentPaymentReceiptQueryHandler
    : IRequestHandler<GetResidentPaymentReceiptQuery, PaymentReceiptFile>
{
    private readonly IResidentAccess _access;
    private readonly IMediator _mediator;

    public GetResidentPaymentReceiptQueryHandler(IResidentAccess access, IMediator mediator)
    {
        _access = access;
        _mediator = mediator;
    }

    public async Task<PaymentReceiptFile> Handle(
        GetResidentPaymentReceiptQuery request, CancellationToken cancellationToken)
    {
        var scope = await _access.RequireAsync(cancellationToken);
        return await _mediator.Send(
            new GetPaymentReceiptQuery(request.PaymentId, scope.ApartmentId),
            cancellationToken);
    }
}
