# Westalife Residence — Faz Takip Dokümanı

| Faz | Kapsam | Durum |
|---|---|---|
| Faz 1 | Solution, Domain (SCD Type 2), PostgreSQL (UTF-8/tr-TR ICU), seed (72 daire), ilk migration'lar | ✔ TAMAMLANDI (23... dahil tüm DB kısıt testleri geçti) |
| Faz 2 | M-Bus Infrastructure: custom EN 13757 parser, TCP/USB kanalları, SimulatedMeterReader, MbusMeterReader + Retry | ✔ TAMAMLANDI (23/23 unit test yeşil) — 20.10.2026 onaylandı |
| Faz 3 | Application: CQRS (MediatR), 30/70 hesaplama motoru, Largest Remainder Method | ✔ TAMAMLANDI (31/31 test yeşil; gerçek DB'de uçtan uca 5.545,20 TL mutabakatı kanıtlandı) — 21.10.2026 onaylandı |
| Faz 4 | API endpoint'leri, PDF/Excel raporlama, Serilog, Global Exception Handling | ✔ TAMAMLANDI (36/36 test yeşil; PDF/Excel canlı üretildi — örnek çıktılar docs altında) — 21.10.2026 |
| Faz 5 | React + Ant Design 5 frontend | ✔ TAMAMLANDI (46/46 test; Dashboard/Daireler+Faturar m² SCD/Faturalar+Hesaplama/Raporlar+Dashboard/Ayarlar+Excel import — Etap 5.5.5 dahil) — 21.10.2026 onaylandı |
| Faz 6 | Docker + Yayına Alma | ✔ TAMAMLANDI (prod compose canlı: web/nginx 8088 → api → postgres, migrator bundle, 3/3 healthy; PDF/Excel prod'da üretildi; Lato font fix) — 22.10.2026 |
| Etap 6.5 | M-Bus Donanım Ayarları (AppSettings'ten dinamik okuyucu + Bağlantıyı Test Et) + Dashboard "Sayaçları Şimdi Oku" | ✔ TAMAMLANDI (canlı doğrulandı: settings roundtrip, test-mbus, trigger Simulator 72 sayaç) |
| Etap 6.6 | Manuel Okuma Girişi + Excel Okuma İçe Aktarma + Dönem Kilitleme (period locking) | ✔ TAMAMLANDI (53/53 test; docs\ETAP66_PLAN.md; canlıda doğrulandı) |
| Etap 6.7 | Fatura Düzenleme + Ayarlar Validasyon Fix + Excel Şablonu (Kat Maliki/m²) + ApartmentOwner SCD Type 2 + Fatura Notu (PDF Ek Açıklamalar) + Son Endeks/Tarih Kolonları + Tüketim Analizi (@ant-design/plots) + Okuma Düzeltme/Toplu Düzenleme + MeterReadingAuditLog | ✔ TAMAMLANDI (62/62 test; migration Etap67AuditAndAnalysis) — 23.10.2026 |
| Etap 6.8 | Tahsilat/Cari Hesap + PDF Ekstre (Faz 6.8) + Devir Bakiyesi Anapara/Faiz Ayrımı (Ledger FIFO: Devir Faiz→Anapara→Fatura; geçmiş faize faiz yok; sentez SourceId; Devir Bakiye Modal'ı) | ✔ TAMAMLANDI (86/86 test; migration Etap68SplitOpeningBalance) — 26.10.2026 |
| Faz 7 | SaaS Multi-Tenant + Identity/JWT (plan: docs\FAZ7_SAAS_PLAN.md) | 📋 PLAN HAZIR (kod yazılacak) |

### Faz 4'te Öğrenilen Teknik Notlar
- QuestPDF 2026.9+: sistem fontları varsayılan yüklünmüyor → Windows Fonts dizini dinamik çözümlenip
  Segoe UI kaydediliyor (Türkçe karakter garantisi; Kural #2 ile uyumlu, mutlak yol yok).
- Akmercan fatura yapısı: Geçişme Bedeli KDV dahil brüt → TotalAmount kullanıcı girişi (S1).
- NU1903: Microsoft.OpenApi 2.7.5 (CVE-2026-49451 düzeltmesi; 3.x hattı AspNetCore.OpenApi ile uyumsuz).
- Lisanslar: QuestPDF Community (<1M$ gelir — onaylı), ClosedXML MIT, MediatR/FluentValidation Apache.

### Faz 2'de Öğrenilen Teknik Notlar
- Gerçek Izar Center telegramlarında ikincil adres (seri no) kaydı, veri kayıtlarından ÖNCE gelebiliyor →
  kayıt sırası güvenilmez; eşleme VIF + "ilk görülen kayıt" mantığıyla yapılıyor (DanfossRecordMapper).
- Sayaçlar: Danfoss SONOMETER 1100 (v20) Heat(Outlet), üretici kodu DFS, 10 veri kaydı (00–09).
- Kayıt 07 (Operating Time → Data Error) cihaz veri hatası bayrağı olarak işlenir.
- Ham telegramlar `RawTelegramHex` ile taşınıyor (ileri adaptasyon garantisi).
