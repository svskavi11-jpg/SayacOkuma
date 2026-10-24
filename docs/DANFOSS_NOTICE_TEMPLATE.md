# Danfoss "Bireysel Gider Bildirimi" — PDF Şablon Spesifikasyonu

> Kaynak: Kullanıcı tarafından gönderilen örnek form (Ocak 2022, Daire 64) — Faz 4'te QuestPDF ile birebir üretilecek.

## Sayfa Yapısı

### 1. Başlık Bölgesi
- **Orta üst:** "BİREYSEL GİDER BİLDİRİMİ" + altında dönem adı (örn. "Ocak 2022" → bizde fatura dönemi adı)
- **Sol üst:** Hizmet firması logosu (örnek formda "Çınar Teknik" — bizde logo placeholder / appsettings'ten opsiyonel)
- **Sağ üst:** Danfoss logosu (placeholder — marka kullanımına dikkat; metin de olabilir)

### 2. Künye Blokları (iki kolon)
| Sol Kolon | Sağ Kolon (sistem alanı eşlemesi) |
|---|---|
| DAİRE 64 | Abone No → *(Akmercan abone no: 10060828 — binanın tek aboneliği)* |
| WESTALIFE RESIDENCE | Daire → 64 (kapı no) |
| Mithat Paşa Mh. Bulent Ecevit Cd. No:67 (adres) | Dönem → 01.01.2022–31.01.2022 (`GasInvoice.ReadingDateFrom/To`) |
| | Ödenen Süre → 30 Gün (dönem gün sayısı) |
| | Oluşturma Tarihi → hesaplama tarihi (`ApartmentInvoice.CalculatedOnUtc`) |
| | **Son Ödeme Tarihi** → *yeni alan: appsettings default (örn. oluşturma + 15 gün) veya manuel giriş* |
| | **Toplam Ödenecek Tutar** → `ApartmentInvoice.TotalAmount` |

### 3. "Bireysel Gider Tüketiminin Paylaştırılması" (dairenin kendi payları)
| Satır | Birim Bedeli | Birim/miktar | Tutar |
|---|---|---|---|
| %30 Sabit | 1,344167 TL/m² alan | 72,000 m² alan (dairenin m²) | 96,78 TL (`FixedAmount`) |
| %70 Tüketim | 0,244630 TL/kWh | 0,000 kWh (dairenin tüketimi) | 0,00 TL (`ConsumptionAmount`) |
| Okuma Hizmet Bededi | — | — | 0,00 TL *(opsiyonel satır — appsettings: varsayılan 0)* |

- **Birim bedel hesabı:** sabit birim = (bina sabit gider toplamı ÷ Σ m²); tüketim birim = (bina tüketim gideri toplamı ÷ Σ kWh). Yani formdaki 1,344167 = 4299,40 ₺ sabit havuzu / toplam m²; 0,244630 = 13.475,19 ₺ / 55.084 kWh.
- **Toplam Ödenecek Tutar** = %30 satırı + %70 satırı + Okuma Hizmet Bededi.

### 4. "Isınma Ve Soğutma Tüketiminin Paylaştırılması" (bina ölçeği — şeffaflık bloğu)
| Satır | % Pay | Tutar | Toplam Birim |
|---|---|---|---|
| Bina Doğalgaz Faturası | 100,00 % | 12.929,25 TL | → Birim Bedeli kolonu (sağda) |
| Toplam Enerji Tüketim Bedeli / 30% Sabit | 30,00 % | 4.299,40 TL | 4299,400 m² alan · 1,344167 TL/m² |
| / 70% Tüketim | 70,00 % | 13.475,19 TL | 55084,000 kWh · 0,244630 TL/kWh |

→ Bizde: "Bina Doğalgaz Faturası" = `GasInvoice.TotalAmount` (Akmercan, 5.545,20); %30/%70 satırları hesaplama motorunun havuz tutarları.

### 5. "Okuma Değerleri" (dairenin sayaç geçmişi)
| Alan | Sistem eşlemesi |
|---|---|
| İş Sayacı / Sayaç Seri No | `Meter.SerialNumber` (örn. 55238358) |
| Okuma Tarihi | dönem son okuma tarihi (`MeterReading.ReadingDateUtc`) |
| İlk Okuma / Son Okuma | dönem başı ve sonu kümülatif endeks (kWh) |
| Tüketim | dönem tüketimi (`ApartmentInvoice.ConsumptionKWhUsed`) |
| Toplam Gider | toplam borç |

⚠️ Not: Formda "İlk Okuma == Son Okuma == 19126 kWh, Tüketim = 0" — kapı 72 benzeri okunamayan/0 tüketimli daireler formda bu şekilde görünür.

### 6. "Ek Açıklamalar" (serbest metin, genelde boş)
→ `appsettings` üzerinden yapılandırılabilir global açıklama (örn. "Kayıp-kaçak %30 sabit gider içinde eritilmiştir").

### 7. Dikey filigran (sağ kenar, dikey metin)
"BİREYSEL GİDER BİLDİRİMİ" — QuestPDF Rotate ile.

## Sistemde Üretim Stratejisi
- **Tek daire:** `/reports/notice.pdf?apartmentId=...` → 1 sayfa
- **Tüm bina:** `/reports/notice.pdf` → 72 sayfa (her daire bir sayfa) + opsiyonel özet sayfa
- **Veri kaynağı:** `GetCalculationResultQuery` + `GasInvoice` + daire başına okuma değerleri (ilk/son endeks)
- **Invariant (Kural #1):** PDF üretiminden önce Σ satır == fatura tutarı tekrar doğrulanır; değilse üretim reddedilir.
- **Yollar:** çıktı HTTP stream olarak döner (diske yazılmaz) — Kural #2 (mutlak yol yok) ile uyumlu.
- Birim fiyatlar `decimal` ile hesaplanır, formda 6 ondalık gösterilir; tutarlar kuruş hassasiyetiyle.
