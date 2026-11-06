using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Westalife.Application;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Budget.Commands.BudgetPeriod;
using Westalife.Application.Features.Budget.Commands.BudgetTransfer;
using Westalife.Application.Features.Budget.Commands.CreateBudgetItem;
using Westalife.Application.Features.Budget.Commands.IncomeItems;
using Westalife.Application.Features.Budget.Commands.CreateExpense;
using Westalife.Application.Features.Budget.Queries.GetBudgetRealization;
using Westalife.Application.Features.Roles;
using Westalife.Application.Features.Settings.BudgetSettings;
using Westalife.Application.Features.Settings.RoleFeeSettings;
using Westalife.Domain.Constants;
using Westalife.Domain.Enums;
using Westalife.Domain.Exceptions;
using Westalife.Infrastructure.Persistence;

namespace Westalife.Infrastructure.Tests;

/// <summary>
/// Faz 6.10 akışı gerçek PostgreSQL üzerinde çalışır ve transaction geri alınır.
/// Npgsql yeniden deneme stratejisi kullanıcı transaction'ını ExecuteAsync içinde ister.
/// </summary>
public class BudgetFlowTests
{
    [Fact]
    public async Task BudgetOn_WarnsAtNinetyPercent_AndRemainingIsEstimateMinusActual()
    {
        var (mediator, db) = Build();
        var ctx = (DbContext)db;
        var strategy = ctx.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await ctx.Database.BeginTransactionAsync();
            var year = DateOnly.FromDateTime(DateTime.Today).Year;
            var today = DateOnly.FromDateTime(DateTime.Today);

            await mediator.Send(new UpdateBudgetSettingsCommand(true));
            var itemId = await mediator.Send(new CreateBudgetItemCommand(year, "Asansör test", 1_000m));

            var first = await mediator.Send(new CreateExpenseCommand(today, 899.99m, "alt eşik", null, itemId));
            Assert.False(first.BudgetWarning);

            var second = await mediator.Send(new CreateExpenseCommand(today, 0.01m, "eşik", null, itemId));
            Assert.True(second.BudgetWarning);
            Assert.Equal(90m, second.UtilizationPercent);
            Assert.Contains("Asansör test", second.WarningMessage);

            var report = await mediator.Send(new GetBudgetRealizationQuery(year));
            var row = Assert.Single(report.Rows, r => r.BudgetItemId == itemId);
            Assert.Equal(1_000m, row.EstimatedAmount);
            Assert.Equal(900m, row.ActualAmount);
            Assert.Equal(100m, row.RemainingAmount);

            await tx.RollbackAsync();
        });
    }

    [Fact]
    public async Task BudgetOff_RequiresFreeText_AndRejectsBudgetItem()
    {
        var (mediator, db) = Build();
        var ctx = (DbContext)db;
        var strategy = ctx.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await ctx.Database.BeginTransactionAsync();
            await mediator.Send(new UpdateBudgetSettingsCommand(false));
            var today = DateOnly.FromDateTime(DateTime.Today);

            var ex = await Assert.ThrowsAsync<DomainException>(() =>
                mediator.Send(new CreateExpenseCommand(today, 10m, null, null, null)));
            Assert.Equal(DomainConstants.Budget.ExpenseTypeRequired, ex.Message);

            var blocked = await Assert.ThrowsAsync<DomainException>(() =>
                mediator.Send(new CreateBudgetItemCommand(today.Year, "Kapalı kalem", 100m)));
            Assert.Equal(DomainConstants.Budget.BudgetTrackingDisabled, blocked.Message);

            var saved = await mediator.Send(new CreateExpenseCommand(today, 15m, "lamba", "Elektrik", null));
            Assert.False(saved.BudgetWarning);

            await tx.RollbackAsync();
        });
    }

    [Fact]
    public async Task Lock_BlocksExpense_TransferReducesSource_BalanceIsIncomeMinusExpense()
    {
        var (mediator, db) = Build();
        var ctx = (DbContext)db;
        var strategy = ctx.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await ctx.Database.BeginTransactionAsync();
            var year = DateOnly.FromDateTime(DateTime.Today).Year;
            var today = DateOnly.FromDateTime(DateTime.Today);

            await mediator.Send(new UpdateBudgetSettingsCommand(true));
            var source = await mediator.Send(new CreateBudgetItemCommand(year, "Virman kaynak test", 1_000m));
            var target = await mediator.Send(new CreateBudgetItemCommand(year, "Virman hedef test", 100m));
            await mediator.Send(new CreateIncomeItemCommand(year, "Banka faizi test", 2_500m));

            await mediator.Send(new CreateBudgetTransferCommand(today, 200m, source, target, "asansorden"));

            var blocked = await Assert.ThrowsAsync<DomainException>(() =>
                mediator.Send(new CreateBudgetTransferCommand(today, 900m, source, target, null)));
            Assert.Equal(DomainConstants.Budget.TransferExceedsAvailable, blocked.Message);

            var report = await mediator.Send(new GetBudgetRealizationQuery(year));
            var sourceRow = Assert.Single(report.Rows, r => r.BudgetItemId == source);
            Assert.Equal(200m, sourceRow.TransferOut);
            Assert.Equal(800m, sourceRow.RemainingAmount);
            Assert.Equal(report.TotalEstimatedIncome - report.TotalEstimated, report.Balance);

            var saved = await mediator.Send(new CreateExpenseCommand(today, 10m, "belge", null, source, "MK-1", null));
            Assert.False(string.IsNullOrWhiteSpace(saved.Id.ToString()));

            await mediator.Send(new SetBudgetPeriodLockCommand(year, true));
            var locked = await Assert.ThrowsAsync<DomainException>(() =>
                mediator.Send(new CreateExpenseCommand(today, 5m, null, null, source)));
            Assert.Equal(DomainConstants.Budget.YearLocked, locked.Message);

            await tx.RollbackAsync();
        });
    }

    [Fact]
    public async Task RoleFeeBudgetItem_BlocksWhenMissing_AndRebindsExistingWaiversWhenSelected()
    {
        var (mediator, db) = Build();
        var ctx = (DbContext)db;
        var strategy = ctx.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await ctx.Database.BeginTransactionAsync();
            var year = DateOnly.FromDateTime(DateTime.Today).Year;

            await mediator.Send(new UpdateBudgetSettingsCommand(true));

            // 1. Ayarlar'da Görev Mahsup Bütçe Kalemi null yapıldığında görev ataması engellenir.
            await mediator.Send(new UpdateRoleFeeSettingsCommand(
                RolePaymentType.Oran, 1.0m, 0m, RolePeriodType.Aylik,
                RolePaymentType.Oran, 0m, 0m, RolePeriodType.Aylik,
                ManagementExpenseBudgetItemId: null));

            var apt = await db.Apartments.AsNoTracking().FirstAsync(a => a.DoorNumber == "70");
            var ex = await Assert.ThrowsAsync<DomainException>(() =>
                mediator.Send(new AssignRoleCommand(
                    apt.Id,
                    "Test Yönetici",
                    RoleType.Yonetici,
                    new DateOnly(year, 1, 1),
                    null,
                    0.5m,
                    null)));
            Assert.Equal(DomainConstants.WaiverOffset.ManagementBudgetItemNotConfigured, ex.Message);

            // 2. Özel bir bütçe kalemi ("Huzur Hakkı Test") oluşturulup Ayarlar'dan seçildiğinde
            // hem görev atanabilir hem de Daire 64 dahil mevcut tüm aktif mahsuplar yeni kaleme taşınır!
            var customBudgetItemId = await mediator.Send(
                new CreateBudgetItemCommand(year, $"Huzur Hakkı Test {Guid.NewGuid():N}"[..30], 120_000m));

            var savedSettings = await mediator.Send(new UpdateRoleFeeSettingsCommand(
                RolePaymentType.Oran, 1.0m, 0m, RolePeriodType.Aylik,
                RolePaymentType.SabitTutar, 0m, 10_000m, RolePeriodType.Yillik,
                ManagementExpenseBudgetItemId: customBudgetItemId));

            Assert.Equal(customBudgetItemId, savedSettings.ManagementExpenseBudgetItemId);
            Assert.Contains(savedSettings.AvailableBudgetItems, b => b.Id == customBudgetItemId);

            var activeWaivers = await db.WaiverOffsets.AsNoTracking()
                .Where(w => !w.IsCancelled)
                .ToListAsync();
            Assert.All(activeWaivers, w => Assert.Equal(customBudgetItemId, w.BudgetItemId));

            await tx.RollbackAsync();
        });
    }

    private static (IMediator Mediator, IApplicationDbContext Db) Build()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(DesignTimeDbContextFactory.DesignTimeConnectionString);
        services.AddApplication();
        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();
        return (
            scope.ServiceProvider.GetRequiredService<IMediator>(),
            scope.ServiceProvider.GetRequiredService<IApplicationDbContext>());
    }
}
