# ETAP 6.6 PLANI — Manuel Okuma, Excel Okuma İçe Aktarma, Dönem Kilitleme

> Durum: **✔ TAMAMLANDI** — 53/53 test yeşil, prod'a deploy edildi ve canlıda doğrulandı.
> Kararlar: dönem dışı okuma uyarıyla kaydedilir; import Faturalar sayfasında; yalnız hesaplanmış fatura kilitlenir; kilitliyken yeniden hesaplama engellenir.
> Ön koşul: Etap 6.5 tamamlandı (46/46 test yeşil, prod canlı).

## Amaç
1. Saha arızalarına karşı B planı: M-Bus okunamadığında elle okuma girişi ve dış kaynak Excel içe aktarma.
2. Muhasebe/denetim standardı: fatura dönemi kilitlenmesi (period locking) + denetim izi temeli.

---

## Özellik 1 — Manuel Sayaç Okuma Girişi (UI)

### Backend
- **Komut:** `AddManualMeterReadingCommand(ApartmentId, MeterId, ReadingDate, IndexValue)`
  - Dosya: `src/application/Westalife.Application/Features/MeterReadings/Commands/AddManualMeterReading/`
  - FluentValidation: MeterId zorunlu, IndexValue >= 0, tarih bugünden ileri olamaz.
  - Handler davranışı (RecordMeterReadingsHandler ile aynı çekirdek mantık):
    - Aynı sayaç + aynı tarih kayıtlıysa → **409 Duplicate** (skip değil, kullanıcıya net hata).
    - Tüketim = son okumaya göre fark (`max(0, index - lastIndex)`); hiç okuma yoksa `IsInitialReading=true`, tüketim 0.
    - **Endeks gerilemesi** (yeni index < son index) → **engellenir** (DomainException): muhasebe güvenliği.
  - `ReadingSource.ManualEntry` ile kaydedilir.
- **Endpoint:** `POST /api/v1/meter-readings/manual` (MeterReadingsController).
- **Dönem uyarısı (engelleme DEĞİL):** Girilen tarih hiçbir `GasInvoice` dönemine
  (`ReadingDateFrom..ReadingDateTo`) denk gelmiyorsa yanıt `Warnings` listesinde
  "Bu tarih hesaplanacak fatura dönemlerinin dışında — bu okuma hiçbir hesaplamada kullanılmaz."
  uyarısı döner; kayıt yine yapılır (B planı senaryosu bunu gerektirir).
  Kilitli döneme denk gelirse **engellenir** (Özellik 3 koruması).

### Frontend (ApartmentsPage)
- Daireler tablosuna satır aksiyonu: **"Manuel Okuma Gir"** butonu.
- Modal alanları:
  - **Sayaç:** Select (dairenin aktif sayaçları — tek sayaç varsa otomatik seçili; ApartmentListItem.Meters zaten geliyor)
  - **Okuma Tarihi:** DatePicker (gg.aa.yyyy)
  - **Kümülatif Endeks (kWh):** InputNumber (min 0)
- Başarılı → sonuç modalı: kaydedilen tüketim + varsa dönem dışı uyarısı (Alert).

## Özellik 2 — Okuma Verisi Excel İçe Aktarma

### Backend
- **Parser:** `IExcelReadingImportParser` (Application) + `ExcelReadingImportParser` (Infrastructure, ClosedXML)
  — mevcut `ExcelMeterImportParser` deseninin birebir kopyası (dinamik başlık eşlemesi, satır bazlı hatalar, TR tarih formatı gg.aa.yyyy).
  - Sütunlar: `Kapı No`, `Okuma Tarihi`, `Endeks (kWh)` (hepsi zorunlu).
- **Şablon:** `ReadingImportExcelTemplate.Generate()` → GET `/api/v1/meter-readings/import-template`
  (Aciklamalar sayfası dahil — mevcut sayaç şablonuyla aynı üslup).
- **Komut:** `ImportReadingsFromExcelCommand(byte[] ExcelContent)` → `Features/MeterReadings/Commands/ImportReadingsFromExcel/`
  - Kapı No → aktif kalorimetre çözümleme; eşleşmeyen kapı → `UnmatchedApartmentCount` + satır hatası.
  - Her satır Özellik 1'deki aynı validasyonlardan geçer (duplicate, gerileme, kilitli dönem).
  - Kilitli döneme denk gelen satır → satır hatası olarak raporlanır, diğer satırlar işlenir (kısmi başarı modeli).
  - `ReadingSource.Import` ile kaydedilir. Tek SaveChanges = tek transaction.
- **Endpoint:** `POST /api/v1/meter-readings/import-excel` (IFormFile, max 5 MB — mevcut desen).
- **Sonuç DTO:** `ImportedCount, SkippedDuplicateCount, UnmatchedApartmentCount, RowErrors[]`.

