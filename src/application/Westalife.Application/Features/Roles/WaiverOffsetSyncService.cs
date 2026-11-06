using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Constants;
using Westalife.Domain.Entities;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;

namespace Westalife.Application.Features.Roles;

/// <summary>
/// Açık uçlu (Devam ediyor) veya tarih aralıklı görev atamaları ile standart aidat tahakkuklarını
/// senkronize eder; geriye dönük eksik kalmış <see cref="WaiverOffset"/> (Görev Mahsubu) kayıtlarını
/// <see cref="AppSettings.ManagementExpenseBudgetItemId"/> üzerinden seçilen bütçe kalemine bağlar,
/// kalem değiştiğinde mevcut mahsupları yeni kaleme taşır (<see cref="WaiverOffset.RebindBudgetItem"/>).
/// </summary>
public static class WaiverOffsetSyncService
{
    public static async Task SyncAllAsync(
        IApplicationDbContext db,
        CancellationToken cancellationToken,
        bool requireBudgetItemWhenCompensated = false)
    {
        var roles = await db.RoleAssignments
            .ToListAsync(cancellationToken);

        if (roles.Count == 0)
        {
            return;
        }

        var settings = await db.AppSettings
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var selectedBudgetItemId = settings?.ManagementExpenseBudgetItemId;
        BudgetItem? selectedBudgetItem = null;
        if (selectedBudgetItemId.HasValue && selectedBudgetItemId.Value != Guid.Empty)
        {
            selectedBudgetItem = await db.BudgetItems
                .FirstOrDefaultAsync(b => b.Id == selectedBudgetItemId.Value, cancellationToken);
        }

        var activeRoles = roles
            .Where(r => !r.IsCancelled && r.HasCompensation)
            .ToList();

        if (requireBudgetItemWhenCompensated && activeRoles.Count > 0 && selectedBudgetItem is null)
        {
            throw new DomainException(DomainConstants.WaiverOffset.ManagementBudgetItemNotConfigured);
        }

        var roleApartments = roles.Select(r => r.ApartmentId).Distinct().ToList();

        var charges = await db.AidatCharges
            .Include(c => c.AidatInvoice)
            .Where(c => roleApartments.Contains(c.ApartmentId)
                && !c.AidatInvoice.IsCancelled
                && c.AidatInvoice.ChargeType == AidatChargeType.StandartAidat)
            .OrderBy(c => c.AidatInvoice.PeriodStart)
            .ThenBy(c => c.CreatedOn)
            .ToListAsync(cancellationToken);

        var existingWaivers = await db.WaiverOffsets
            .Where(w => roleApartments.Contains(w.ApartmentId))
            .ToListAsync(cancellationToken);

        var lockedYears = (await db.BudgetPeriods
            .AsNoTracking()
            .Where(p => p.IsLocked)
            .Select(p => p.Year)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var changed = false;
        var now = DateTimeOffset.UtcNow;
        var roleById = roles.ToDictionary(r => r.Id);
        var chargeById = charges.ToDictionary(c => c.Id);
        var previousBudgetItemIds = new HashSet<Guid>();

        // 1. İptal edilmiş veya sonlandırılmış (EndDate verilmiş) görevlerin kapsamadığı aktif mahsupları iptal et
        foreach (var waiver in existingWaivers.Where(w => !w.IsCancelled))
        {
            if (lockedYears.Contains(waiver.OffsetDate.Year))
            {
                continue;
            }

            if (!roleById.TryGetValue(waiver.RoleAssignmentId, out var role))
            {
                continue;
            }

            if (role.IsCancelled)
            {
                waiver.Cancel(now);
                changed = true;
                continue;
            }

            if (chargeById.TryGetValue(waiver.AidatChargeId, out var charge)
                && !role.CoversPeriod(charge.AidatInvoice.PeriodStart, charge.AidatInvoice.PeriodEnd))
            {
                waiver.Cancel(now);
                changed = true;
                continue;
            }

            // Ayarlar'da seçili bütçe kalemi değiştiyse mevcut aktif mahsupları (Daire 64 dahil) yeni kaleme taşı
            if (selectedBudgetItem is not null && waiver.BudgetItemId != selectedBudgetItem.Id)
            {
                previousBudgetItemIds.Add(waiver.BudgetItemId);
                waiver.RebindBudgetItem(selectedBudgetItem.Id);
                changed = true;
            }
        }

        // 2. Seçili bir bütçe kalemi varsa eksik kalmış görev mahsubu (WaiverOffset) kayıtlarını geriye dönük tamamla
        if (selectedBudgetItem is not null && charges.Count > 0 && activeRoles.Count > 0)
        {
            var activeWaiverChargeIds = existingWaivers
                .Where(w => !w.IsCancelled)
                .Select(w => w.AidatChargeId)
                .ToHashSet();

            var annualRoleYearsApplied = existingWaivers
                .Where(w => !w.IsCancelled)
                .Select(w => (w.RoleAssignmentId, w.OffsetDate.Year))
                .ToHashSet();

            foreach (var charge in charges)
            {
                if (activeWaiverChargeIds.Contains(charge.Id))
                {
                    continue;
                }

                var year = charge.AidatInvoice.PeriodStart.Year;
                if (lockedYears.Contains(year))
                {
                    continue;
                }

                var matchingRole = activeRoles
                    .Where(r => r.ApartmentId == charge.ApartmentId
                        && r.CoversPeriod(charge.AidatInvoice.PeriodStart, charge.AidatInvoice.PeriodEnd))
                    .OrderByDescending(r => r.StartDate)
                    .FirstOrDefault();

                if (matchingRole is null)
                {
                    continue;
                }

                // Yıllık sabit tutar (Huzur Hakkı) her takvim yılında görev başına bir kez mahsup edilir
                if (matchingRole.PaymentType == RolePaymentType.SabitTutar
                    && matchingRole.PeriodType == RolePeriodType.Yillik
                    && annualRoleYearsApplied.Contains((matchingRole.Id, year)))
                {
                    continue;
                }

                var amount = WaiverOffset.AmountFor(charge.Amount, matchingRole);
                if (amount <= 0m)
                {
                    continue;
                }

                var waiver = WaiverOffset.Create(
                    charge.Id,
                    matchingRole.Id,
                    charge.ApartmentId,
                    selectedBudgetItem.Id,
                    amount,
                    charge.AidatInvoice.PeriodStart,
                    WaiverOffset.Describe(matchingRole));

                db.WaiverOffsets.Add(waiver);
                activeWaiverChargeIds.Add(charge.Id);
                if (matchingRole.PaymentType == RolePaymentType.SabitTutar
                    && matchingRole.PeriodType == RolePeriodType.Yillik)
                {
                    annualRoleYearsApplied.Add((matchingRole.Id, year));
                }

                changed = true;
            }
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);

            // Daha önce otomatik oluşturulmuş "Yönetim gideri" kaleminden başka kaleme geçildiyse ve
            // eski kalemde hiçbir mahsup/gider/virman kalmadıysa yetim kalemi temizle.
            foreach (var oldBudgetId in previousBudgetItemIds)
            {
                if (selectedBudgetItem is not null && oldBudgetId == selectedBudgetItem.Id)
                {
                    continue;
                }

                var oldItem = await db.BudgetItems
                    .FirstOrDefaultAsync(
                        b => b.Id == oldBudgetId && b.Category == DomainConstants.RoleAssignment.ManagementExpenseCategory,
                        cancellationToken);
                if (oldItem is null)
                {
                    continue;
                }

                var hasWaivers = await db.WaiverOffsets.AnyAsync(w => w.BudgetItemId == oldBudgetId, cancellationToken);
                var hasExpenses = await db.Expenses.AnyAsync(e => e.BudgetItemId == oldBudgetId, cancellationToken);
                var hasTransfers = await db.BudgetTransfers.AnyAsync(
                    t => t.FromBudgetItemId == oldBudgetId || t.ToBudgetItemId == oldBudgetId,
                    cancellationToken);

                if (!hasWaivers && !hasExpenses && !hasTransfers)
                {
                    db.BudgetItems.Remove(oldItem);
                    await db.SaveChangesAsync(cancellationToken);
                }
            }
        }
    }
}
