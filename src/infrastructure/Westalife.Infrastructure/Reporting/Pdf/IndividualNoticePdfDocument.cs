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
/// Sol/sağ üst logo ve başlık metinleri Ayarlar'dan (veritabanı) gelir; logo yoksa metin basılır.
/// Üretim öncesi Kural #1 invariant'ı zorunludur: Σ satır tutarı != fatura tutarı ise PDF ÜRETİLMEZ.
/// </summary>
public static class IndividualNoticePdfDocument
{
    // Capraz-platform font: QuestPDF gomulu "Lato" arsivi her ortamda mevcut
    // (Windows gelistirme + Linux konteyner) ve Turkce karakterleri destekler.
    private const string FontFamily = "Lato";
    private const string WatermarkText = "BİREYSEL GİDER BİLDİRİMİ";

    // Referans form renkleri: koyu gri bölüm başlığı, açık gri içerik kutusu, sağ kenar şeridi.
    private const string SectionHeaderColor = "#9E9E9E";
    private const string SectionBodyColor = "#ECECEC";
    private const string SideStripColor = "#C4C4C4";
    private const float SideStripWidth = 30f;

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
        CreateDocument(data, options, singleApartmentId: null).GeneratePdf();

    public static byte[] GenerateSingle(NoticeReportData data, ReportingOptions options, Guid apartmentId) =>
        CreateDocument(data, options, apartmentId).GeneratePdf();

    /// <summary>PDF belgesini oluşturur (önizleme/test için görüntüye de çevrilebilir).</summary>
    public static Document CreateDocument(NoticeReportData data, ReportingOptions options, Guid? singleApartmentId)
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
                    page.MarginTop(28);
                    page.MarginBottom(28);
                    page.MarginLeft(28);
                    // Sağ kenarda gri şerit için ek boşluk bırakılır.
                    page.MarginRight(28 + SideStripWidth);
                    page.DefaultTextStyle(x => x.FontFamily(FontFamily).FontSize(9));

                    // Sağ kenar: tüm sayfa boyunca gri şerit + dikey filigran metin.
                    page.Background().AlignRight().Width(SideStripWidth).Background(SideStripColor)
                        .AlignMiddle().AlignCenter().RotateLeft()
                        .Text(WatermarkText).Bold().FontSize(22).FontColor("#E6E6E6");