### Frontend (InvoicesPage)
- **"Okuma Verilerini İçe Aktar"** butonu (Upload, beforeUpload ile anında POST) + **"Şablon İndir"** butonu.
- Sonuç modalı: eklenen / duplicate / eşleşmeyen sayıları + satır hataları listesi (Alert).

## Özellik 3 — Dönem Kilitleme (Period Locking)

### Domain + DB
- **Migration:** `AddGasInvoicePeriodLock` → `GasInvoice` tablosuna:
  - `IsLocked` (bool, default false)
  - `LockedOn` (DateTimeOffset?) — denetim izi; `LockedBy` Faz 7'de TenantId ile eklenecek (şimdilik yok).
- **Domain metotları:** `Lock()` (yalnız hesaplama varken çağrılabilir — kontrol Application'da), `Unlock()`.
- **DomainException mesajları:** `DomainConstants.ValidationMessages`'a eklenir (kodda literal mesaj yok).

### Backend
- `POST /api/v1/gas-invoices/{id}/lock` → yalnız **hesaplaması olan** fatura kilitlenebilir (yoksa 409).
- `POST /api/v1/gas-invoices/{id}/unlock` → şimdilik herkes; Faz 7'de `[Authorize(Roles="TenantAdmin")]`.
- `GetGasInvoicesQuery` → `GasInvoiceListItem`'a `IsLocked` eklenir.
- **Zorlama noktaları (tümü):**
  1. Manuel okuma (Özellik 1) — kilitli döneme düşen tarih → DomainException.
  2. Excel içe aktarma (Özellik 2) — satır bazlı hata.
  3. M-Bus trigger — kilitli döneme düşen okumalar kaydedilmez, `LockedPeriodSkippedCount` ile raporlanır
     (TriggerMeterReadingCycleResult'a alan eklenir; normalde 0).
  4. Kilitli faturada **yeniden hesaplama engellenir** (CalculateCostDistribution → 422).
  - Not: Okuma silme/düzenleme endpoint'i zaten yok (hard delete yasak) — kilit ayrıca onları korur.

### Frontend (InvoicesPage)
- Fatura tablosu "Durum" sütununa **"Kilitli" Tag'i** (kırmızı kilit ikonu).
- **"Kilitle"** butonu (`hasCalculation && !isLocked`) → onay modalı:
  "Bu döneme okuma eklenemez ve yeniden hesaplama yapılamaz."
- **"Kilidi Aç"** butonu (isLocked) → uyarılı onay modalı.

## Test Planı (Westalife.Infrastructure.Tests — Etab66Tests.cs)
1. Manuel okuma: başarılı kayıt + `Source=ManualEntry` + tüketim farkı doğru hesaplanır.
2. Manuel okuma: dönem dışı tarih → kayıt oluşur + Warning döner.
3. Manuel okuma: endeks gerilemesi → hata; duplicate tarih → 409.
4. Excel içe aktarma: şablon parse + `Source=Import` kayıtları; eşleşmeyen kapı → satır hatası.
5. Kilit: hesaplamasız fatura kilitlenemez; kilit sonrası manuel okuma / Excel satırı / yeniden hesaplama engellenir; unlock sonrası kabul edilir.
6. Regresyon: mevcut 46 test yeşil kalır.

## Docker/Prod (kod onay sonrası)
- Yeni migration'la API imajı yeniden build → migrator prod DB'ye `AddGasInvoicePeriodLock` uygular → `up -d` → smoke test.

## Dosya Listesi (tahmini ~20 dosya)
- **Application:** AddManualMeterReading/, ImportReadingsFromExcel/, LockPeriod+UnlockPeriod/, IExcelReadingImportParser, TriggerMeterReadingCycle (LockedPeriodSkippedCount), GetGasInvoices (IsLocked), CalculateCostDistribution (kilit kontrolü)
- **Domain:** GasInvoice (IsLocked/LockedOn + Lock()/Unlock()), DomainConstants (yeni mesajlar)
- **Infrastructure:** ExcelReadingImportParser, ReadingImportExcelTemplate, migration `AddGasInvoicePeriodLock`
- **API:** MeterReadingsController (manual + import-template + import-excel), GasInvoicesController (lock/unlock)
- **Frontend:** types/api.ts, ApartmentsPage, InvoicesPage
- **Tests:** Etab66Tests.cs (yeni)

## Karar Noktaları (onayınızla netleşir)
1. **Dönem dışı manuel okuma:** engellemeden uyarıyla kaydediliyor (B planı esnekliği). İsterseniz engellemeye çevirebiliriz.
2. **Excel içe aktarma sayfası:** Faturalar sayfası önerildi (okumalar dönemsel olduğu için). İsterseniz Daireler'e de eklenebilir.
3. **Kilitleme ön koşulu:** yalnız hesaplanmış fatura kilitlenebilir. İsterseniz hesaplamasız da kilitlenebilir.
4. **M-Bus trigger davranışı:** kilitli döneme düşen otomatik okumalar atlanır ve sayılır (normalde kilit geçmiş dönemde olur, etki sıfır).
