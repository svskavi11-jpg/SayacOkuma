using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Notifications;

public sealed record GetNoticePdfByTokenQuery(Guid Token) : IRequest<NoticePdfDownload>;

public sealed record NoticePdfDownload(byte[] Content, string FileName, Guid ApartmentId, Guid GasInvoiceId);

public sealed class GetNoticePdfByTokenQueryHandler
    : IRequestHandler<GetNoticePdfByTokenQuery, NoticePdfDownload>
{
    private readonly IApplicationDbContext _db;
    private readonly INoticePdfGenerator _pdfGenerator;

    public GetNoticePdfByTokenQueryHandler(IApplicationDbContext db, INoticePdfGenerator pdfGenerator)
    {
        _db = db;
        _pdfGenerator = pdfGenerator;
    }

    public async Task<NoticePdfDownload> Handle(GetNoticePdfByTokenQuery request, CancellationToken cancellationToken)
    {
        if (request.Token == Guid.Empty)
        {
            throw new NotFoundException(DomainConstants.Notification.PdfTokenInvalid);
        }

        var access = await _db.NoticePdfAccessTokens.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Token == request.Token, cancellationToken)
            ?? throw new NotFoundException(DomainConstants.Notification.PdfTokenInvalid);

        if (!access.IsValidAt(DateTimeOffset.UtcNow))
        {
            throw new NotFoundException(DomainConstants.Notification.PdfTokenInvalid);
        }

        var apartment = await _db.Apartments.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == access.ApartmentId, cancellationToken);
        var door = apartment?.DoorNumber ?? access.ApartmentId.ToString("N")[..8];

        var bytes = await _pdfGenerator.GenerateApartmentNoticeAsync(
            access.GasInvoiceId, access.ApartmentId, cancellationToken);

        return new NoticePdfDownload(
            bytes,
            $"BireyselGiderBildirimi-{door}.pdf",
            access.ApartmentId,
            access.GasInvoiceId);
    }
}
