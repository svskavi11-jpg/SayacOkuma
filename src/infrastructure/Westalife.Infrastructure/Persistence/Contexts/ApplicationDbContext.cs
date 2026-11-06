using Microsoft.EntityFrameworkCore;
using Westalife.Application.Common.Interfaces;
using Westalife.Domain.Entities;

namespace Westalife.Infrastructure.Persistence.Contexts;

/// <summary>EF Core DbContext. Tüm entity konfigürasyonları Configuration sınıflarından uygulanır.</summary>
public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
