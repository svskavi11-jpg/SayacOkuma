using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Westalife.Application.Features.Reports;

namespace Westalife.Infrastructure.Reporting.Pdf;

/// <summary>
/// Genel Kurul ve Denetim Kurulu Resmi Dönem Raporu (PDF).
/// Seçilen yönetim dönemi (From - To) için Gelir-Gider, Banka Mutabakatı,
/// Dinamik Aylık Matris, Daire Alacak/Borç Cetveli ve Resmi İmza Bloğu içerir.
/// </summary>
public static class GeneralAssemblyPdfDocument
{
    private const string FontFamily = "Lato";
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    static GeneralAssemblyPdfDocument()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(GeneralAssemblyReport data) =>
        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontFamily(FontFamily).FontSize(8.5f));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text(data.BuildingName).Bold().FontSize(15).FontColor("#1F2937");
                            left.Item().Text("GENEL KURUL VE DENETİM KURULU RESMİ FAALİYET RAPORU")
                                .Bold().FontSize(11).FontColor("#0958D9");
                        });
                        row.ConstantItem(240).AlignRight().Column(right =>
                        {
                            right.Item().Text($"Yönetim Dönemi: {data.From:dd.MM.yyyy} – {data.To:dd.MM.yyyy}").Bold().FontSize(9.5f);
                            right.Item().Text($"Rapor Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(8).FontColor("#6B7280");
                            right.Item().Text(data.Reconciled ? "Mutabakat: KURUŞU KURUŞUNA MUTABIK ✔" : "Mutabakat: Kontrol Edilmeli")
                                .Bold().FontSize(8).FontColor(data.Reconciled ? "#237804" : "#CF1322");
                        });
                    });
                    col.Item().PaddingTop(4).LineHorizontal(1f).LineColor("#CBD5E1");
                });

                page.Content().PaddingTop(10).Column(content =>
                {
                    // 1. Özet KPI Kutuları
                    content.Item().Text("1. YÖNETİM DÖNEMİ FİNANSAL ÖZETİ").Bold().FontSize(10).FontColor("#1E293B");
                    content.Item().PaddingTop(4).Table(kpi =>
                    {
                        kpi.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                        });

                        KpiBox(kpi.Cell(), "TOPLAM TAHSİLAT (GELİR)", Money(data.TotalIncome), "#237804");
                        KpiBox(kpi.Cell(), "NAKİT / BANKA GİDERİ", Money(data.TotalCashExpense), "#CF1322");
                        KpiBox(kpi.Cell(), "GÖREV MAHSUBU", Money(data.TotalWaiverExpense), "#08979C");
                        KpiBox(kpi.Cell(), "TOPLAM GERÇEKLEŞEN GİDER", Money(data.TotalActualExpense), "#CF1322");
                        KpiBox(kpi.Cell(), "DÖNEM SONU BANKA MEVCUDU", Money(data.TotalBankClosingBalance), "#0958D9");
                        KpiBox(kpi.Cell(), "TOPLAM DAİRE ALACAĞI (BORÇLULAR)", Money(data.TotalReceivables), "#D46B08");
                    });

                    // 2. Gelir ve Gider Kalemleri + Banka Özeti Yan Yana
                    content.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem(3).Column(left =>
                        {
                            left.Item().Text("2. GİDER KALEMLERİ VE BÜTÇE GERÇEKLEŞMESİ").Bold().FontSize(10).FontColor("#1E293B");
                            left.Item().PaddingTop(4).Table(table =>
                            {
                                table.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(2.4f);
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                });

                                table.Header(h =>
                                {
                                    HeaderCell(h.Cell(), "Gider Kalemi");
                                    HeaderCell(h.Cell(), "Tahmini", true);
                                    HeaderCell(h.Cell(), "Harcama", true);
                                    HeaderCell(h.Cell(), "Görev Mahsubu", true);
                                    HeaderCell(h.Cell(), "Gerçekleşen", true);
                                    HeaderCell(h.Cell(), "Kalan", true);
                                });

                                foreach (var exp in data.ExpenseRows)
                                {
                                    BodyCell(table.Cell(), exp.Category);
                                    BodyCell(table.Cell(), Money(exp.Estimated), true);
                                    BodyCell(table.Cell(), Money(exp.ExpenseAmount), true);
                                    BodyCell(table.Cell(), Money(exp.WaiverAmount), true);
                                    BodyCell(table.Cell(), Money(exp.Actual), true);
                                    BodyCell(table.Cell(), Money(exp.Remaining), true);
                                }

                                HeaderCell(table.Cell(), "TOPLAM");
                                HeaderCell(table.Cell(), Money(data.TotalEstimatedExpense), true);
                                HeaderCell(table.Cell(), Money(data.TotalCashExpense), true);
                                HeaderCell(table.Cell(), Money(data.TotalWaiverExpense), true);
                                HeaderCell(table.Cell(), Money(data.TotalActualExpense), true);
                                HeaderCell(table.Cell(), Money(data.TotalRemainingBudget), true);
                            });
                        });

                        row.ConstantItem(14);

                        row.RelativeItem(2).Column(right =>
                        {
                            right.Item().Text("3. GELİR DAĞILIMI VE BANKA ÖZETİ").Bold().FontSize(10).FontColor("#1E293B");
                            right.Item().PaddingTop(4).Table(incTable =>
                            {
                                incTable.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(2);
                                    c.RelativeColumn();
                                });
                                incTable.Header(h =>
                                {
                                    HeaderCell(h.Cell(), "Gelir Türü");
                                    HeaderCell(h.Cell(), "Tutar", true);
                                });
                                foreach (var inc in data.IncomeRows)
                                {
                                    BodyCell(incTable.Cell(), inc.Title);
                                    BodyCell(incTable.Cell(), Money(inc.Amount), true);
                                }
                                HeaderCell(incTable.Cell(), "TOPLAM GELİR");
                                HeaderCell(incTable.Cell(), Money(data.TotalIncome), true);
                            });

                            right.Item().PaddingTop(8).Table(bankTable =>
                            {
                                bankTable.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(1.8f);
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                    c.RelativeColumn();
                                });
                                bankTable.Header(h =>
                                {
                                    HeaderCell(h.Cell(), "Banka Hesabı");
                                    HeaderCell(h.Cell(), "Açılış", true);
                                    HeaderCell(h.Cell(), "Giren", true);
                                    HeaderCell(h.Cell(), "Çıkan", true);
                                    HeaderCell(h.Cell(), "Kapanış", true);
                                });
                                foreach (var b in data.Banks)
                                {
                                    BodyCell(bankTable.Cell(), b.Label);
                                    BodyCell(bankTable.Cell(), Money(b.Opening), true);
                                    BodyCell(bankTable.Cell(), Money(b.MoneyIn), true);
                                    BodyCell(bankTable.Cell(), Money(b.MoneyOut), true);
                                    BodyCell(bankTable.Cell(), Money(b.Closing), true);
                                }
                                HeaderCell(bankTable.Cell(), "TOPLAM BANKA");
                                HeaderCell(bankTable.Cell(), Money(data.TotalBankOpeningBalance), true);
                                HeaderCell(bankTable.Cell(), Money(data.Banks.Sum(x => x.MoneyIn)), true);
                                HeaderCell(bankTable.Cell(), Money(data.Banks.Sum(x => x.MoneyOut)), true);
                                HeaderCell(bankTable.Cell(), Money(data.TotalBankClosingBalance), true);
                            });
                        });
                    });

                    // 4. Dinamik Aylık Nakit Akışı Matrisi (Seçilen yönetim döneminin aylarına göre)
                    if (data.MonthColumns.Count > 0 && data.MonthColumns.Count <= 18)
                    {
                        content.Item().PaddingTop(12).Text("4. DİNAMİK AYLIK GELİR - GİDER MATRİSİ").Bold().FontSize(10).FontColor("#1E293B");
                        content.Item().PaddingTop(4).Table(matrix =>
                        {
                            matrix.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(110);
                                foreach (var _ in data.MonthColumns)
                                {
                                    c.RelativeColumn();
                                }
                                c.ConstantColumn(75);
                            });

                            matrix.Header(h =>
                            {
                                HeaderCell(h.Cell(), "Kalem");
                                foreach (var col in data.MonthColumns)
                                {
                                    HeaderCell(h.Cell(), col.Label, true);
                                }
                                HeaderCell(h.Cell(), "TOPLAM", true);
                            });

                            foreach (var row in data.MonthlySummaryRows)
                            {
                                var isNet = row.RowTitle.Contains("Net");
                                if (isNet)
                                {
                                    HeaderCell(matrix.Cell(), row.RowTitle);
                                    foreach (var col in data.MonthColumns)
                                    {
                                        HeaderCell(matrix.Cell(), ShortMoney(row.MonthValues.GetValueOrDefault(col.Key)), true);
                                    }
                                    HeaderCell(matrix.Cell(), ShortMoney(row.Total), true);
                                }
                                else
                                {
                                    BodyCell(matrix.Cell(), row.RowTitle);
                                    foreach (var col in data.MonthColumns)
                                    {
                                        BodyCell(matrix.Cell(), ShortMoney(row.MonthValues.GetValueOrDefault(col.Key)), true);
                                    }
                                    BodyCell(matrix.Cell(), ShortMoney(row.Total), true);
                                }
                            }
                        });
                    }

                    // 5. Daire Bazlı Alacak / Borç ve Görev Mahsubu Cetveli
                    content.Item().PageBreak();
                    content.Item().Text("5. DAİRE BAZLI ALACAK / BORÇ VE GÖREV MAHSUBU DENETİM CETVELİ")
                        .Bold().FontSize(10).FontColor("#1E293B");
                    content.Item().PaddingTop(4).Table(aptTable =>
                    {
                        aptTable.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(34);
                            c.RelativeColumn(1.4f);
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn(1.1f);
                        });

                        aptTable.Header(h =>
                        {
                            HeaderCell(h.Cell(), "Kapı");
                            HeaderCell(h.Cell(), "Bağımsız Bölüm");
                            HeaderCell(h.Cell(), "Devir", true);
                            HeaderCell(h.Cell(), "Ek Aidat", true);
                            HeaderCell(h.Cell(), "Aidat Tah.", true);
                            HeaderCell(h.Cell(), "Yakıt Tah.", true);
                            HeaderCell(h.Cell(), "Aidat Öd.", true);
                            HeaderCell(h.Cell(), "Yakıt Öd.", true);
                            HeaderCell(h.Cell(), "Görev Mah.", true);
                            HeaderCell(h.Cell(), "Gecikme F.", true);
                            HeaderCell(h.Cell(), "Net Bakiye", true);
                        });

                        foreach (var r in data.ApartmentRows)
                        {
                            BodyCell(aptTable.Cell(), r.DoorNumber);
                            BodyCell(aptTable.Cell(), r.Title);
                            BodyCell(aptTable.Cell(), Money(r.Devir), true);
                            BodyCell(aptTable.Cell(), Money(r.ExtraAidat), true);
                            BodyCell(aptTable.Cell(), Money(r.AidatAccrual), true);
                            BodyCell(aptTable.Cell(), Money(r.FuelAccrual), true);
                            BodyCell(aptTable.Cell(), Money(r.AidatCollected), true);
                            BodyCell(aptTable.Cell(), Money(r.FuelCollected), true);
                            BodyCell(aptTable.Cell(), Money(r.Waiver), true);
                            BodyCell(aptTable.Cell(), Money(r.Interest), true);
                            BodyCell(aptTable.Cell(), Money(r.Net), true);
                        }

                        var t = data.ApartmentTotals;
                        HeaderCell(aptTable.Cell(), "");
                        HeaderCell(aptTable.Cell(), "GENEL TOPLAM");
                        HeaderCell(aptTable.Cell(), Money(t.Devir), true);
                        HeaderCell(aptTable.Cell(), Money(t.ExtraAidat), true);
                        HeaderCell(aptTable.Cell(), Money(t.AidatAccrual), true);
                        HeaderCell(aptTable.Cell(), Money(t.FuelAccrual), true);
                        HeaderCell(aptTable.Cell(), Money(t.AidatCollected), true);
                        HeaderCell(aptTable.Cell(), Money(t.FuelCollected), true);
                        HeaderCell(aptTable.Cell(), Money(t.Waiver), true);
                        HeaderCell(aptTable.Cell(), Money(t.Interest), true);
                        HeaderCell(aptTable.Cell(), Money(t.Net), true);
                    });

                    // 6. Resmi Genel Kurul / Denetim Kurulu İmza Bloğu
                    content.Item().PaddingTop(22).ShowEntire().Column(signCol =>
                    {
                        signCol.Item().Text("6. YÖNETİM VE DENETİM KURULU ONAY / İMZA BLOĞU")
                            .Bold().FontSize(10).FontColor("#1E293B");
                        signCol.Item().PaddingTop(4).Text(
                            $"İşbu rapor, {data.BuildingName} yönetiminin {data.From:dd.MM.yyyy} – {data.To:dd.MM.yyyy} dönemine ait gelir, gider, banka ve bağımsız bölüm cari hesap kayıtlarından elektronik ortamda üretilmiş olup denetim ve genel kurul onayına sunulmuştur.")
                            .FontSize(8.5f).FontColor("#4B5563");

                        signCol.Item().PaddingTop(14).Row(signRow =>
                        {
                            var managers = data.BoardMembers.Where(m => m.RoleTitle == "Yönetici").ToList();
                            var auditors = data.BoardMembers.Where(m => m.RoleTitle == "Denetçi").ToList();

                            signRow.RelativeItem().Border(0.6f).BorderColor("#CBD5E1").Padding(10).Column(box =>
                            {
                                box.Item().Text("SİTE YÖNETİCİLİĞİ").Bold().FontSize(9.5f);
                                if (managers.Count > 0)
                                {
                                    foreach (var m in managers)
                                    {
                                        box.Item().PaddingTop(3).Text($"{m.PersonName} (Daire {m.DoorNumber})").FontSize(8.5f);
                                    }
                                }
                                else
                                {
                                    box.Item().PaddingTop(3).Text("Yönetici Adı Soyadı: ........................................").FontSize(8.5f);
                                }
                                box.Item().PaddingTop(24).Text("İmza: ........................................................").FontSize(8.5f);
                            });

                            signRow.ConstantItem(20);

                            signRow.RelativeItem().Border(0.6f).BorderColor("#CBD5E1").Padding(10).Column(box =>
                            {
                                box.Item().Text("DENETİM KURULU").Bold().FontSize(9.5f);
                                if (auditors.Count > 0)
                                {
                                    foreach (var a in auditors)
                                    {
                                        box.Item().PaddingTop(3).Text($"{a.PersonName} (Daire {a.DoorNumber})").FontSize(8.5f);
                                    }
                                }
                                else
                                {
                                    box.Item().PaddingTop(3).Text("Denetçi Adı Soyadı: ........................................").FontSize(8.5f);
                                }
                                box.Item().PaddingTop(24).Text("İmza: ........................................................").FontSize(8.5f);
                            });

                            signRow.ConstantItem(20);

                            signRow.RelativeItem().Border(0.6f).BorderColor("#CBD5E1").Padding(10).Column(box =>
                            {
                                box.Item().Text("GENEL KURUL DİVAN BAŞKANLIĞI").Bold().FontSize(9.5f);
                                box.Item().PaddingTop(3).Text("Divan Başkanı: ........................................").FontSize(8.5f);
                                box.Item().PaddingTop(24).Text("İmza: ........................................................").FontSize(8.5f);
                            });
                        });
                    });
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text($"{data.BuildingName} — Genel Kurul ve Denetim Raporu ({data.From:dd.MM.yyyy} – {data.To:dd.MM.yyyy})")
                        .FontSize(7.5f).FontColor("#6B7280");
                    row.ConstantItem(100).AlignRight().Text(t =>
                    {
                        t.Span("Sayfa ").FontSize(7.5f);
                        t.CurrentPageNumber().FontSize(7.5f);
                        t.Span(" / ").FontSize(7.5f);
                        t.TotalPages().FontSize(7.5f);
                    });
                });
            });
        }).GeneratePdf();

    private static void KpiBox(IContainer cell, string label, string value, string color) =>
        cell.Padding(2).Border(0.6f).BorderColor("#E2E8F0").Background("#F8FAFC").Padding(6).Column(c =>
        {
            c.Item().Text(label).FontSize(7).Bold().FontColor("#64748B");
            c.Item().PaddingTop(2).Text(value).FontSize(10.5f).Bold().FontColor(color);
        });

    private static void HeaderCell(IContainer cell, string text, bool alignRight = false)
    {
        var box = cell.Background("#F1F5F9").Border(0.5f).BorderColor("#CBD5E1").Padding(3.5f);
        if (alignRight)
        {
            box.AlignRight().Text(text).Bold().FontSize(7.8f);
        }
        else
        {
            box.Text(text).Bold().FontSize(7.8f);
        }
    }

    private static void BodyCell(IContainer cell, string text, bool alignRight = false)
    {
        var box = cell.BorderBottom(0.4f).BorderColor("#E2E8F0").Padding(3f);
        if (alignRight)
        {
            box.AlignRight().Text(text).FontSize(7.8f);
        }
        else
        {
            box.Text(text).FontSize(7.8f);
        }
    }

    private static string Money(decimal value) => value.ToString("N2", Tr) + " TL";

    private static string ShortMoney(decimal value) => value.ToString("N2", Tr);
}

