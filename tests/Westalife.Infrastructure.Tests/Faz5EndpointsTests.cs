using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Westalife.Application;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Apartments.Commands.UpdateApartmentDimension;
using Westalife.Application.Features.Dashboard.Queries.GetDashboardSummary;
using Westalife.Application.Features.GasInvoices.Queries.GetGasInvoices;
using Westalife.Infrastructure.Persistence;
using Xunit;

namespace Westalife.Infrastructure.Tests;

/// <summary>
/// Faz 5 backend eklentileri: fatura listesi, m² güncelleme (SCD Type 2) ve dashboard özeti.
/// Gerçek PostgreSQL'e karşı çalışır.
/// </summary>
public class Faz5EndpointsTests
{
    private static (IMediator Mediator, IApplicationDbContext Db) Build()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(DesignTimeDbContextFactory.DesignTimeConnectionString);
        services.AddApplication();
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IMediator>(), provider.GetRequiredService<IApplicationDbContext>());
    }

    [Fact]
    public async Task UpdateApartmentDimension_ClosesOldRecordAndOpensNewOne()
    {
        var (mediator, db) = Build();

        // Rastgele daire (test tekrarında farklı daireler güncellenir).
        var random = new Random();
        var door = random.Next(1, 73);
        var apartmentId = await db.Apartments
            .Where(a => a.DoorNumber == door.ToString())
            .Select(a => a.Id)
            .FirstAsync();

        var result = await mediator.Send(new UpdateApartmentDimensionCommand(apartmentId, 123.45m, 0m));

        Assert.NotNull(result);

        var activeDimensions = await db.ApartmentDimensions
            .Where(d => d.ApartmentId == apartmentId && d.ValidTo == null)
            .ToListAsync();

        var supersededCount = await db.ApartmentDimensions
            .CountAsync(d => d.ApartmentId == apartmentId && d.ValidTo != null);

        Assert.Single(activeDimensions);                                  // tek aktif kayıt
        Assert.Equal(123.45m, activeDimensions[0].SquareMeters);          // yeni değer
        Assert.True(supersededCount >= 1);                                // eski kayıt EZİLMEDİ, aralığı kapandı
        Assert.NotNull(result.PreviousValidTo ?? result.ValidFrom);       // geçiş bilgisi döndü
    }

    [Fact]
    public async Task GetGasInvoices_ReturnsInvoicesWithCalculationFlag()
    {
        var (mediator, _) = Build();

        var invoices = await mediator.Send(new GetGasInvoicesQuery());

        Assert.NotEmpty(invoices);
        Assert.All(invoices, i => Assert.True(i.TotalAmount > 0));
        // Entegrasyon testi faturalarından en az biri hesaplanmış olmalı.
        Assert.Contains(invoices, i => i.HasCalculation);
    }

    [Fact]
    public async Task GetDashboardSummary_ReturnsBuildingAndApartmentCounts()
    {
        var (mediator, _) = Build();

        var summary = await mediator.Send(new GetDashboardSummaryQuery());

        Assert.Contains("Westalife", summary.BuildingName);
        Assert.Equal(72, summary.ApartmentCount);
        Assert.NotNull(summary.LastInvoiceId);
        Assert.NotNull(summary.LastInvoiceReconciled);
    }
}
