using System.Text;
using ClosedXML.Excel;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Westalife.Application;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Features.Payments.Commands.CreateBulkPayment;
using Westalife.Application.Features.Payments.Commands.ParseBankStatement;
using Westalife.Domain.Services.Ledger;
using Westalife.Infrastructure.Persistence;
using Westalife.Infrastructure.Reporting.Excel;

namespace Westalife.Infrastructure.Tests;

public class BankStatementImportTests
{
    private static readonly IReadOnlySet<string> ValidDoors =
        Enumerable.Range(1, 72).Select(i => i.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);

    [Theory]
    [InlineData("FAST-TR450006200014200006298723-AHMET YILMAZ-DAİRE 14 KASIM AİDAT", "14")]
    [InlineData("05.11.2026 TARİHLİ EFT - 23 NOLU DAİRE DOĞALGAZ BORCU", "23")]
    [InlineData("CEP ŞUBE HAVALE D:8 ISINMA BEDELİ REF:984512365", "8")]
    [InlineData("EFT GELEN - MEHMET KAYA - DEMİRBAŞ KATKI PAYI NO: 42", "42")]
    [InlineData("GARANTİ FAST - KAPI NO: 07 AİDAT ÖDEMESİ 1.500,00 TL", "7")]
    [InlineData("TEB HAVALE - 64 NO LU DAİRE YAKIT PAYI 2026", "64")]
    public void ExtractDoorNumber_MasksNoiseAndExtractsCorrectDoor(string description, string expectedDoor)
    {
        var actual = BankStatementMatcher.ExtractDoorNumber(description, ValidDoors);
        Assert.Equal(expectedDoor, actual);
    }

    [Theory]
    [InlineData("TR140006200019200006298725 06.11.2026 REF:142536987 EFT ÖDEMESİ")]
    [InlineData("DAİRE 99 AİDAT ÖDEMESİ")] // 99 sistemde yok (1..72)
    public void ExtractDoorNumber_NoiseOnlyOrInvalidDoor_ReturnsNull(string description)
    {
        var actual = BankStatementMatcher.ExtractDoorNumber(description, ValidDoors);
        Assert.Null(actual);
    }

    [Fact]
    public void ExtractDoorNumber_WhenNoDoorNumberInText_FallsBackToUniqueResidentName()
    {
        var residents = new List<BankStatementMatcher.ResidentLookupItem>
        {
            new(
                Guid.NewGuid(),
                "19",
                Guid.NewGuid(),
                "Zeynep Demir",
                BankStatementMatcher.NormalizeTurkishUpper("Zeynep Demir"))
        };

        var door = BankStatementMatcher.ExtractDoorNumber(
            "FAST GELEN - ZEYNEP DEMİR - KASIM AİDAT ÖDEMESİ",
            ValidDoors,
            residents);

        Assert.Equal("19", door);
    }

    [Theory]
    [InlineData("DAİRE 12 KASIM AİDAT ÖDEMESİ", LedgerDebtType.Aidat)]
    [InlineData("15 NOLU DAİRE DOĞALGAZ FATURA ÖDEMESİ", LedgerDebtType.Invoice)]
    [InlineData("D-4 ISINMA / YAKIT BEDELİ", LedgerDebtType.Invoice)]
    [InlineData("D:9 GEÇMİŞ DÖNEM DEVİR BORCU", LedgerDebtType.OpeningBalancePrincipal)]
    [InlineData("DAİRE 20 DEMİRBAŞ ÖDEMESİ", null)]
    [InlineData("DAİRE 20 AİDAT VE DOĞALGAZ ÖDEMESİ", null)]
    public void InferTargetDebtType_ReturnsExpectedType(string description, LedgerDebtType? expected)
    {
        var actual = BankStatementMatcher.InferTargetDebtType(description);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void BankStatementParser_ExcelWithMetadataRowsAndNegativeRows_ExtractsOnlyIncomingRows()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Hesap Hareketleri");

        // Üst banka meta satırları (Garanti / TEB formatı)
        ws.Cell(1, 1).Value = "GARANTİ BBVA HESAP EKSTRESİ";
        ws.Cell(2, 1).Value = "Müşteri: WESTALİFE SİTE YÖNETİMİ";

        // 4. satırda gerçek tablo başlığı
        ws.Cell(4, 1).Value = "İşlem Tarihi";
        ws.Cell(4, 2).Value = "Açıklama";
        ws.Cell(4, 3).Value = "Tutar";
        ws.Cell(4, 4).Value = "Dekont No";

        // Gelen tahsilat 1
        ws.Cell(5, 1).Value = "05.11.2026";
        ws.Cell(5, 2).Value = "FAST - DAİRE 12 KASIM AİDAT";
        ws.Cell(5, 3).Value = "1.500,00 TL";
        ws.Cell(5, 4).Value = "DK-2026-001";

        // Giden ödeme (negatif) -> otomatik elenmeli
        ws.Cell(6, 1).Value = "05.11.2026";
        ws.Cell(6, 2).Value = "DOĞALGAZ FİRMASI ÖDEMESİ";
        ws.Cell(6, 3).Value = "-45.000,00 TL";
        ws.Cell(6, 4).Value = "DK-2026-002";

        // Gelen tahsilat 2
        ws.Cell(7, 1).Value = "06.11.2026";
        ws.Cell(7, 2).Value = "25 NOLU DAİRE ISINMA PAYI";
        ws.Cell(7, 3).Value = "2.345,67";
        ws.Cell(7, 4).Value = "DK-2026-003";

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var parser = new BankStatementParser();
        var result = parser.Parse(stream, "garanti_ekstre.xlsx");

        Assert.Empty(result.FileErrors);
        Assert.Equal(2, result.Rows.Count);

        Assert.Equal(new DateOnly(2026, 11, 5), result.Rows[0].PaymentDate);
        Assert.Equal(1500.00m, result.Rows[0].Amount);
        Assert.Equal("DK-2026-001", result.Rows[0].DocumentNumber);

        Assert.Equal(new DateOnly(2026, 11, 6), result.Rows[1].PaymentDate);
        Assert.Equal(2345.67m, result.Rows[1].Amount);
        Assert.Equal("DK-2026-003", result.Rows[1].DocumentNumber);
    }

