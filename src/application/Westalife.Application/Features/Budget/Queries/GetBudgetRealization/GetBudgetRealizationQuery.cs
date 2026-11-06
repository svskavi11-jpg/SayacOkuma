using System.Globalization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;
using Westalife.Domain.Services.Budget;

namespace Westalife.Application.Features.Budget.Queries.GetBudgetRealization;

public sealed record GetBudgetRealizationQuery(int Year) : IRequest<BudgetRealizationReport>;

public sealed record BudgetRealizationRow(
    Guid BudgetItemId,
    int Year,
    string Category,
    decimal EstimatedAmount,
    decimal ActualAmount,
    decimal RemainingAmount,
    decimal UtilizationPercent,
    decimal TransferIn,
    decimal TransferOut);

public sealed record BudgetRealizationReport(
    int Year,
    string BuildingName,
    decimal TotalEstimated,
    decimal TotalActual,
    decimal TotalRemaining,
    decimal UtilizationPercent,
    IReadOnlyList<BudgetRealizationRow> Rows,
    decimal TotalEstimatedIncome,
    decimal Balance,
    bool IsLocked);

public sealed class GetBudgetRealizationQueryValidator : AbstractValidator<GetBudgetRealizationQuery>
{
    public GetBudgetRealizationQueryValidator()
    {
        RuleFor(q => q.Year)
            .InclusiveBetween(DomainConstants.Budget.YearMin, DomainConstants.Budget.YearMax)
            .WithMessage(DomainConstants.Budget.YearInvalid);
    }
}

public sealed class GetBudgetRealizationQueryHandler
    : IRequestHandler<GetBudgetRealizationQuery, BudgetRealizationReport>
{
    private readonly IApplicationDbContext _db;

    public GetBudgetRealizationQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<BudgetRealizationReport> Handle(
        GetBudgetRealizationQuery request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sistem ayarları bulunamadı.");

        if (!settings.IsBudgetTrackingEnabled)
        {
            throw new DomainException(DomainConstants.Budget.BudgetTrackingDisabled);
        }

        var items = await _db.BudgetItems.AsNoTracking()
            .Where(b => b.Year == request.Year)
            .ToListAsync(cancellationToken);

        var ids = items.Select(i => i.Id).ToList();
        var spentRows = ids.Count == 0
            ? []
            : await _db.Expenses.AsNoTracking()
                .Where(e => e.BudgetItemId != null && ids.Contains(e.BudgetItemId.Value))
                .Select(e => new { e.BudgetItemId, e.Amount })
                .ToListAsync(cancellationToken);

        var waiverRows = ids.Count == 0
            ? []
            : await _db.WaiverOffsets.AsNoTracking()
                .Where(w => !w.IsCancelled && ids.Contains(w.BudgetItemId))
                .Select(w => new { w.BudgetItemId, w.Amount })
                .ToListAsync(cancellationToken);

        var actualMap = spentRows
            .GroupBy(e => e.BudgetItemId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        foreach (var waiver in waiverRows)
        {
            actualMap[waiver.BudgetItemId] = actualMap.GetValueOrDefault(waiver.BudgetItemId) + waiver.Amount;
        }

        var transfers = ids.Count == 0
            ? []
            : await _db.BudgetTransfers.AsNoTracking()
                .Where(t => ids.Contains(t.FromBudgetItemId) || ids.Contains(t.ToBudgetItemId))
                .Select(t => new { t.FromBudgetItemId, t.ToBudgetItemId, t.Amount })
                .ToListAsync(cancellationToken);

        var transferIn = transfers.GroupBy(t => t.ToBudgetItemId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        var transferOut = transfers.GroupBy(t => t.FromBudgetItemId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        var comparer = StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: false);

        var rows = items
            .OrderBy(i => i.Category, comparer)
            .Select(i =>
            {
                var actual = actualMap.GetValueOrDefault(i.Id);
                var incoming = transferIn.GetValueOrDefault(i.Id);
                var outgoing = transferOut.GetValueOrDefault(i.Id);
                var adjusted = BudgetMath.AdjustedEstimate(i.EstimatedAmount, incoming, outgoing);
                return new BudgetRealizationRow(
                    i.Id,
                    i.Year,
                    i.Category,
                    i.EstimatedAmount,
                    actual,
                    BudgetMath.Remaining(adjusted, actual),
                    BudgetMath.UtilizationPercent(adjusted, actual),
                    incoming,
                    outgoing);
            })
            .ToList();

        var totalEstimated = rows.Sum(r => r.EstimatedAmount);
        var totalActual = rows.Sum(r => r.ActualAmount);
        var totalAdjusted = rows.Sum(r => BudgetMath.AdjustedEstimate(r.EstimatedAmount, r.TransferIn, r.TransferOut));
        var estimatedIncome = await _db.IncomeItems.AsNoTracking()
            .Where(i => i.Year == request.Year)
            .SumAsync(i => (decimal?)i.EstimatedAmount, cancellationToken) ?? 0m;
        var locked = await _db.BudgetPeriods.AsNoTracking()
            .AnyAsync(p => p.Year == request.Year && p.IsLocked, cancellationToken);

        return new BudgetRealizationReport(
            request.Year,
            settings.BuildingName,
            totalEstimated,
            totalActual,
            BudgetMath.Remaining(totalAdjusted, totalActual),
            BudgetMath.UtilizationPercent(totalAdjusted, totalActual),
            rows,
            estimatedIncome,
            BudgetMath.Balance(estimatedIncome, totalEstimated),
            locked);
    }
}
