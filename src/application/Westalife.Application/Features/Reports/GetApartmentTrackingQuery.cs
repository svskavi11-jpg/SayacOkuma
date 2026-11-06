using FluentValidation;
using MediatR;
using Westalife.Application.Common;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Services.Ledger;
using Westalife.Domain.Services.Reports;

namespace Westalife.Application.Features.Reports;

public sealed record ApartmentTrackingRow(
    Guid ApartmentId,
    string DoorNumber,
    string Title,
    decimal Devir,
    decimal ExtraAidat,
    decimal AidatAccrual,
    decimal FuelAccrual,
    decimal AidatCollected,
    decimal FuelCollected,
    decimal Waiver,
    decimal Interest,
    decimal Net,
    decimal LedgerBalance);

public sealed record ApartmentTrackingReport(
    DateOnly From,
    DateOnly To,
    string BuildingName,
    IReadOnlyList<ApartmentTrackingRow> Rows,
    ApartmentTrackingRow Totals,
    bool Reconciled,
    decimal PeriodOpening,
    decimal OpeningEntered,
    decimal OpeningCollected,
    decimal Unallocated);

public sealed record GetApartmentTrackingQuery(DateOnly From, DateOnly To) : IRequest<ApartmentTrackingReport>;

public sealed class GetApartmentTrackingQueryValidator : AbstractValidator<GetApartmentTrackingQuery>
{
    public GetApartmentTrackingQueryValidator()
    {
        RuleFor(q => q.To)
            .GreaterThanOrEqualTo(q => q.From)
            .WithMessage(DomainConstants.AuditReport.PeriodInvalid);
    }
}

