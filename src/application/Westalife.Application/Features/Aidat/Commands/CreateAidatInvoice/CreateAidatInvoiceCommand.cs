using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Aidat.Commands.CreateAidatInvoice;

/// <summary>
/// Faz 6.9: Bir dönem için aidat tahakkuku oluşturur. Aktif tüm daireleri gezip
/// seçilen dağıtım yöntemine göre <see cref="AidatCharge"/> borç satırları yazar:
/// <list type="bullet">
///   <item>TarifeBazli: dairenin atanmış tarifesinin aylık tutarı. Tarifesiz daire ATLANIR ve raporlanır.</item>
///   <item>Metrekare: birim m² fiyatı tarifenin tutarından (tarife tutarı / tarife m²),
///     dairenin aktif <see cref="ApartmentDimension"/> m²'si ile çarpılır. Tarifesi veya m²'si olmayan daire atlanır.</item>
///   <item>Esit: tüm dairelere komutta verilen <paramref name="EqualAmount"/> kadar.</item>
/// </list>
/// Dönem çakışması engellenir (mevcut tahakkukla kesişen dönem kabul edilmez).
/// Tüm tutarlar decimal ve kuruş hassasiyetindedir.
/// </summary>
public sealed record CreateAidatInvoiceCommand(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly DueDate,
    AidatDistributionMethod DistributionMethod,
    decimal? EqualAmount = null,
    string? Note = null) : IRequest<CreateAidatInvoiceResult>;

public sealed record CreateAidatInvoiceSkippedApartment(string DoorNumber, string Reason);

public sealed record CreateAidatInvoiceResult(
    Guid AidatInvoiceId,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly DueDate,
    AidatDistributionMethod DistributionMethod,
    int ChargeCount,
    int SkippedCount,
    decimal TotalAmount,
    IReadOnlyList<CreateAidatInvoiceSkippedApartment> SkippedApartments);

public sealed class CreateAidatInvoiceCommandValidator : AbstractValidator<CreateAidatInvoiceCommand>
{
    public CreateAidatInvoiceCommandValidator()
    {
        RuleFor(c => c.PeriodStart).NotEmpty();

        RuleFor(c => c.PeriodEnd)
            .NotEmpty()
            .GreaterThanOrEqualTo(c => c.PeriodStart)
            .WithMessage(DomainConstants.ValidationMessages.AidatPeriodInvalid);

        RuleFor(c => c.DueDate).NotEmpty();

        RuleFor(c => c.DistributionMethod).IsInEnum();

        RuleFor(c => c.EqualAmount)
            .GreaterThan(0m)
            .WithMessage(DomainConstants.ValidationMessages.AidatEqualAmountRequired)
            .When(c => c.DistributionMethod == AidatDistributionMethod.Esit);

        RuleFor(c => c.Note)
            .MaximumLength(DomainConstants.AidatInvoice.NoteMaxLength)
            .When(c => c.Note is not null);
    }
}

// ── Handler ──────────────────────────────────────────────────────────────────

public sealed class CreateAidatInvoiceCommandHandler : IRequestHandler<CreateAidatInvoiceCommand, CreateAidatInvoiceResult>
{
    private readonly IApplicationDbContext _db;

    public CreateAidatInvoiceCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CreateAidatInvoiceResult> Handle(
        CreateAidatInvoiceCommand request, CancellationToken cancellationToken)
    {
        // 1. Dönem çakışması engeli (fatura dönemlerindeki kuralın aynısı).
        var overlap = await _db.AidatInvoices
            .AnyAsync(i => i.PeriodStart < request.PeriodEnd && i.PeriodEnd > request.PeriodStart, cancellationToken);

        if (overlap)
        {
            throw new DomainException(DomainConstants.ValidationMessages.AidatPeriodOverlap);
        }

        // 2. Eşit dağıtım için daire başına tutar zorunlu.
        if (request.DistributionMethod == AidatDistributionMethod.Esit &&
            (request.EqualAmount is null || request.EqualAmount <= 0m))
        {
            throw new DomainException(DomainConstants.ValidationMessages.AidatEqualAmountRequired);
        }

        // 3. Aktif daireler — tarife ve SCD m² kayıtlarıyla.
        var apartments = await _db.Apartments
            .AsNoTracking()
            .Include(a => a.AidatTariff)
            .Include(a => a.Dimensions)
            .Where(a => a.IsActive)
            .ToListAsync(cancellationToken);

        var invoice = new AidatInvoice(
            request.PeriodStart,
            request.PeriodEnd,
            request.DueDate,
            request.DistributionMethod,
            request.Note);

        var skipped = new List<CreateAidatInvoiceSkippedApartment>();

        // Kapı numarası sayısal sırada (1, 2, ... 10, 11).
        foreach (var apartment in apartments.OrderByDoorNumber(a => a.DoorNumber))
        {
            switch (request.DistributionMethod)
            {
                case AidatDistributionMethod.TarifeBazli:
                    if (apartment.AidatTariff is null)
                    {
                        skipped.Add(new CreateAidatInvoiceSkippedApartment(
                            apartment.DoorNumber, DomainConstants.AidatInvoice.SkipReasonNoTariff));
                        continue;
                    }

                    invoice.AddCharge(apartment.Id, apartment.AidatTariff.Amount);
                    break;

                case AidatDistributionMethod.Metrekare:
                    if (apartment.AidatTariff is null)
                    {
                        skipped.Add(new CreateAidatInvoiceSkippedApartment(
                            apartment.DoorNumber, DomainConstants.AidatInvoice.SkipReasonNoTariff));
                        continue;
                    }

                    var activeDimension = apartment.Dimensions.FirstOrDefault(d => d.ValidTo is null);
                    if (activeDimension is null || activeDimension.SquareMeters <= 0m)
                    {
                        skipped.Add(new CreateAidatInvoiceSkippedApartment(
                            apartment.DoorNumber, DomainConstants.AidatInvoice.SkipReasonNoDimension));
                        continue;
                    }

                    // Birim m² fiyatı tarifenin tutarından türetilir: tarife tutarı / tarife m².
                    var unitPrice = apartment.AidatTariff.Amount / apartment.AidatTariff.SquareMeters;
                    invoice.AddCharge(apartment.Id, decimal.Round(activeDimension.SquareMeters * unitPrice, 2));
                    break;

                case AidatDistributionMethod.Esit:
                    invoice.AddCharge(apartment.Id, request.EqualAmount!.Value);
                    break;
            }
        }

        // Hiç daire borcu yazılamadıysa tahakkuk oluşturma (boş dönem kaydı oluşmasın).
        if (invoice.Charges.Count == 0)
        {
            throw new DomainException(DomainConstants.ValidationMessages.AidatInvoiceNoCharges);
        }

        _db.AidatInvoices.Add(invoice);
        await _db.SaveChangesAsync(cancellationToken);

        return new CreateAidatInvoiceResult(
            invoice.Id,
            invoice.PeriodStart,
            invoice.PeriodEnd,
            invoice.DueDate,
            invoice.DistributionMethod,
            invoice.Charges.Count,
            skipped.Count,
            invoice.TotalAmount,
            skipped);
    }
}
