using FluentValidation;
using MediatR;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;

namespace Westalife.Application.Features.Reports;

public sealed record FuelBreakdownRow(
    Guid GasInvoiceId,
    string InvoiceNumber,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    string DoorNumber,
    string Title,
    decimal SquareMeters,
    decimal ConsumptionKWh,
    decimal FixedAmount,
    decimal ConsumptionAmount,
    decimal TotalAmount);

public sealed record FuelBreakdownReport(
    DateOnly From,
    DateOnly To,
    Guid? GasInvoiceId,
    decimal Total,
    decimal ApartmentTrackingFuel,
    bool Reconciled,
    IReadOnlyList<FuelBreakdownRow> Rows);

public sealed record GetFuelBreakdownQuery(DateOnly From, DateOnly To, Guid? GasInvoiceId = null)
    : IRequest<FuelBreakdownReport>;

public sealed class GetFuelBreakdownQueryValidator : AbstractValidator<GetFuelBreakdownQuery>
{
    public GetFuelBreakdownQueryValidator()
    {
        RuleFor(q => q.To)
            .GreaterThanOrEqualTo(q => q.From)
            .WithMessage(DomainConstants.AuditReport.PeriodInvalid);
    }
}

public sealed class GetFuelBreakdownQueryHandler : IRequestHandler<GetFuelBreakdownQuery, FuelBreakdownReport>
{
    private readonly IApplicationDbContext _db;

    public GetFuelBreakdownQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<FuelBreakdownReport> Handle(
        GetFuelBreakdownQuery request, CancellationToken cancellationToken)
    {
        var books = await AuditBooks.LoadAsync(_db, cancellationToken);
        var titles = books.Apartments.ToDictionary(a => a.Id, a => (a.DoorNumber, a.Title));
        var rows = books.Invoices
            .Where(i => AuditBooks.FuelInRange(i, request.From, request.To, request.GasInvoiceId))
            .Select(i =>
            {
                var apartment = titles.GetValueOrDefault(i.ApartmentId);
                return new FuelBreakdownRow(
                    i.GasInvoiceId,
                    i.GasInvoice?.InvoiceNumber ?? string.Empty,
                    AuditBooks.FuelPeriodStart(i),
                    AuditBooks.FuelPeriodEnd(i),
                    apartment.DoorNumber ?? i.ApartmentId.ToString(),
                    apartment.Title ?? string.Empty,
                    i.SquareMetersUsed,
                    i.ConsumptionKWhUsed,
                    i.FixedAmount,
                    i.ConsumptionAmount,
                    i.TotalAmount);
            })
            .OrderBy(r => r.PeriodFrom)
            .ThenBy(r => r.InvoiceNumber, StringComparer.Ordinal)
            .ThenBy(r => int.TryParse(r.DoorNumber, out var door) ? door : int.MaxValue)
            .ThenBy(r => r.DoorNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var total = rows.Sum(r => r.TotalAmount);
        var trackingFuel = books.Invoices
            .Where(i => AuditBooks.FuelInRange(i, request.From, request.To, request.GasInvoiceId))
            .Sum(i => i.TotalAmount);
        return new FuelBreakdownReport(
            request.From,
            request.To,
            request.GasInvoiceId,
            total,
            trackingFuel,
            total == trackingFuel,
            rows);
    }
}
