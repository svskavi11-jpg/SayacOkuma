using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Westalife.Application;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Apartments.Commands.AddApartments;
using Westalife.Application.Features.Apartments.Commands.UpdateApartmentOwner;
using Westalife.Application.Features.Residents.Commands.UpdateResident;
using Westalife.Domain.Enums;
using Westalife.Infrastructure.Persistence;
using Xunit;

namespace Westalife.Infrastructure.Tests;

/// <summary>
/// SCD Type 2 sakin kaydı: client-GUID'li yeni ApartmentOwner satırı Added olarak INSERT edilmeli.
/// Modified sanılırsa UPDATE 0 satır döner ve DbUpdateConcurrencyException fırlar.
/// Test verisi transaction içinde kalır ve geri alınır (paylaşılan daire verisine yazılmaz).
/// </summary>
public class UpdateResidentScdTests
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
    public async Task UpdateResident_InsertsNewRows_AndSameNameIsNoOp()
    {
        var (mediator, db) = Build();
        var ctx = (DbContext)db;
        var strategy = ctx.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await ctx.Database.BeginTransactionAsync();
            var added = await mediator.Send(new AddApartmentsCommand(1));
            var apartmentId = await db.Apartments
                .Where(a => a.DoorNumber == added.LastDoorNumber.ToString())
                .Select(a => a.Id)
                .SingleAsync();

            // ValidFrom geçmişte olmalı: handler aktif sakini UtcNow ile çözer.
            var t0 = DateTimeOffset.UtcNow.AddMinutes(-5);
            var created = await mediator.Send(new UpdateResidentCommand(
                apartmentId,
                t0,
                new ResidentInfoInput("Ev Sahibi SCD", "5551112233", "ev@example.com", null, null),
                new ResidentInfoInput("Kiraci SCD", "5559998877", null, null, null)));

            Assert.Equal("Ev Sahibi SCD", created.EvSahibiName);
            Assert.Equal("Kiraci SCD", created.KiraciName);

            var owners = await db.ApartmentOwners.Where(o => o.ApartmentId == apartmentId).ToListAsync();
            Assert.Equal(2, owners.Count);
            Assert.Contains(owners, o => o.ResidentType == ResidentType.EvSahibi && o.ValidTo == null && o.OwnerName == "Ev Sahibi SCD");
            Assert.Contains(owners, o => o.ResidentType == ResidentType.Kiraci && o.ValidTo == null && o.OwnerName == "Kiraci SCD");

            // Aynı ad: yeni satır açılmaz, telefon / e-posta / TCKN / not mevcut satıra yazılır.
            await mediator.Send(new UpdateResidentCommand(
                apartmentId,
                t0.AddMinutes(1),
                new ResidentInfoInput("Ev Sahibi SCD", "5320001122", "yeni@example.com", "10000000146", "Kapıcı notu"),
                null));

            var afterContact = await db.ApartmentOwners.Where(o => o.ApartmentId == apartmentId).ToListAsync();
            Assert.Equal(2, afterContact.Count);
            var ev = Assert.Single(afterContact, o => o.ResidentType == ResidentType.EvSahibi && o.ValidTo == null);
            Assert.Equal("5320001122", ev.PhoneNumber);
            Assert.Equal("yeni@example.com", ev.Email);
            Assert.Equal("10000000146", ev.IdentityNumber);
            Assert.Equal("Kapıcı notu", ev.Notes);

            var renamed = await mediator.Send(new UpdateResidentCommand(
                apartmentId,
                t0.AddMinutes(2),
                new ResidentInfoInput("Ev Sahibi Yeni", null, null, null, null),
                null));

            Assert.Equal("Ev Sahibi Yeni", renamed.EvSahibiName);
            Assert.Equal("Kiraci SCD", renamed.KiraciName);

            var afterRename = await db.ApartmentOwners.Where(o => o.ApartmentId == apartmentId).ToListAsync();
            Assert.Equal(3, afterRename.Count);
            Assert.Single(afterRename, o => o.ResidentType == ResidentType.EvSahibi && o.ValidTo != null && o.OwnerName == "Ev Sahibi SCD");
            Assert.Single(afterRename, o => o.ResidentType == ResidentType.EvSahibi && o.ValidTo == null && o.OwnerName == "Ev Sahibi Yeni");

            var noop = await mediator.Send(new UpdateResidentCommand(
                apartmentId,
                t0.AddMinutes(4),
                new ResidentInfoInput("ev sahibi yeni", null, null, null, null),
                null));

            Assert.Equal("Ev Sahibi Yeni", noop.EvSahibiName);
            Assert.Equal(3, await db.ApartmentOwners.CountAsync(o => o.ApartmentId == apartmentId));

            await tx.RollbackAsync();
        });
    }

    [Fact]
    public async Task UpdateOwner_DoesNotTreatActiveKiraciAsNoOp()
    {
        var (mediator, db) = Build();
        var ctx = (DbContext)db;
        var strategy = ctx.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await ctx.Database.BeginTransactionAsync();
            var added = await mediator.Send(new AddApartmentsCommand(1));
            var apartmentId = await db.Apartments
                .Where(a => a.DoorNumber == added.LastDoorNumber.ToString())
                .Select(a => a.Id)
                .SingleAsync();

            const string sharedName = "Ayni Isim SCD";
            await mediator.Send(new UpdateResidentCommand(
                apartmentId,
                DateTimeOffset.UtcNow,
                null,
                new ResidentInfoInput(sharedName, null, null, null, null)));

            var owner = await mediator.Send(new UpdateApartmentOwnerCommand(apartmentId, sharedName));

            Assert.True(owner.Changed);
            Assert.Equal(sharedName, owner.OwnerName);

            var owners = await db.ApartmentOwners.Where(o => o.ApartmentId == apartmentId).ToListAsync();
            Assert.Equal(2, owners.Count);
            Assert.Contains(owners, o => o.ResidentType == ResidentType.EvSahibi && o.ValidTo == null && o.OwnerName == sharedName);
            Assert.Contains(owners, o => o.ResidentType == ResidentType.Kiraci && o.ValidTo == null && o.OwnerName == sharedName);

            await tx.RollbackAsync();
        });
    }

    [Fact]
    public async Task UpdateResident_NonUtcValidFrom_IsStoredAsUtc()
    {
        var (mediator, db) = Build();
        var ctx = (DbContext)db;
        var strategy = ctx.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await ctx.Database.BeginTransactionAsync();
            var added = await mediator.Send(new AddApartmentsCommand(1));
            var apartmentId = await db.Apartments
                .Where(a => a.DoorNumber == added.LastDoorNumber.ToString())
                .Select(a => a.Id)
                .SingleAsync();

            var localMidnight = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.FromHours(-3));
            await mediator.Send(new UpdateResidentCommand(
                apartmentId,
                localMidnight,
                new ResidentInfoInput("Ofset Test", null, null, null, null),
                null));

            var owner = await db.ApartmentOwners.SingleAsync(o => o.ApartmentId == apartmentId);
            Assert.Equal(TimeSpan.Zero, owner.ValidFrom.Offset);
            Assert.Equal(new DateTimeOffset(2026, 11, 2, 3, 0, 0, TimeSpan.Zero), owner.ValidFrom);

            await tx.RollbackAsync();
        });
    }
}