public sealed class GetApartmentTrackingQueryHandler
    : IRequestHandler<GetApartmentTrackingQuery, ApartmentTrackingReport>
{
    private readonly IApplicationDbContext _db;

    public GetApartmentTrackingQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<ApartmentTrackingReport> Handle(
        GetApartmentTrackingQuery request, CancellationToken cancellationToken)
    {
        var books = await AuditBooks.LoadAsync(_db, cancellationToken);
        var dayBefore = request.From.AddDays(-1);
        var rows = new List<ApartmentTrackingRow>(books.Apartments.Count);
        foreach (var apartment in books.Apartments.OrderByDoorNumber(a => a.DoorNumber))
        {
            rows.Add(BuildRow(books, apartment, request.From, request.To, dayBefore));
        }

        var totals = Sum(rows);
        var reconciled = rows.All(r => r.Net == r.LedgerBalance);
        return new ApartmentTrackingReport(
            request.From,
            request.To,
            books.BuildingName,
            rows,
            totals,
            reconciled,
            rows.Count == 0 ? 0m : rows.Sum(r => OpeningBalance(books, books.Apartments.First(a => a.Id == r.ApartmentId), dayBefore)),
            rows.Count == 0 ? 0m : books.Apartments.Sum(a => OpeningEntered(a, request.From, request.To)),
            rows.Count == 0 ? 0m : books.Apartments.Sum(a => OpeningCollected(books, a, request.From, request.To)),
            rows.Count == 0 ? 0m : books.Apartments.Sum(a => UnallocatedInRange(books, a, request.From, request.To)));
    }

    private static ApartmentTrackingRow BuildRow(
        AuditBundle books, Apartment apartment, DateOnly from, DateOnly to, DateOnly dayBefore)
    {
        var opening = AuditBooks.Snapshot(books, apartment, dayBefore);
        var closing = AuditBooks.Snapshot(books, apartment, to);
        var openingBalance = opening.TotalBalance;
        var entered = OpeningEntered(apartment, from, to);
        var collectedOpening = AuditBooks.Collected(
            closing, from, to, AllocationTarget.OpeningBalance, AllocationTarget.Interest);
        var unallocated = AuditBooks.Unallocated(closing, from, to);
        var devir = AuditTrackingMath.DevirColumn(openingBalance, entered, collectedOpening, unallocated);
        var extra = Accrual(books, apartment.Id, from, to, AidatChargeType.OzelToplama);
        var aidat = Accrual(books, apartment.Id, from, to, AidatChargeType.StandartAidat);
        var fuel = books.Invoices
            .Where(i => i.ApartmentId == apartment.Id && AuditBooks.FuelInRange(i, from, to, null))
            .Sum(i => i.TotalAmount);
        var aidatCollected = AuditBooks.Collected(closing, from, to, AllocationTarget.Aidat);
        var fuelCollected = AuditBooks.Collected(closing, from, to, AllocationTarget.Invoice);
        var waiver = closing.Lines
            .Where(l => l.Type == LedgerDebtType.WaiverOffset
                && l.PeriodStart is { } date
                && AuditTrackingMath.InRange(date, from, to))
            .Sum(l => l.PrincipalAmount);
        var interest = closing.TotalInterestAccrued - opening.TotalInterestAccrued;
        var net = AuditTrackingMath.Net(devir, extra, aidat, fuel, interest, aidatCollected, fuelCollected, waiver);
        return new ApartmentTrackingRow(
            apartment.Id,
            apartment.DoorNumber,
            apartment.Title,
            devir,
            extra,
            aidat,
            fuel,
            aidatCollected,
            fuelCollected,
            waiver,
            interest,
            net,
            closing.TotalBalance);
    }

    private static decimal OpeningBalance(AuditBundle books, Apartment apartment, DateOnly dayBefore) =>
        AuditBooks.Snapshot(books, apartment, dayBefore).TotalBalance;

    private static decimal OpeningCollected(AuditBundle books, Apartment apartment, DateOnly from, DateOnly to)
    {
        var closing = AuditBooks.Snapshot(books, apartment, to);
        return AuditBooks.Collected(closing, from, to, AllocationTarget.OpeningBalance, AllocationTarget.Interest);
    }

    private static decimal UnallocatedInRange(AuditBundle books, Apartment apartment, DateOnly from, DateOnly to) =>
        AuditBooks.Unallocated(AuditBooks.Snapshot(books, apartment, to), from, to);

    private static decimal OpeningEntered(Apartment apartment, DateOnly from, DateOnly to)
    {
        if (apartment.OpeningBalanceDate is not { } date || !AuditTrackingMath.InRange(date, from, to))
        {
            return 0m;
        }

        var amount = 0m;
        if (apartment.OpeningBalancePrincipal != 0m)
        {
            amount += apartment.OpeningBalancePrincipal;
        }

        if (apartment.OpeningBalanceInterest != 0m)
        {
            amount += apartment.OpeningBalanceInterest;
        }

        return amount;
    }

    private static decimal Accrual(
        AuditBundle books, Guid apartmentId, DateOnly from, DateOnly to, AidatChargeType chargeType) =>
        books.Charges
            .Where(c => c.ApartmentId == apartmentId
                && !c.AidatInvoice.IsCancelled
                && c.AidatInvoice.ChargeType == chargeType
                && AuditTrackingMath.InRange(c.AidatInvoice.PeriodStart, from, to))
            .Sum(c => c.Amount);

    private static ApartmentTrackingRow Sum(IReadOnlyList<ApartmentTrackingRow> rows) =>
        new(
            Guid.Empty,
            string.Empty,
            "Toplam",
            rows.Sum(r => r.Devir),
            rows.Sum(r => r.ExtraAidat),
            rows.Sum(r => r.AidatAccrual),
            rows.Sum(r => r.FuelAccrual),
            rows.Sum(r => r.AidatCollected),
            rows.Sum(r => r.FuelCollected),
            rows.Sum(r => r.Waiver),
            rows.Sum(r => r.Interest),
            rows.Sum(r => r.Net),
            rows.Sum(r => r.LedgerBalance));
}
