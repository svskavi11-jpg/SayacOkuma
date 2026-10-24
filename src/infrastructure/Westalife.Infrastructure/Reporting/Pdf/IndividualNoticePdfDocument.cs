using System.Globalization;
using Microsoft.Extensions.Options;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Westalife.Application.Common.Models;
using Westalife.Application.Features.GasInvoices.Queries.GetNoticeReport;
using Westalife.Domain.Constants;
using Westalife.Domain.Exceptions;

namespace Westalife.Infrastructure.Reporting.Pdf;

/// <summary>
/// Danfoss "Bireysel Gider Bildirimi" formu — docs/DANFOSS_NOTICE_TEMPLATE.md spesifikasyonuna birebir.
/// Logo fallback: yapılandırılan relative path'te dosya yoksa metin başlık basılır (Kural #2: mutlak yol yok).
/// Üretim öncesi Kural #1 invariant'ı zorunludur: Σ satır tutarı != fatura tutarı ise PDF ÜRETİLMEZ.
/// </summary>
public static class IndividualNoticePdfDocument
{
    // Capraz-platform font: QuestPDF gomulu "Lato" arsivi her ortamda mevcut
    // (Windows gelistirme + Linux konteyner) ve Turkce karakterleri destekler.
    private const string FontFamily = "Lato";
    private const string WatermarkText = "BİREYSEL GİDER BİLDİRİMİ";

    static IndividualNoticePdfDocument()
    {
        QuestPDF.Settings.License = LicenseType.Community; // Onaylı: yıllık gelir < 1M$

        // QuestPDF 2026.9+: sistem fontları varsayılan devre dışı — Türkçe karakterler için
        // Windows font dizini DİNAMİK çözülür (Kural #2: mutlak yol yok), mevcutsa kaydedilir.
        try
        {
            var fontsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            foreach (var fontFile in new[] { "segoeui.ttf", "segoeuib.ttf" })
            {
                var fontPath = Path.Combine(fontsDirectory, fontFile);
                if (File.Exists(fontPath))
                {
                    using var fontStream = new FileStream(fontPath, FileMode.Open, FileAccess.Read);
                    FontManager.RegisterFont(fontStream);
                }
            }
        }
        catch (Exception)
        {
            // Font kaydı başarısız olursa Latin temel karakter setiyle üretime devam edilir.
        }
    }

    public static byte[] GenerateAll(NoticeReportData data, ReportingOptions options) =>
        Generate(data, options, singleApartmentId: null);

    public static byte[] GenerateSingle(NoticeReportData data, ReportingOptions options, Guid apartmentId) =>
        Generate(data, options, apartmentId);

