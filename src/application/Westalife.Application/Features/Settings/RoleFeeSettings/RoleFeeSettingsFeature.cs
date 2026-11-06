using System.Globalization;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Roles;
using Westalife.Domain.Constants;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Settings.RoleFeeSettings;

public sealed record RoleFeeBudgetItemOption(
    Guid Id,
    int Year,
    string Category,
    decimal EstimatedAmount);

/// <summary>
/// Varsayılan görev ücretleri (Yönetici ve Denetçi için Oran veya Sabit Tutar / Huzur Hakkı)
/// ve Görev Mahsup Bütçe Kalemi (<see cref="ManagementExpenseBudgetItemId"/>) ayarları.
/// </summary>
public sealed record RoleFeeSettingsResponse(
    RolePaymentType ManagerPaymentType,
    decimal ManagerExemptionRate,
    decimal ManagerFixedAmount,
    RolePeriodType ManagerPeriodType,
    RolePaymentType AuditorPaymentType,
    decimal AuditorExemptionRate,
    decimal AuditorFixedAmount,
    RolePeriodType AuditorPeriodType,
    Guid? ManagementExpenseBudgetItemId,
    IReadOnlyList<RoleFeeBudgetItemOption> AvailableBudgetItems);

public sealed record GetRoleFeeSettingsQuery : IRequest<RoleFeeSettingsResponse>;

public sealed class GetRoleFeeSettingsQueryHandler
    : IRequestHandler<GetRoleFeeSettingsQuery, RoleFeeSettingsResponse>
{
    private readonly IApplicationDbContext _db;

    public GetRoleFeeSettingsQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<RoleFeeSettingsResponse> Handle(
        GetRoleFeeSettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sistem ayarları bulunamadı.");

        var budgetItems = await LoadAvailableBudgetItemsAsync(_db, cancellationToken);

        return new RoleFeeSettingsResponse(
            settings.DefaultManagerPaymentType,
            settings.DefaultManagerExemptionRate,
            settings.DefaultManagerFixedAmount,
            settings.DefaultManagerPeriodType,
            settings.DefaultAuditorPaymentType,
            settings.DefaultAuditorExemptionRate,
            settings.DefaultAuditorFixedAmount,
            settings.DefaultAuditorPeriodType,
            settings.ManagementExpenseBudgetItemId,
            budgetItems);
    }

    internal static async Task<IReadOnlyList<RoleFeeBudgetItemOption>> LoadAvailableBudgetItemsAsync(
        IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var raw = await db.BudgetItems.AsNoTracking()
            .Select(b => new RoleFeeBudgetItemOption(b.Id, b.Year, b.Category, b.EstimatedAmount))
            .ToListAsync(cancellationToken);

        var comparer = StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: false);
        return raw
            .OrderByDescending(b => b.Year)
            .ThenBy(b => b.Category, comparer)
            .ToList();
    }
}

public sealed record UpdateRoleFeeSettingsCommand(
    RolePaymentType ManagerPaymentType,
    decimal ManagerExemptionRate,
    decimal ManagerFixedAmount,
    RolePeriodType ManagerPeriodType,
    RolePaymentType AuditorPaymentType,
    decimal AuditorExemptionRate,
    decimal AuditorFixedAmount,
    RolePeriodType AuditorPeriodType,
    Guid? ManagementExpenseBudgetItemId = null) : IRequest<RoleFeeSettingsResponse>;

public sealed class UpdateRoleFeeSettingsCommandValidator
    : AbstractValidator<UpdateRoleFeeSettingsCommand>
{
    public UpdateRoleFeeSettingsCommandValidator()
    {
        RuleFor(c => c.ManagerPaymentType).IsInEnum().WithMessage(DomainConstants.RoleAssignment.PaymentTypeInvalid);
        RuleFor(c => c.ManagerPeriodType).IsInEnum().WithMessage(DomainConstants.RoleAssignment.PeriodTypeRequired);
        RuleFor(c => c.ManagerExemptionRate)
            .InclusiveBetween(0m, 1m)
            .WithMessage(DomainConstants.RoleAssignment.RateInvalid);
        RuleFor(c => c.ManagerFixedAmount)
            .InclusiveBetween(0m, DomainConstants.Budget.AmountMax)
            .WithMessage(DomainConstants.RoleAssignment.FixedAmountInvalid);

        RuleFor(c => c.AuditorPaymentType).IsInEnum().WithMessage(DomainConstants.RoleAssignment.PaymentTypeInvalid);
        RuleFor(c => c.AuditorPeriodType).IsInEnum().WithMessage(DomainConstants.RoleAssignment.PeriodTypeRequired);
        RuleFor(c => c.AuditorExemptionRate)
            .InclusiveBetween(0m, 1m)
            .WithMessage(DomainConstants.RoleAssignment.RateInvalid);
        RuleFor(c => c.AuditorFixedAmount)
            .InclusiveBetween(0m, DomainConstants.Budget.AmountMax)
            .WithMessage(DomainConstants.RoleAssignment.FixedAmountInvalid);
    }
}

public sealed class UpdateRoleFeeSettingsCommandHandler
    : IRequestHandler<UpdateRoleFeeSettingsCommand, RoleFeeSettingsResponse>
{
    private readonly IApplicationDbContext _db;

    public UpdateRoleFeeSettingsCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<RoleFeeSettingsResponse> Handle(
        UpdateRoleFeeSettingsCommand request, CancellationToken cancellationToken)
    {
        var settings = await _db.AppSettings.FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("Sistem ayarları bulunamadı.");

        if (request.ManagementExpenseBudgetItemId.HasValue
            && request.ManagementExpenseBudgetItemId.Value != Guid.Empty)
        {
            var exists = await _db.BudgetItems.AsNoTracking()
                .AnyAsync(b => b.Id == request.ManagementExpenseBudgetItemId.Value, cancellationToken);
            if (!exists)
            {
                throw new NotFoundException(DomainConstants.Budget.BudgetItemNotFound);
            }
        }

        settings.UpdateRoleFeeDefaults(
            request.ManagerPaymentType,
            request.ManagerExemptionRate,
            request.ManagerFixedAmount,
            request.ManagerPeriodType,
            request.AuditorPaymentType,
            request.AuditorExemptionRate,
            request.AuditorFixedAmount,
            request.AuditorPeriodType,
            request.ManagementExpenseBudgetItemId);

        await _db.SaveChangesAsync(cancellationToken);

        // Yeni seçilen bütçe kalemiyle tüm aktif görev mahsuplarını (Daire 64 dahil) senkronize et ve taşı.
        await WaiverOffsetSyncService.SyncAllAsync(_db, cancellationToken);

        var budgetItems = await GetRoleFeeSettingsQueryHandler.LoadAvailableBudgetItemsAsync(_db, cancellationToken);

        return new RoleFeeSettingsResponse(
            settings.DefaultManagerPaymentType,
            settings.DefaultManagerExemptionRate,
            settings.DefaultManagerFixedAmount,
            settings.DefaultManagerPeriodType,
            settings.DefaultAuditorPaymentType,
            settings.DefaultAuditorExemptionRate,
            settings.DefaultAuditorFixedAmount,
            settings.DefaultAuditorPeriodType,
            settings.ManagementExpenseBudgetItemId,
            budgetItems);
    }
}