                    page.Header().Element(content => ComposeHeader(content, data, options));
                    page.Content().Element(content => ComposeBody(content, data, row));
                    page.Footer().AlignRight().Text(t =>
                    {
                        t.Span($"{row.DoorNumber}. daire sayfası - ");
                        t.Span(DateTime.Now.ToString("dd.MM.yyyy HH:mm", CultureInfo.GetCultureInfo("tr-TR")));
                    });
                });
            }
        });
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
            // Sol üst: sol logo (Ayarlar) → yoksa sol başlık metni → yoksa Şirket Adı → yoksa appsettings metni.
            var leftText = FirstNonEmpty(data.LeftTitle, data.CompanyName, options.LogoLeftFallbackText);
            var rightText = FirstNonEmpty(data.RightTitle, options.LogoRightFallbackText);

            row.RelativeItem(3).Element(c => AddLogoOrText(c, data.LeftLogo, leftText, alignRight: false, data.LeftLogoHeightMm));
            row.RelativeItem(4).Column(col =>
            {
                col.Item().AlignCenter().Text("BİREYSEL GİDER BİLDİRİMİ").Bold().FontSize(13);
                col.Item().AlignCenter().Text(data.PeriodName).Bold().FontSize(11);
            });
            row.RelativeItem(3).Element(c => AddLogoOrText(c, data.RightLogo, rightText, alignRight: true, data.RightLogoHeightMm));
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
        // Toplam ödenecek tutar: dairenin payı (Sabit + Tüketim) + varsa okuma hizmet bedeli.
        var payable = row.TotalAmount + data.ReadingServiceFeeAmount;

        content.Row(r =>
        {
            r.RelativeItem(5).Column(c =>
            {
                c.Item().Text($"DAİRE {row.DoorNumber}").Bold().FontSize(10);
                c.Item().PaddingTop(10).Text(data.BuildingName.ToUpper(CultureInfo.GetCultureInfo("tr-TR"))).Bold();
                c.Item().Text(data.BuildingAddress);
            });
            r.RelativeItem(5).Column(c =>
            {
                c.Item().Element(x => InfoLine(x, "Abone No", data.SubscriberNumber));
                c.Item().Element(x => InfoLine(x, "Daire", row.DoorNumber));
                c.Item().Element(x => InfoLine(x, "Dönem", $"{data.PeriodFrom:dd.MM.yyyy} - {data.PeriodTo:dd.MM.yyyy}"));
                c.Item().Element(x => InfoLine(x, "Ödenen Süre", $"{data.DaysInPeriod} Gün"));
                c.Item().Element(x => InfoLine(x, "Oluşturma Tarihi",
                    data.IssueDate.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("tr-TR"))));
                c.Item().Element(x => InfoLine(x, "Son Ödeme Tarihi",
                    data.DueDate.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("tr-TR")), bold: true));
                c.Item().Element(x => InfoLine(x, "Toplam Ödenecek Tutar", $"{Fmt(payable)} TL", bold: true, large: true));
            });
        });
    }

    /// <summary>Sağ künye satırı: etiket sağa yaslı, değer solda (referans formdaki gibi).</summary>
    private static void InfoLine(IContainer container, string label, string value, bool bold = false, bool large = false)
    {
        container.Row(r =>
        {
            r.RelativeItem(5).AlignRight().PaddingRight(6).Text(label).FontSize(large ? 10 : 9);
            r.RelativeItem(4).Text(t =>
            {
                var span = t.Span(value).FontSize(large ? 11 : 9);
                if (bold)
                {
                    span.Bold();
                }
            });
        });
    }

    private static void ComposePersonalDistribution(IContainer content, NoticeReportData data, NoticeRow row)
    {
        content.Column(col =>
        {
            col.Item().SectionHeader("Bireysel Gider Tüketiminin Paylaştırılması");
            col.Item().Background(SectionBodyColor).Padding(6).Table(table =>
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
                    h.Cell().Text("Gider Payları").Bold();
                    h.Cell().Text("Birim Bedeli").Bold();
                    h.Cell().Text("Birim/miktar").Bold();
                    h.Cell().Text(string.Empty);
                });

                table.Cell().Text("%30 Sabit");
                table.Cell().Text($"{Fmt(data.UnitPriceFixedPerM2, "N6")} TL/m2 alan");
                table.Cell().Text($"{Fmt(row.SquareMeters, "N3")} m2 alan");
                table.Cell().AlignRight().Text($"{Fmt(row.FixedAmount)} TL");

                table.Cell().Text("%70 Tüketim");
                table.Cell().Text($"{Fmt(data.UnitPriceConsumptionPerKwh, "N6")} TL/kWh");
                table.Cell().Text($"{Fmt(row.ConsumptionKWh, "N3")} kWh");
                table.Cell().AlignRight().Text($"{Fmt(row.ConsumptionAmount)} TL");

                table.Cell().Text("Okuma Hizmet Bedeli");
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
            col.Item().Background(SectionBodyColor).Padding(6).Table(table =>
            {
                table.ColumnsDefinition(c =>
                {
                    c.RelativeColumn(4);
                    c.RelativeColumn(2);
                    c.RelativeColumn(2.5f);
                    c.RelativeColumn(3.5f);
                    c.RelativeColumn(3.5f);
                });

                table.Header(h =>
                {
                    h.Cell().Text("Isınma Ve Soğutma Tüketiminin Paylaştırılması").Bold();
                    h.Cell().AlignRight().Text("% Pay").Bold();
                    h.Cell().AlignRight().Text("Tutar").Bold();
                    h.Cell().AlignRight().Text("Toplam Birim").Bold();
                    h.Cell().AlignRight().Text("Birim Bedeli").Bold();
                });

                table.Cell().Text("Bina Doğalgaz Faturası");
                table.Cell().AlignRight().Text("100,00 %");
                table.Cell().AlignRight().Text($"{Fmt(data.TotalInvoiceAmount)} TL");
                table.Cell().Text(string.Empty);
                table.Cell().Text(string.Empty);

                table.Cell().Text("Toplam Enerji Tüketim Bedeli / 30% Sabit");
                table.Cell().AlignRight().Text("30,00 %");
                table.Cell().AlignRight().Text($"{Fmt(data.Rows.Sum(r => r.FixedAmount))} TL");
                table.Cell().AlignRight().Text($"{Fmt(data.TotalSquareMeters, "N3")} m2 alan");
                table.Cell().AlignRight().Text($"{Fmt(data.UnitPriceFixedPerM2, "N6")} TL/m2");

                table.Cell().Text("Toplam Enerji Tüketim Bedeli / 70% Tüketim");
                table.Cell().AlignRight().Text("70,00 %");
                table.Cell().AlignRight().Text($"{Fmt(data.Rows.Sum(r => r.ConsumptionAmount))} TL");
                table.Cell().AlignRight().Text($"{Fmt(data.TotalConsumptionKWh, "N3")} kWh");
                table.Cell().AlignRight().Text($"{Fmt(data.UnitPriceConsumptionPerKwh, "N6")} TL/kWh");
            });
        });
    }

    private static void ComposeReadingValues(IContainer content, NoticeRow row)
    {
        content.Column(col =>
        {
            col.Item().SectionHeader("Okuma Değerleri");
            col.Item().Background(SectionBodyColor).Padding(6).Column(inner =>
            {
                inner.Item().Table(table =>
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
                        h.Cell().Text("İş Sayacı / Sayaç Seri No").Bold();
                        h.Cell().Text("Okuma Tarihi").Bold();
                        h.Cell().AlignRight().Text("İlk Okuma").Bold();
                        h.Cell().AlignRight().Text("Son Okuma").Bold();
                        h.Cell().AlignRight().Text("Tüketim").Bold();
                    });

                    table.Cell().Text(string.IsNullOrWhiteSpace(row.MeterSerialNumber) ? "-" : row.MeterSerialNumber);
                    table.Cell().Text(row.LastReadingDateUtc?.ToString("dd.MM.yyyy", CultureInfo.GetCultureInfo("tr-TR")) ?? "-");
                    table.Cell().AlignRight().Text(row.FirstIndexKWh is null ? "-" : $"{Fmt(row.FirstIndexKWh.Value, "N3")} kWh");
                    table.Cell().AlignRight().Text(row.LastIndexKWh is null ? "-" : $"{Fmt(row.LastIndexKWh.Value, "N3")} kWh");
                    table.Cell().AlignRight().Text($"{Fmt(row.ConsumptionKWh, "N3")} kWh");
                });

                // Referans formda "Toplam Gider" satırı dönem tüketimini (kWh) gösterir.
                inner.Item().PaddingTop(4).AlignRight().Text(t =>
                {
                    t.Span("Toplam Gider: ").Bold();
                    t.Span($"{Fmt(row.ConsumptionKWh, "N3")} kWh").Bold();
                });
            });
        });
    }

    private static void ComposeNotes(IContainer content, NoticeReportData data)
    {
        content.Column(col =>
        {
            col.Item().SectionHeader("Ek Açıklamalar");
            col.Item().Background(SectionBodyColor).MinHeight(90).Padding(6)
                .Text(t =>
                {
                    // Öncelik faturaya girilen "Duyuru" (GasInvoice.Note); boşsa Ayarlar'daki varsayılan açıklama
                    // (handler tarafından InvoiceNote'a zaten çözümlenmiş olarak gelir).
                    if (!string.IsNullOrWhiteSpace(data.InvoiceNote))
                    {
                        t.Span(data.InvoiceNote);
                    }
                });
        });
    }

    /// <summary>Logo (DB'den byte[]) varsa görsel basılır; yoksa metin başlık.</summary>
    private static void AddLogoOrText(
        IContainer container, byte[]? logo, string fallbackText, bool alignRight, int heightMm)
    {
        if (logo is { Length: > 0 })
        {
            // Yükseklik Ayarlar'dan (mm); genişlik görsel oranına göre otomatik, sütun genişliğini aşmaz.
            var aligned = alignRight ? container.AlignRight() : container.AlignLeft();
            aligned.MaxHeight(heightMm, Unit.Millimetre).Image(logo).FitArea();
            return;
        }

        if (!string.IsNullOrWhiteSpace(fallbackText))
        {
            var aligned = alignRight ? container.AlignRight() : container.AlignLeft();
            aligned.Text(fallbackText).Bold().FontSize(11);
        }
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;

    private static string Fmt(decimal value, string format = "N2") =>
        value.ToString(format, CultureInfo.GetCultureInfo("tr-TR"));
}
