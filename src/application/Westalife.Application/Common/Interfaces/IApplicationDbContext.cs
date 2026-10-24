using Microsoft.EntityFrameworkCore;
using Westalife.Domain.Entities;

namespace Westalife.Application.Common.Interfaces;

/// <summary>
/// Application katmanının kalıcılık soyutlaması. Gerçek implementasyon (ApplicationDbContext)
/// Infrastructure'da yaşar — Application, EF Core Npgsql'e bağımlı değildir.
/// </summary>
public interface IApplicationDbContext
{
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

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
