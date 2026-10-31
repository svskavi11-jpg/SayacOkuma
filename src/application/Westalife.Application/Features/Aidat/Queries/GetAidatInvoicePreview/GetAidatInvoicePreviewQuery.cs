using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;

namespace Westalife.Application.Features.Aidat.Queries.GetAidatInvoicePreview;

/// <summary>
/// Faz 6.9: Tahakkuk oluşturmadan ÖNCE daire bazında borç önizlemesi.
/// CreateAidatInvoiceCommand ile aynı dağıtım kuralları uygulanır; hiçbir kayıt yazılmaz.
/// </summary>
public sealed record GetAidatInvoicePreviewQuery(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly DueDate,
    AidatDistributionMethod DistributionMethod,
    decimal? EqualAmount = null) : IRequest<AidatInvoicePreviewResult>;

public sealed record AidatInvoicePreviewRow(
    string DoorNumber,
    string ApartmentTitle,
    string? TariffName,
    decimal? SquareMeters,
    decimal Amount,
    string? SkipReason);

public sealed record AidatInvoicePreviewResult(
    int ChargeCount,
    int SkippedCount,
    decimal TotalAmount,
    IReadOnlyList<AidatInvoicePreviewRow> Rows);

public sealed class GetAidatInvoicePreviewQueryValidator : AbstractValidator<GetAidatInvoicePreviewQuery>
{
    public GetAidatInvoicePreviewQueryValidator()
    {
        RuleFor(c => c.PeriodEnd)
            .GreaterThanOrEqualTo(c => c.PeriodStart)
            .WithMessage(DomainConstants.ValidationMessages.AidatPeriodInvalid);

        RuleFor(c => c.EqualAmount)
            .GreaterThan(0m)
            .WithMessage(DomainConstants.ValidationMessages.AidatEqualAmountRequired)
            .When(c => c.DistributionMethod == AidatDistributionMethod.Esit);
    }
}

public sealed class GetAidatInvoicePreviewQueryHandler : IRequestHandler<GetAidatInvoicePreviewQuery, AidatInvoicePreviewResult>
{
    private readonly IApplicationDbContext _db;

    public GetAidatInvoicePreviewQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<AidatInvoicePreviewResult> Handle(
        GetAidatInvoicePreviewQuery request, CancellationToken cancellationToken)
    {
        var apartments = await _db.Apartments
            .AsNoTracking()
            .Include(a => a.AidatTariff)
            .Include(a => a.Dimensions)
            .Where(a => a.IsActive)
            .ToListAsync(cancellationToken);

        var rows = new List<AidatInvoicePreviewRow>();

        foreach (var apartment in apartments.OrderByDoorNumber(a => a.DoorNumber))
        {
            switch (request.DistributionMethod)
            {
                case AidatDistributionMethod.TarifeBazli:
                    if (apartment.AidatTariff is null)
                    {
                        rows.Add(NoTariffRow(apartment));
                        continue;
                    }

                    rows.Add(new AidatInvoicePreviewRow(
                        apartment.DoorNumber,
                        apartment.Title,
                        apartment.AidatTariff.Name,
                        apartment.AidatTariff.SquareMeters,
                        apartment.AidatTariff.Amount,
                        null));
                    break;

                case AidatDistributionMethod.Metrekare:
                    if (apartment.AidatTariff is null)
                    {
                        rows.Add(NoTariffRow(apartment));
                        continue;
                    }

                    var activeDimension = apartment.Dimensions.FirstOrDefault(d => d.ValidTo is null);
                    if (activeDimension is null || activeDimension.SquareMeters <= 0m)
                    {
                        rows.Add(new AidatInvoicePreviewRow(
                            apartment.DoorNumber,
                            apartment.Title,
                            apartment.AidatTariff.Name,
                            null,
                            0m,
                            DomainConstants.AidatInvoice.SkipReasonNoDimension));
                        continue;
                    }

                    var unitPrice = apartment.AidatTariff.Amount / apartment.AidatTariff.SquareMeters;
                    rows.Add(new AidatInvoicePreviewRow(
                        apartment.DoorNumber,
                        apartment.Title,
                        apartment.AidatTariff.Name,
                        activeDimension.SquareMeters,
                        decimal.Round(activeDimension.SquareMeters * unitPrice, 2),
                        null));
                    break;

                case AidatDistributionMethod.Esit:
                    rows.Add(new AidatInvoicePreviewRow(
                        apartment.DoorNumber,
                        apartment.Title,
                        null,
                        null,
                        request.EqualAmount ?? 0m,
                        null));
                    break;
            }
        }

        var chargeable = rows.Where(r => r.SkipReason is null).ToList();

        return new AidatInvoicePreviewResult(
            chargeable.Count,
            rows.Count - chargeable.Count,
            decimal.Round(chargeable.Sum(r => r.Amount), 2),
            rows);
    }

    private static AidatInvoicePreviewRow NoTariffRow(Apartment apartment) =>
        new(apartment.DoorNumber, apartment.Title, null, null, 0m, DomainConstants.AidatInvoice.SkipReasonNoTariff);
}
