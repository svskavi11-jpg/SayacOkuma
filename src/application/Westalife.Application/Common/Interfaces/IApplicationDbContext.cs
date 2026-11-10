using Microsoft.EntityFrameworkCore;
using Westalife.Domain.Entities;

namespace Westalife.Application.Common.Interfaces;

/// <summary>
/// Application katmanının kalıcılık soyutlaması. Gerçek implementasyon (ApplicationDbContext)
/// Infrastructure'da yaşar — Application, EF Core Npgsql'e bağımlı değildir.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Tenant> Tenants { get; }
    DbSet<Building> Buildings { get; }
    DbSet<Apartment> Apartments { get; }
    DbSet<ApartmentDimension> ApartmentDimensions { get; }
    DbSet<ApartmentOwner> ApartmentOwners { get; }
    DbSet<Meter> Meters { get; }
    DbSet<MeterReading> MeterReadings { get; }
    DbSet<MeterReadingAuditLog> MeterReadingAuditLogs { get; }
    DbSet<GasInvoice> GasInvoices { get; }
    DbSet<ApartmentInvoice> ApartmentInvoices { get; }
    DbSet<AppSettings> AppSettings { get; }
    DbSet<Payment> Payments { get; }
    DbSet<PaymentAllocation> PaymentAllocations { get; }
    DbSet<AidatTariff> AidatTariffs { get; }
    DbSet<AidatInvoice> AidatInvoices { get; }
    DbSet<AidatCharge> AidatCharges { get; }
    DbSet<BudgetItem> BudgetItems { get; }
    DbSet<Expense> Expenses { get; }
    DbSet<IncomeItem> IncomeItems { get; }
    DbSet<BudgetPeriod> BudgetPeriods { get; }
    DbSet<BudgetTransfer> BudgetTransfers { get; }
    DbSet<BankAccount> BankAccounts { get; }
    DbSet<BankTransaction> BankTransactions { get; }
    DbSet<RoleAssignment> RoleAssignments { get; }
    DbSet<WaiverOffset> WaiverOffsets { get; }
    DbSet<ResidentDocument> ResidentDocuments { get; }
    DbSet<NotificationTemplate> NotificationTemplates { get; }
    DbSet<NotificationLog> NotificationLogs { get; }
    DbSet<NoticePdfAccessToken> NoticePdfAccessTokens { get; }
    DbSet<RolePermission> RolePermissions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

}
