using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Payments.Queries.GetLedger;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;
using Westalife.Domain.Services.Ledger;

namespace Westalife.Application.Features.Payments.Queries.GetPaymentReceipt;

public sealed record PaymentReceiptFile(byte[] Content, string FileName);

public sealed record GetPaymentReceiptQuery(Guid PaymentId, Guid? ApartmentId = null) : IRequest<PaymentReceiptFile>;

public sealed class GetPaymentReceiptQueryHandler : IRequestHandler<GetPaymentReceiptQuery, PaymentReceiptFile>
{
    private readonly IApplicationDbContext _db;
    private readonly IMediator _mediator;
    private readonly IPaymentReceiptPdf _pdf;

    public GetPaymentReceiptQueryHandler(IApplicationDbContext db, IMediator mediator, IPaymentReceiptPdf pdf)
    {
        _db = db;
        _mediator = mediator;
        _pdf = pdf;
    }

    public async Task<PaymentReceiptFile> Handle(GetPaymentReceiptQuery request, CancellationToken cancellationToken)
    {
        var payment = await _db.Payments
            .Include(p => p.Apartment)
            .Include(p => p.ApartmentOwner)
            .FirstOrDefaultAsync(p => p.Id == request.PaymentId, cancellationToken)
            ?? throw new NotFoundException(DomainConstants.Payment.NotFound);

        if (request.ApartmentId is Guid apartmentId && payment.ApartmentId != apartmentId)
        {
            throw new NotFoundException(DomainConstants.Payment.NotFound);
        }

        if (string.IsNullOrWhiteSpace(payment.ReceiptNumber))
        {
            var existingNumbers = await ReceiptNumberAllocator.LoadAsync(_db, payment.Id, cancellationToken);
            payment.AssignReceiptNumber(ReceiptNumberAllocator.Allocate(
                existingNumbers, payment.PaymentDate.Year, null));
            await _db.SaveChangesAsync(cancellationToken);
        }

        var settings = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var ledger = await _mediator.Send(new GetLedgerQuery(payment.ApartmentId), cancellationToken);
        var lines = Describe(payment, ledger);
        var buildingName = FirstText(settings?.BuildingName, settings?.CompanyName) ?? "Site";

        var bytes = _pdf.Generate(new PaymentReceiptDocument(
            buildingName,
            settings?.LogoLeft,
            settings?.LogoLeftHeightMm ?? 18,
            payment.ReceiptNumber!,
            payment.PaymentDate,
            payment.Apartment.DoorNumber,
            string.IsNullOrWhiteSpace(payment.ApartmentOwner?.OwnerName) ? "—" : payment.ApartmentOwner!.OwnerName,
            payment.Amount,
            lines,
            string.IsNullOrWhiteSpace(payment.CreatedBy) ? "—" : payment.CreatedBy,
            payment.IsCancelled));

        return new PaymentReceiptFile(bytes, $"Makbuz-{payment.ReceiptNumber}.pdf");
    }

    private static IReadOnlyList<PaymentReceiptLine> Describe(Payment payment, LedgerResult ledger)
    {
        var lines = new List<PaymentReceiptLine>();
        foreach (var line in ledger.Lines)
        {
            foreach (var allocation in line.Allocations)
            {
                if (allocation.PaymentId != payment.Id || allocation.AllocatedAmount == 0m)
                {
                    continue;
                }

                var description = string.IsNullOrWhiteSpace(line.Label)
                    ? TargetName(allocation.Target)
                    : line.Label;
                lines.Add(new PaymentReceiptLine(description, allocation.AllocatedAmount));
            }
        }

        foreach (var credit in ledger.Credits ?? [])
        {
            if (credit.PaymentId == payment.Id && credit.Amount != 0m)
            {
                lines.Add(new PaymentReceiptLine("Cari hesap alacağı", credit.Amount));
            }
        }

        if (lines.Count == 0)
        {
            lines.Add(new PaymentReceiptLine(TargetName(payment.TargetDebtType), payment.Amount));
        }

        return lines;
    }

    private static string TargetName(AllocationTarget target) => target switch
    {
        AllocationTarget.Aidat => "Aidat",
        AllocationTarget.Invoice => "Isınma faturası",
        AllocationTarget.OpeningBalance => "Devir bakiyesi",
        AllocationTarget.Interest => "Devir faizi",
        _ => "Genel tahsilat"
    };

    private static string TargetName(LedgerDebtType? target) => target switch
    {
        LedgerDebtType.Aidat => "Aidat",
        LedgerDebtType.Invoice => "Isınma faturası",
        LedgerDebtType.OpeningBalancePrincipal => "Devir bakiyesi",
        LedgerDebtType.OpeningBalanceInterest => "Devir faizi",
        _ => "Genel tahsilat"
    };

    private static string? FirstText(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
