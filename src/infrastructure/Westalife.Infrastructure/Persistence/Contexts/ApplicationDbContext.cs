using System.Reflection;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Common;
using Westalife.Domain.Entities;
using Westalife.Domain.Exceptions;
using Westalife.Infrastructure.Auth;

namespace Westalife.Infrastructure.Persistence.Contexts;

/// <summary>EF Core DbContext. Tüm entity konfigürasyonları Configuration sınıflarından uygulanır.</summary>
public class ApplicationDbContext : IdentityDbContext<AppUser, AppRole, Guid>, IApplicationDbContext
{
    private readonly ITenantContext _tenant;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantContext? tenantContext = null)
        : base(options)
    {
        _tenant = tenantContext ?? new AmbientTenantContext();
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Apartment> Apartments => Set<Apartment>();
    public DbSet<ApartmentDimension> ApartmentDimensions => Set<ApartmentDimension>();
    public DbSet<ApartmentOwner> ApartmentOwners => Set<ApartmentOwner>();
    public DbSet<Meter> Meters => Set<Meter>();
    public DbSet<MeterReading> MeterReadings => Set<MeterReading>();
    public DbSet<MeterReadingAuditLog> MeterReadingAuditLogs => Set<MeterReadingAuditLog>();
    public DbSet<GasInvoice> GasInvoices => Set<GasInvoice>();
    public DbSet<ApartmentInvoice> ApartmentInvoices => Set<ApartmentInvoice>();
    public DbSet<AppSettings> AppSettings => Set<AppSettings>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
    public DbSet<AidatTariff> AidatTariffs => Set<AidatTariff>();
    public DbSet<AidatInvoice> AidatInvoices => Set<AidatInvoice>();
    public DbSet<AidatCharge> AidatCharges => Set<AidatCharge>();
    public DbSet<BudgetItem> BudgetItems => Set<BudgetItem>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<IncomeItem> IncomeItems => Set<IncomeItem>();
    public DbSet<BudgetPeriod> BudgetPeriods => Set<BudgetPeriod>();
    public DbSet<BudgetTransfer> BudgetTransfers => Set<BudgetTransfer>();
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();
    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();
    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();
    public DbSet<WaiverOffset> WaiverOffsets => Set<WaiverOffset>();
    public DbSet<ResidentDocument> ResidentDocuments => Set<ResidentDocument>();
    public DbSet<NotificationTemplate> NotificationTemplates => Set<NotificationTemplate>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<NoticePdfAccessToken> NoticePdfAccessTokens => Set<NoticePdfAccessToken>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        ApplyTenantQueryFilters(modelBuilder);
    }

    public override int SaveChanges()
    {
        StampTenant();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTenant();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void StampTenant()
    {
        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.State != EntityState.Added || entry.Entity.TenantId != Guid.Empty)
            {
                continue;
            }

            if (_tenant.TenantId == Guid.Empty)
            {
                throw new DomainException("Süper yönetici kayıt oluşturmak için X-Tenant-Id başlığı göndermelidir.");
            }

            entry.Entity.AssignTenant(_tenant.TenantId);
        }
    }

    private void ApplyTenantQueryFilters(ModelBuilder modelBuilder)
    {
        var method = typeof(ApplicationDbContext).GetMethod(
            nameof(ApplyTenantQueryFilter),
            BindingFlags.Instance | BindingFlags.NonPublic)!;

        foreach (var type in typeof(ITenantEntity).Assembly.GetTypes())
        {
            if (type.IsAbstract || !typeof(ITenantEntity).IsAssignableFrom(type))
            {
                continue;
            }

            method.MakeGenericMethod(type).Invoke(this, new object[] { modelBuilder });
        }
    }

    private void ApplyTenantQueryFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantEntity
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(entity =>
            _tenant.BypassTenantFilter || entity.TenantId == _tenant.TenantId);
    }
}