    [Fact]
    public async Task ParseAndBulkCreate_DetectsDuplicateDocumentNumbers_AndSavesValidRows()
    {
        var (mediator, db) = Build();
        var ctx = (DbContext)db;

        // Payments tablosunda DocumentNumber sütunu henüz yoksa güvenli şekilde oluştur (idempotent)
        await ctx.Database.ExecuteSqlRawAsync("""
            ALTER TABLE "Payments" ADD COLUMN IF NOT EXISTS "DocumentNumber" character varying(100) NULL;
            CREATE INDEX IF NOT EXISTS "IX_Payments_DocumentNumber_Active"
                ON "Payments" ("DocumentNumber")
                WHERE "IsCancelled" = false AND "DocumentNumber" IS NOT NULL;
            """);

        var strategy = ctx.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await ctx.Database.BeginTransactionAsync();

            var today = DateOnly.FromDateTime(DateTime.Today);
            var existingDocNo = $"DEKONT-TEST-{Guid.NewGuid():N}"[..24];
            var newDocNo = $"DEKONT-NEW-{Guid.NewGuid():N}"[..24];

            var apt10 = await db.Apartments.FirstAsync(a => a.DoorNumber == "10");

            // Önce mevcut bir dekont numaralı tahsilat kaydet
            var firstBulk = await mediator.Send(new CreateBulkPaymentCommand(
                BankAccountId: null,
                ReceivedBy: "Test",
                Items:
                [
                    new CreateBulkPaymentItemDto(
                        apt10.Id,
                        today,
                        1200m,
                        "Önceden kaydedilmiş tahsilat",
                        existingDocNo,
                        LedgerDebtType.Aidat)
                ]));

            Assert.Equal(1, firstBulk.CreatedCount);

            // Şimdi içinde hem mevcut dekontun (mükerrer) hem yeni dekontun olduğu CSV ekstresi ayrıştır
            var csvContent = string.Join("\r\n",
                "Tarih;Açıklama;Tutar;Dekont No",
                $"{today:dd.MM.yyyy};DAİRE 10 AİDAT ÖDEMESİ;1.200,00;{existingDocNo}",
                $"{today:dd.MM.yyyy};15 NOLU DAİRE DOĞALGAZ ÖDEMESİ;2.450,50;{newDocNo}",
                $"{today:dd.MM.yyyy};15 NOLU DAİRE TEKRAR SATIR;2.450,50;{newDocNo}");

            using var csvStream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
            var parseRes = await mediator.Send(new ParseBankStatementCommand(csvStream, "teb_ekstre.csv"));

            Assert.Equal(3, parseRes.TotalRows);
            Assert.Equal(1, parseRes.ReadyCount);
            Assert.Equal(2, parseRes.DuplicateCount);

            // 1. satır DB'de var -> Duplicate
            Assert.Equal(BankStatementRowStatus.Duplicate, parseRes.Rows[0].Status);
            // 2. satır yeni -> Ready, Daire 15, Invoice (Doğalgaz)
            Assert.Equal(BankStatementRowStatus.Ready, parseRes.Rows[1].Status);
            Assert.Equal("15", parseRes.Rows[1].MatchedDoorNumber);
            Assert.Equal(LedgerDebtType.Invoice, parseRes.Rows[1].SuggestedDebtType);
            // 3. satır dosya içi mükerrer -> Duplicate
            Assert.Equal(BankStatementRowStatus.Duplicate, parseRes.Rows[2].Status);

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

