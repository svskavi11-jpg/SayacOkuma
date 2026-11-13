using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.BankAccounts;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;
using Westalife.Domain.Services.Ledger;

namespace Westalife.Application.Features.Payments.Commands.CreateBulkPayment;

public sealed record CreateBulkPaymentItemDto(
    Guid ApartmentId,
    DateOnly PaymentDate,
    decimal Amount,
    string? Description,
    string? DocumentNumber = null,
    LedgerDebtType? TargetDebtType = null);

public sealed record CreateBulkPaymentCommand(
    Guid? BankAccountId,
    string? ReceivedBy,
    IReadOnlyList<CreateBulkPaymentItemDto> Items) : IRequest<CreateBulkPaymentResult>;

public sealed record CreateBulkPaymentResult(
    int CreatedCount,
    int SkippedDuplicateCount,
    decimal TotalCreatedAmount,
    IReadOnlyList<Guid> CreatedPaymentIds);

public sealed class CreateBulkPaymentCommandValidator : AbstractValidator<CreateBulkPaymentCommand>
{
    public CreateBulkPaymentCommandValidator()
    {
        RuleFor(c => c.Items)
            .NotEmpty()
            .WithMessage("Onaylanacak en az bir tahsilat satırı seçilmelidir.");

        RuleFor(c => c.ReceivedBy)
            .MaximumLength(DomainConstants.Payment.ReceivedByMaxLength)
            .When(c => c.ReceivedBy is not null);

        RuleForEach(c => c.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ApartmentId)
                .NotEmpty()
                .WithMessage("Daire kimliği zorunludur.");

            item.RuleFor(i => i.Amount)
                .GreaterThan(0m)
                .WithMessage(DomainConstants.Payment.AmountMustBePositive)
                .LessThanOrEqualTo(DomainConstants.Payment.AmountMax)
                .WithMessage(DomainConstants.Payment.AmountTooLarge);

            item.RuleFor(i => i.PaymentDate)
                .Must(d => d != default)
                .WithMessage("Tahsilat tarihi zorunludur.")
                .LessThanOrEqualTo(_ => DateOnly.FromDateTime(DateTime.Today))
                .WithMessage(DomainConstants.Payment.PaymentDateFuture);

            item.RuleFor(i => i.Description)
                .MaximumLength(DomainConstants.Payment.DescriptionMaxLength)
                .When(i => i.Description is not null);

            item.RuleFor(i => i.DocumentNumber)
                .MaximumLength(DomainConstants.Payment.DocumentNumberMaxLength)
                .When(i => i.DocumentNumber is not null);

            item.RuleFor(i => i.TargetDebtType)
                .IsInEnum()
                .Must(t => t != LedgerDebtType.WaiverOffset)
                .WithMessage(DomainConstants.RoleAssignment.NotCollectionTarget)
                .When(i => i.TargetDebtType.HasValue);
        });
    }
}

public sealed class CreateBulkPaymentCommandHandler
    : IRequestHandler<CreateBulkPaymentCommand, CreateBulkPaymentResult>
{
    private readonly IApplicationDbContext _db;

    public CreateBulkPaymentCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<CreateBulkPaymentResult> Handle(
        CreateBulkPaymentCommand request,
        CancellationToken cancellationToken)
    {
        var bankAccountId = await BankAccountLookup.ResolveAsync(
            _db,
            request.BankAccountId,
            currentBankAccountId: null,
            cancellationToken);

        var distinctApartmentIds = request.Items
            .Select(i => i.ApartmentId)
            .Distinct()
            .ToList();

        var apartments = await _db.Apartments
            .Include(a => a.Owners)
            .Where(a => distinctApartmentIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        // Sistemde olmayan bir daire gönderildiyse kaydı engelle
        foreach (var aptId in distinctApartmentIds)
        {
            if (!apartments.ContainsKey(aptId))
            {
                throw new NotFoundException(DomainConstants.RoleAssignment.ApartmentNotFound);
            }
        }

        // Mükerrer dekont numarası kontrolü
        var incomingDocs = request.Items
            .Where(i => !string.IsNullOrWhiteSpace(i.DocumentNumber))
            .Select(i => i.DocumentNumber!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existingDocs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (incomingDocs.Count > 0)
        {
            var dbDocs = await _db.Payments
                .AsNoTracking()
                .Where(p => !p.IsCancelled && p.DocumentNumber != null && incomingDocs.Contains(p.DocumentNumber))
                .Select(p => p.DocumentNumber!)
                .ToListAsync(cancellationToken);

            foreach (var d in dbDocs)
            {
                existingDocs.Add(d.Trim());
            }
        }

        var now = DateTimeOffset.UtcNow;
        var seenInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var paymentsToCreate = new List<Payment>(request.Items.Count);
        var skippedDuplicates = 0;

        foreach (var item in request.Items)
        {
            var docNo = string.IsNullOrWhiteSpace(item.DocumentNumber) ? null : item.DocumentNumber.Trim();
            if (docNo is not null && (existingDocs.Contains(docNo) || !seenInBatch.Add(docNo)))
            {
                skippedDuplicates++;
                continue;
            }

            var apartment = apartments[item.ApartmentId];
            var payerId = apartment.ResolveDebtOwner(now)?.Id;
            var legalOwnerId = apartment.GetActiveResident(ResidentType.EvSahibi, now)?.Id;

            var description = item.Description;
            if (!string.IsNullOrWhiteSpace(description) &&
                description.Length > DomainConstants.Payment.DescriptionMaxLength)
            {
                description = description[..DomainConstants.Payment.DescriptionMaxLength];
            }

            var payment = new Payment(
                apartmentId: item.ApartmentId,
                paymentDate: item.PaymentDate,
                amount: item.Amount,
                description: description,
                receivedBy: request.ReceivedBy,
                apartmentOwnerId: payerId,
                legalResponsibleOwnerId: legalOwnerId,
                targetDebtType: item.TargetDebtType,
                targetOwnerId: null,
                bankAccountId: bankAccountId,
                documentNumber: docNo);

            paymentsToCreate.Add(payment);
        }

        if (paymentsToCreate.Count > 0)
        {
            var existingNumbers = await ReceiptNumberAllocator.LoadAsync(_db, null, cancellationToken);
            foreach (var created in paymentsToCreate)
            {
                created.AssignReceiptNumber(ReceiptNumberAllocator.Allocate(
                    existingNumbers, created.PaymentDate.Year, null));
            }

            _db.Payments.AddRange(paymentsToCreate);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return new CreateBulkPaymentResult(
            CreatedCount: paymentsToCreate.Count,
            SkippedDuplicateCount: skippedDuplicates,
            TotalCreatedAmount: paymentsToCreate.Sum(p => p.Amount),
            CreatedPaymentIds: paymentsToCreate.Select(p => p.Id).ToList());
    }
}

