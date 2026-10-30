using System.Diagnostics;
using Westalife.Application.Common.Models;
using Westalife.Application.Features.GasInvoices.Queries.GetNoticeReport;
using Westalife.Infrastructure.Reporting.Pdf;

// ============================================================================
// BİREYSEL GİDER BİLDİRİMİ — PDF ÖNİZLEME ARACI
// ----------------------------------------------------------------------------
// Amaç: PDF tasarımını (IndividualNoticePdfDocument.cs) kodda değiştirdikten
// sonra uygulama/Docker'a gerek kalmadan ANINDA görmek.
//
// Kullanım:
//   dotnet run --project tools/PdfPreview                 → 1. dairenin PDF'i + otomatik açılır
//   dotnet run --project tools/PdfPreview -- --tum        → tüm daireler tek PDF'te
//   dotnet run --project tools/PdfPreview -- --cikti C:\temp\deneme.pdf
//
// NOT: PDF'i besleyen örnek veri aşağıdaki BuildSampleData metodundadır.
// Tasarımı test etmek için buradaki tutarları değiştirmeniz yeterlidir.
// Kural #1 uyarınca Σ satır tutarı != ToplamInvoiceAmount ise PDF ÜRETİLMEZ;
// örnek veri bu invariant'ı sağlayacak şekilde hesaplanır.
// ============================================================================

var open = !args.Contains("--no-open");
var all = args.Contains("--tum");
var outArgIdx = Array.IndexOf(args, "--cikti");
var outputPath = outArgIdx >= 0 && outArgIdx + 1 < args.Length
    ? args[outArgIdx + 1]
    : Path.Combine(Directory.GetCurrentDirectory(), "Bildirim-Onizleme.pdf");

var (data, options) = SampleDataBuilder.Build();

var pdf = all
    ? IndividualNoticePdfDocument.GenerateAll(data, options)
    : IndividualNoticePdfDocument.GenerateSingle(data, options, data.Rows[0].ApartmentId);

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
await File.WriteAllBytesAsync(outputPath, pdf);

Console.WriteLine($"✔ PDF üretildi : {Path.GetFullPath(outputPath)}");
Console.WriteLine($"  İçerik       : {(all ? $"{data.Rows.Count} daire (tümü)" : data.Rows[0].ApartmentTitle)}");
Console.WriteLine($"  Boyut        : {pdf.Length:N0} bayt");

if (open)
{
    Process.Start(new ProcessStartInfo(Path.GetFullPath(outputPath)) { UseShellExecute = true });
    Console.WriteLine("  → Varsayılan görüntüleyicide açıldı.");
}

public static class SampleDataBuilder
{
    /// <summary>Örnek veri: 3 daire, 1 fatura dönemi, 1. dairede devir + gecikmiş borç.</summary>
    public static (NoticeReportData Data, ReportingOptions Options) Build()
    {
        const decimal unitFixedPerM2 = 12.500000m;   // sabit bedel: TL/m²
        const decimal unitConsumptionPerKwh = 8.333333m; // tüketim bedeli: TL/kWh

        (string Door, string Title, string Serial, decimal M2, decimal KWh)[] apartments =
        {
            ("1", "Daire 1", "IZ-2400311", 47.40m, 850m),
            ("2", "Daire 2", "IZ-2400312", 63.00m, 1120m),
            ("3", "Daire 3", "IZ-2400313", 52.00m, 690m),
        };

        var periodFrom = new DateTimeOffset(2026, 4, 9, 0, 0, 0, TimeSpan.Zero);
        var periodTo = new DateTimeOffset(2026, 5, 8, 0, 0, 0, TimeSpan.Zero);
        var issueDate = new DateTimeOffset(2026, 5, 10, 0, 0, 0, TimeSpan.Zero);
        var dueDate = issueDate.AddDays(15);

        var rows = new List<NoticeRow>();
        foreach (var a in apartments)
        {
            var fixedAmount = decimal.Round(a.M2 * unitFixedPerM2, 2);
            var consumptionAmount = decimal.Round(a.KWh * unitConsumptionPerKwh, 2);
            rows.Add(new NoticeRow(
                Guid.NewGuid(), a.Door, a.Title, a.Serial, a.M2, a.KWh,
                FirstIndexKWh: 12_000m, LastIndexKWh: 12_000m + a.KWh,
                LastReadingDateUtc: periodTo,
                fixedAmount, consumptionAmount,
                TotalAmount: fixedAmount + consumptionAmount,
                IsMeterReadFailed: false));
        }

        var totalInvoice = rows.Sum(r => r.TotalAmount);
        var totalM2 = apartments.Sum(a => a.M2);
        var totalKwh = apartments.Sum(a => a.KWh);

        // Daire 1'e cari hesap özeti: devir anapara + devir faiz + gecikmiş fatura.
        var ledger1 = new NoticeLedgerSummary(
            OpeningBalancePrincipalRemaining: 1_200.00m,
            OpeningBalanceInterestRemaining: 200.00m,
            InterestAccrued: 188.71m,
            TotalBalance: 1_200.00m + 200.00m + 188.71m,
            AnnualInterestRate: 24m,
            Debts: new List<NoticeDebtRow>
            {
                new("Devir (01.01.2026)", new DateOnly(2026, 1, 1), 1_200.00m, 129, 24m, 101.91m),
                new("Devir Faiz (01.01.2026)", new DateOnly(2026, 1, 1), 200.00m, 129, 24m, 0m),
                new("Nis 2026 – May 2026", new DateOnly(2026, 4, 25), 867.75m, 15, 24m, 86.80m),
            });

        var data = new NoticeReportData(
            CompanyName: "WESTALİFE REZİDANS YÖNETİMİ",
            BuildingName: "Westalife Residence",
            BuildingAddress: "Örnek Mah. Deneme Cad. No: 123 / İstanbul",
            PeriodName: "Nisan 2026 Dönemi",
            periodFrom, periodTo,
            DaysInPeriod: 30,
            SubscriberNumber: "4812345678",
            MasterMeterSerial: "ANA-2026-001",
            issueDate, dueDate,
            TotalInvoiceAmount: totalInvoice,
            TotalSquareMeters: totalM2,
            TotalConsumptionKWh: totalKwh,
            unitFixedPerM2, unitConsumptionPerKwh,
            ReadingServiceFeeAmount: 0m,
            InvoiceNote: "ÖRNEK DUYURU — Bu bir önizleme belgesidir. Ödemelerinizi zamanında yapmanızı rica ederiz.",
            Rows: rows,
            LeftTitle: "WESTALIFE",
            RightTitle: "Yönetim Hizmetleri",
            LeftLogo: null, RightLogo: null);

        var options = new ReportingOptions
        {
            DueDateDays = 15,
            ReadingServiceFeeAmount = 0m,
        };

        return (data, options);
    }
}
