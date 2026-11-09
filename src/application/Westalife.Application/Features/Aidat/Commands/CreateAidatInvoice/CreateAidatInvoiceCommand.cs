using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Roles;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Aidat.Commands.CreateAidatInvoice;

public sealed record CreateAidatInvoiceCommand(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    DateOnly DueDate,
    AidatDistributionMethod DistributionMethod,
    decimal? EqualAmount = null,
    string? Note = null,
    AidatChargeType ChargeType = AidatChargeType.StandartAidat,
    string? Title = null,
    decimal? TargetAmount = null) : IRequest<CreateAidatInvoiceResult>;

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
    IReadOnlyList<CreateAidatInvoiceSkippedApartment> SkippedApartments,
    IReadOnlyList<Guid> ChargedApartmentIds,
    bool CanNotify,
    string? NotifyBlockReason);

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

        RuleFor(c => c.Title)
            .NotEmpty()
            .WithMessage(DomainConstants.AidatInvoice.FundTitleRequired)
            .MaximumLength(DomainConstants.AidatInvoice.TitleMaxLength)
            .WithMessage(DomainConstants.AidatInvoice.FundTitleTooLong)
            .When(c => c.ChargeType == AidatChargeType.OzelToplama);

        RuleFor(c => c.TargetAmount)
            .NotNull()
            .GreaterThan(0m)
            .WithMessage(DomainConstants.AidatInvoice.FundTargetRequired)
            .When(c => c.ChargeType == AidatChargeType.OzelToplama);
    }
}

public sealed class CreateAidatInvoiceCommandHandler : IRequestHandler<CreateAidatInvoiceCommand, CreateAidatInvoiceResult>
{
    private readonly IApplicationDbContext _db;

    public CreateAidatInvoiceCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CreateAidatInvoiceResult> Handle(
        CreateAidatInvoiceCommand request, CancellationToken cancellationToken)
    {
        // 1. Dönem çakışması engeli (fatura dönemlerindeki kuralın aynısı — iptal edilmiş tahakkuklar hariç).
        if (request.ChargeType != AidatChargeType.OzelToplama)
        {
            var overlap = await _db.AidatInvoices
                .AnyAsync(
                    i => !i.IsCancelled
                        && i.ChargeType != AidatChargeType.OzelToplama
                        && i.PeriodStart < request.PeriodEnd
                        && i.PeriodEnd > request.PeriodStart,
                    cancellationToken);

            if (overlap)
            {
                throw new DomainException(DomainConstants.ValidationMessages.AidatPeriodOverlap);
            }
        }

        // 2. Eşit dağıtım için daire başına tutar zorunlu.
        if (request.DistributionMethod == AidatDistributionMethod.Esit &&
            (request.EqualAmount is null || request.EqualAmount <= 0m))
        {
            throw new DomainException(DomainConstants.ValidationMessages.AidatEqualAmountRequired);
        }

        // 3. Aktif daireler — tarife, SCD m² kayıtları ve sakinler (SCD Type 2) ile.
        var apartments = await _db.Apartments
            .AsNoTracking()
            .Include(a => a.AidatTariff)
            .Include(a => a.Dimensions)
            .Include(a => a.Owners)
            .Where(a => a.IsActive)
            .ToListAsync(cancellationToken);

        var invoice = new AidatInvoice(
            request.PeriodStart,
            request.PeriodEnd,
            request.DueDate,
            request.DistributionMethod,
            request.Note);

        if (request.ChargeType == AidatChargeType.OzelToplama)
        {
            invoice.MarkAsSpecialCollection(request.Title ?? string.Empty, request.TargetAmount ?? 0m);
        }

        var skipped = new List<CreateAidatInvoiceSkippedApartment>();

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

                    AssignHibritOwner(apartment, invoice.AddCharge(apartment.Id, apartment.AidatTariff.Amount));
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

                    var unitPrice = apartment.AidatTariff.Amount / apartment.AidatTariff.SquareMeters;
                    AssignHibritOwner(apartment, invoice.AddCharge(
                        apartment.Id, decimal.Round(activeDimension.SquareMeters * unitPrice, 2)));
                    break;

                case AidatDistributionMethod.Esit:
                    AssignHibritOwner(apartment, invoice.AddCharge(apartment.Id, request.EqualAmount!.Value));
                    break;
            }
        }

        if (invoice.Charges.Count == 0)
        {
            throw new DomainException(DomainConstants.ValidationMessages.AidatInvoiceNoCharges);
        }

        // Standart aidat döneminde muafiyetli / sabit ücretli aktif görev varsa Görev Mahsup Bütçe Kalemi seçilmiş olmalıdır.
        if (request.ChargeType == AidatChargeType.StandartAidat)
        {
            var apartmentIds = invoice.Charges.Select(c => c.ApartmentId).Distinct().ToList();
            var activeRoles = await _db.RoleAssignments.AsNoTracking()
                .Where(r => apartmentIds.Contains(r.ApartmentId) && !r.IsCancelled)
                .ToListAsync(cancellationToken);

            var hasMatchingCompensatedRole = activeRoles.Any(
                r => r.HasCompensation && r.CoversPeriod(invoice.PeriodStart, invoice.PeriodEnd));

            if (hasMatchingCompensatedRole)
            {
                var settings = await _db.AppSettings.AsNoTracking()
                    .OrderBy(s => s.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                var budgetItemId = settings?.ManagementExpenseBudgetItemId;
                if (!budgetItemId.HasValue || budgetItemId.Value == Guid.Empty)
                {
                    throw new DomainException(DomainConstants.WaiverOffset.ManagementBudgetItemNotConfigured);
                }

                var budgetItemExists = await _db.BudgetItems.AsNoTracking()
                    .AnyAsync(b => b.Id == budgetItemId.Value, cancellationToken);
                if (!budgetItemExists)
                {
                    throw new DomainException(DomainConstants.WaiverOffset.ManagementBudgetItemNotConfigured);
                }
            }
        }

        _db.AidatInvoices.Add(invoice);
        await _db.SaveChangesAsync(cancellationToken);

        await WaiverOffsetSyncService.SyncAllAsync(_db, cancellationToken);

        var chargedIds = invoice.Charges.Select(c => c.ApartmentId).Distinct().ToList();
        var canNotify = chargedIds.Count > 0;
        return new CreateAidatInvoiceResult(
            invoice.Id,
            invoice.PeriodStart,
            invoice.PeriodEnd,
            invoice.DueDate,
            invoice.DistributionMethod,
            invoice.Charges.Count,
            skipped.Count,
            invoice.TotalAmount,
            skipped,
            chargedIds,
            canNotify,
            canNotify ? null : "Bu dönem için tahakkuk edilen daire yok, bildirim gönderilemez.");
    }

    private static void AssignHibritOwner(Apartment apartment, AidatCharge charge)
    {
        var now = DateTimeOffset.UtcNow;
        charge.AssignOwner(
            apartment.ResolveDebtOwner(now)?.Id,
            apartment.GetActiveResident(ResidentType.EvSahibi, now)?.Id);
    }
}
