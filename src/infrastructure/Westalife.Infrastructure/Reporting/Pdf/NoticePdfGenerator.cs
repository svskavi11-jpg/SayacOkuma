using MediatR;
using Microsoft.Extensions.Options;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Common.Models;
using Westalife.Application.Features.GasInvoices.Queries.GetNoticeReport;

namespace Westalife.Infrastructure.Reporting.Pdf;

public sealed class NoticePdfGenerator : INoticePdfGenerator
{
    private readonly IMediator _mediator;
    private readonly ReportingOptions _options;

    public NoticePdfGenerator(IMediator mediator, IOptions<ReportingOptions> options)
    {
        _mediator = mediator;
        _options = options.Value;
    }

    public async Task<byte[]> GenerateApartmentNoticeAsync(
        Guid gasInvoiceId,
        Guid apartmentId,
        CancellationToken cancellationToken = default)
    {
        var data = await _mediator.Send(new GetNoticeReportQuery(gasInvoiceId), cancellationToken);
        return IndividualNoticePdfDocument.GenerateSingle(data, _options, apartmentId);
    }
}
