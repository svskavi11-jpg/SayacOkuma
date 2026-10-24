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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