    private static byte[] Generate(NoticeReportData data, ReportingOptions options, Guid? singleApartmentId)
    {
        ValidateInvariants(data);

        var rows = singleApartmentId is null
            ? data.Rows
            : data.Rows.Where(r => r.ApartmentId == singleApartmentId.Value).ToList();

        if (rows.Count == 0)
        {
            throw new DomainException("Belirtilen daire için hesaplama satırı bulunamadı.");
        }

        return Document.Create(container =>
        {
            foreach (var row in rows)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(28);
                    page.DefaultTextStyle(x => x.FontFamily(FontFamily).FontSize(9));

                    page.Header().Element(content => ComposeHeader(content, data, options));
                    page.Content().Element(content => ComposeBody(content, data, row));
                    page.Footer().AlignRight().Text(t =>
                    {
                        t.Span($"{row.DoorNumber}. daire sayfası - ");
                        t.Span(DateTime.Now.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("tr-TR")));
                    });
                });
            }
        }).GeneratePdf();
    }

    private static void ValidateInvariants(NoticeReportData data)
    {
        var allocated = data.Rows.Sum(r => r.TotalAmount);
        if (decimal.Round(allocated, 2) != decimal.Round(data.TotalInvoiceAmount, 2))
        {
            throw new DomainException(
                $"{DomainConstants.ApartmentInvoice.TotalMustEqualParts} (Dağıtılan {allocated}, Fatura {data.TotalInvoiceAmount})");
        }
    }

    private static void ComposeHeader(IContainer content, NoticeReportData data, ReportingOptions options)
    {
        content.Row(row =>
        {
            // Sol üst: Ayarlar'daki Şirket Adı (boşsa appsettings fallback metni), varsa logo dosyası.
            var leftFallback = string.IsNullOrWhiteSpace(data.CompanyName)
                ? options.LogoLeftFallbackText
                : data.CompanyName;
            row.RelativeColumn(2).Element(c => AddLogoOrText(c, options.LogoLeftPath, leftFallback));
            row.RelativeColumn(5).Column(col =>
            {
                col.Item().AlignCenter().Text("BİREYSEL GİDER BİLDİRİMİ").Bold().FontSize(13);
                col.Item().AlignCenter().Text(data.PeriodName).Bold().FontSize(11);
            });
            row.RelativeColumn(2).Element(c => AddLogoOrText(c, options.LogoRightPath, options.LogoRightFallbackText));
        });
    }

    private static void ComposeBody(IContainer content, NoticeReportData data, NoticeRow row)
    {
        content.PaddingTop(10).Column(col =>
        {
            ComposeInfoBlocks(col.Item(), data, row);
            col.Item().PaddingTop(12).Element(c => ComposePersonalDistribution(c, data, row));
            col.Item().PaddingTop(12).Element(c => ComposeBuildingDistribution(c, data));
            col.Item().PaddingTop(12).Element(c => ComposeReadingValues(c, row));
            col.Item().PaddingTop(12).Element(c => ComposeNotes(c, data));
        });
    }

    private static void ComposeInfoBlocks(IContainer content, NoticeReportData data, NoticeRow row)
    {
        content.Row(r =>
        {
            r.RelativeColumn(5).Column(c =>
            {
                c.Item().Text($"DAİRE {row.DoorNumber}").Bold();
                c.Item().Text(data.BuildingName).Bold();
                c.Item().Text(data.BuildingAddress);
            });
            r.RelativeColumn(5).Column(c =>
            {
                c.Item().Text(t => { t.Span("Abone No: ").Bold(); t.Span(data.SubscriberNumber); });
                c.Item().Text(t => { t.Span("Daire: ").Bold(); t.Span(row.DoorNumber); });
                c.Item().Text(t =>
                {
                    t.Span("Dönem: ").Bold();
                    t.Span($"{data.PeriodFrom:dd.MM.yyyy} - {data.PeriodTo:dd.MM.yyyy}");
                });
                c.Item().Text(t => { t.Span("Ödenen Süre: ").Bold(); t.Span($"{data.DaysInPeriod} Gün"); });
                c.Item().Text(t =>
                {
                    t.Span("Oluşturma Tarihi: ").Bold();
                    t.Span(data.IssueDate.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("tr-TR")));
                });
                c.Item().Text(t =>
                {
                    t.Span("Son Ödeme Tarihi: ").Bold();
                    t.Span(data.DueDate.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("tr-TR")));
                });
                c.Item().AlignRight().Text(t =>
                {
                    t.Span("Toplam Ödenecek Tutar: ").Bold();
                    t.Span(Fmt(data.TotalInvoiceAmount)).Bold().FontSize(11);
                });
            });
        });
    }

    private static void ComposePersonalDistribution(IContainer content, NoticeReportData data, NoticeRow row)
    {
        content.Column(col =>
        {
            col.Item().SectionHeader("Bireysel Gider Tüketiminin Paylaştırılması");
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(4);
                    c.RelativeColumn(3);
                    c.RelativeColumn(3);
                    c.RelativeColumn(2);
                });

                table.Header(h =>
                {
                    h.Cell().HeadCell().Text("Gider Payları");
                    h.Cell().HeadCell().Text("Birim Bedeli");
                    h.Cell().HeadCell().Text("Birim/miktar");
                    h.Cell().HeadCell().AlignRight().Text("Tutar");
                });

                table.Cell().Text("%30 Sabit");
                table.Cell().Text($"{Fmt(data.UnitPriceFixedPerM2, "N6")} TL/m2 alan");
                table.Cell().Text($"{Fmt(row.SquareMeters, "N3")} m2 alan");
                table.Cell().AlignRight().Text($"{Fmt(row.FixedAmount)} TL");

                table.Cell().Text("%70 Tüketim");
                table.Cell().Text($"{Fmt(data.UnitPriceConsumptionPerKwh, "N6")} TL/kWh");
                table.Cell().Text($"{Fmt(row.ConsumptionKWh, "N3")} kWh");
                table.Cell().AlignRight().Text($"{Fmt(row.ConsumptionAmount)} TL");

                table.Cell().Text("Okuma Hizmet Bededi");
                table.Cell().Text(string.Empty);
                table.Cell().Text(string.Empty);
                table.Cell().AlignRight().Text($"{Fmt(data.ReadingServiceFeeAmount)} TL");
            });
        });
    }

    private static void ComposeBuildingDistribution(IContainer content, NoticeReportData data)
    {
        content.Column(col =>
        {
            col.Item().SectionHeader("Isınma Ve Soğutma Tüketiminin Paylaştırılması");
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(4);
                    c.RelativeColumn(2);
                    c.RelativeColumn(2);
                    c.RelativeColumn(4);
                });

                table.Header(h =>
                {
                    h.Cell().HeadCell().Text("Isınma Ve Soğutma Tüketiminin Paylaştırılması");
                    h.Cell().HeadCell().AlignRight().Text("% Pay");
                    h.Cell().HeadCell().AlignRight().Text("Tutar");
                    h.Cell().HeadCell().AlignRight().Text("Toplam Birim");
                });

                table.Cell().Text("Bina Doğalgaz Faturası");
                table.Cell().AlignRight().Text("100,00 %");
                table.Cell().AlignRight().Text($"{Fmt(data.TotalInvoiceAmount)} TL");
                table.Cell().Text(string.Empty);

                table.Cell().Text("Toplam Enerji Tüketim Bedeli / 30% Sabit");
                table.Cell().AlignRight().Text("30,00 %");
                table.Cell().AlignRight().Text($"{Fmt(data.Rows.Sum(r => r.FixedAmount))} TL");
                table.Cell().AlignRight().Text($"{Fmt(data.TotalSquareMeters, "N3")} m2 alan - {Fmt(data.UnitPriceFixedPerM2, "N6")} TL/m2");

                table.Cell().Text("Toplam Enerji Tüketim Bedeli / 70% Tüketim");
                table.Cell().AlignRight().Text("70,00 %");
                table.Cell().AlignRight().Text($"{Fmt(data.Rows.Sum(r => r.ConsumptionAmount))} TL");
                table.Cell().AlignRight().Text($"{Fmt(data.TotalConsumptionKWh, "N3")} kWh - {Fmt(data.UnitPriceConsumptionPerKwh, "N6")} TL/kWh");
            });
        });
    }

    private static void ComposeReadingValues(IContainer content, NoticeRow row)
    {
        content.Column(col =>
        {
            col.Item().SectionHeader("Okuma Değerleri");
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(3);
                    c.RelativeColumn(2);
                    c.RelativeColumn(2);
                    c.RelativeColumn(2);
                    c.RelativeColumn(2);
                });

                table.Header(h =>
                {
                    h.Cell().HeadCell().Text("İş Sayacı / Sayaç Seri No");
                    h.Cell().HeadCell().Text("Okuma Tarihi");
                    h.Cell().HeadCell().AlignRight().Text("İlk Okuma");
                    h.Cell().HeadCell().AlignRight().Text("Son Okuma");
                    h.Cell().HeadCell().AlignRight().Text("Tüketim");
                });

                table.Cell().Text(string.IsNullOrWhiteSpace(row.MeterSerialNumber) ? "-" : row.MeterSerialNumber);
                table.Cell().Text(row.LastReadingDateUtc?.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("tr-TR")) ?? "-");
                table.Cell().AlignRight().Text(row.FirstIndexKWh is null ? "-" : $"{Fmt(row.FirstIndexKWh.Value, "N3")} kWh");
                table.Cell().AlignRight().Text(row.LastIndexKWh is null ? "-" : $"{Fmt(row.LastIndexKWh.Value, "N3")} kWh");
                table.Cell().AlignRight().Text($"{Fmt(row.ConsumptionKWh, "N3")} kWh");
            });

            col.Item().AlignRight().Text(t =>
            {
                t.Span("Toplam Gider: ").Bold();
                t.Span($"{Fmt(row.TotalAmount)} TL").Bold();
            });
        });
    }

    private static void ComposeNotes(IContainer content, NoticeReportData data)
    {
        content.Column(col =>
        {
            col.Item().SectionHeader("Ek Açıklamalar");
            col.Item().Border(1).BorderColor("#CCCCCC").MinHeight(90).Padding(6)
                .Text(t =>
                {
                    // Etap 6.7: öncelik faturaya girilen "Duyuru" (GasInvoice.Note);
                    // boşsa appsettings'teki global açıklama metni kullanılır.
                    if (!string.IsNullOrWhiteSpace(data.InvoiceNote))
                    {
                        t.Span(data.InvoiceNote);
                    }
                });
        });
    }

    private static void AddLogoOrText(IContainer container, string configuredPath, string fallbackText)
    {
        var resolved = ResolvePath(configuredPath);
        if (!string.IsNullOrWhiteSpace(resolved) && File.Exists(resolved))
        {
            container.MaxHeight(48).MaxWidth(120).Image(resolved, ImageScaling.FitArea);
            return;
        }

        if (!string.IsNullOrWhiteSpace(fallbackText))
        {
            container.AlignLeft().Text(fallbackText).Bold().FontSize(11);
        }
    }

    /// <summary>Kural #2: relative yollar çalışma dizinine göre çözülür, mutlak yol yasak.</summary>
    private static string? ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Path.IsPathRooted(path) ? path : Path.Combine(Directory.GetCurrentDirectory(), path);
    }

    private static string Fmt(decimal value, string format = "N2") =>
        value.ToString(format, CultureInfo.GetCultureInfo("tr-TR"));
}
