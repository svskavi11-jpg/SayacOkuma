using ClosedXML.Excel;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Westalife.Application;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Apartments.Commands.ImportMetersFromExcel;
using Westalife.Application.Features.GasInvoices.Commands.AddGasInvoice;
using Westalife.Domain.Entities;
using Westalife.Infrastructure.Persistence;
using Xunit;

namespace Westalife.Infrastructure.Tests;

/// <summary>
/// m² guncelleme kurali: faturalanmamis (ilk kurulum) daire -> uzerine yaz; faturalanmis daire ->
/// SCD Type 2 (eski kayit ValidTo ile kapanir, yeni kayit acilir; eski deger korunur).
/// Her test kendi benzersiz kapi numarali, izole bir daire olusturur (paylasilan DB'den etkilenmez).
/// </summary>
public class SquareMetersScdTests
{
    private const decimal PlaceholderM2 = 100m;
    private const decimal ImportedM2 = 78.4m;
    private const int BilledPeriodSlot = 6;
    private const int UnbilledDoor = 71;
    private const int BilledDoor = 72;

    private static (IMediator Mediator, IApplicationDbContext Db) Build()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(DesignTimeDbContextFactory.DesignTimeConnectionString);
        services.AddApplication();
        var provider = services.BuildServiceProvider();
        return (provider.GetRequiredService<IMediator>(), provider.GetRequiredService<IApplicationDbContext>());
    }

    private static byte[] BuildExcel(int door, string serial, decimal squareMeters)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Sayac Listesi");
        string[] headers = ["Kapı No", "Sayaç Seri No", "M-Bus Adresi", "m²"];
        for (var c = 0; c < headers.Length; c++)
        {
            ws.Cell(1, c + 1).Value = headers[c];
        }

        ws.Cell(2, 1).Value = door.ToString();
        ws.Cell(2, 2).Value = serial;
        ws.Cell(2, 3).Value = "1";
        ws.Cell(2, 4).Value = squareMeters.ToString("0.000", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Deterministik baslangic durumu: seed dairesinin tum fatura satirlari/m² kayitlari silinir ve tek bir
    /// 100 m² yer tutucu aktif kayit (2026-01-01'den itibaren) kurulur. Kapi no sabittir (import 1-72 kabul eder);
    /// assembly'de test paralelligi kapali oldugundan baska test araya girmez.
    /// </summary>
    private static async Task<Guid> ResetApartmentAsync(IApplicationDbContext db, int door)
    {
        var apartment = await db.Apartments.FirstAsync(a => a.DoorNumber == door.ToString());

        await db.ApartmentInvoices.Where(a => a.ApartmentId == apartment.Id).ExecuteDeleteAsync();
        await db.ApartmentDimensions.Where(d => d.ApartmentId == apartment.Id).ExecuteDeleteAsync();

        var fresh = new ApartmentDimension(apartment.Id, PlaceholderM2, 0m, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        db.ApartmentDimensions.Add(fresh);
        await db.SaveChangesAsync();
        return apartment.Id;
    }
    [Fact]
    public async Task Import_UnbilledApartment_OverwritesPlaceholder_WithoutHistory()
    {
        var (mediator, db) = Build();
        var apartmentId = await ResetApartmentAsync(db, UnbilledDoor);

        await mediator.Send(new ImportMetersFromExcelCommand(
            BuildExcel(UnbilledDoor, "SCDU71", ImportedM2)));

        var dimensions = await db.ApartmentDimensions.Where(d => d.ApartmentId == apartmentId).ToListAsync();
        var only = Assert.Single(dimensions);
        Assert.Equal(ImportedM2, only.SquareMeters);
        Assert.Null(only.ValidTo);
    }

    [Fact]
    public async Task Import_BilledApartment_ClosesOldRecord_AndOpensNewScdRecord()
    {
        var (mediator, db) = Build();
        var apartmentId = await ResetApartmentAsync(db, BilledDoor);

        // Daireyi faturalanmis yap: sabit tarihli bir fatura + o daireye ait fatura satiri.
        var (from, to) = await TestPeriods.FixedFreePeriodAsync(db, BilledPeriodSlot);
        var gasInvoiceId = await mediator.Send(new AddGasInvoiceCommand(
            $"SCDB{Random.Shared.Next(100000, 999999)}", "10060828", "2160191", from, to,
            to.AddDays(5), 693.25m, 19126.000m, 1.22588m,
            600m, 108m, 400m, 72m, 1180m));
        db.ApartmentInvoices.Add(new ApartmentInvoice(
            gasInvoiceId, apartmentId, PlaceholderM2, 0m, 1000, 1000, false, to));
        await db.SaveChangesAsync();

        await mediator.Send(new ImportMetersFromExcelCommand(
            BuildExcel(BilledDoor, "SCDB72", ImportedM2)));

        var dimensions = await db.ApartmentDimensions
            .Where(d => d.ApartmentId == apartmentId)
            .OrderBy(d => d.ValidFrom)
            .ToListAsync();

        Assert.Equal(2, dimensions.Count);
        Assert.Equal(PlaceholderM2, dimensions[0].SquareMeters);
        Assert.NotNull(dimensions[0].ValidTo);
        Assert.Equal(ImportedM2, dimensions[1].SquareMeters);
        Assert.Null(dimensions[1].ValidTo);
        Assert.Equal(dimensions[0].ValidTo, dimensions[1].ValidFrom);

        // Gecmis fatura satiri eski m²'yi korur.
        var billedRow = await db.ApartmentInvoices.FirstAsync(a => a.ApartmentId == apartmentId);
        Assert.Equal(PlaceholderM2, billedRow.SquareMetersUsed);
    }
}
