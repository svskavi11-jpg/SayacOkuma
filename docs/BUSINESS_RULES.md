# Westalife Residence — İş Kuralları Dokümanı

## KURAL #2 — YOL VE İSİMLENDİRME HİJYENİ (Mimari Kural)

> **Kaynak:** Proje sahibi, 21.10.2026

1. **Mutlak yol yasak:** Kodun hiçbir yerine (dosya yolları, PDF kaydetme, loglama, yedekleme vb.)
   `G:\...` gibi sabit Windows yolları **hardcode EDİLMEZ**. Tüm dosya/klasör yolları:
   - `appsettings.json` üzerinden (relative path, örn. `"Serilog": { "WriteTo": [ { "Args": { "path": "logs/..." } } ] }`), veya
   - `AppContext.BaseDirectory` / `Directory.GetCurrentDirectory()` gibi dinamik yöntemlerle
   belirlenir. Docker dağıtımında (Faz 6) bunlar volume mount ile eşlenir.
2. **İsimlendirme:** Kodda, veritabanında, loglarda ve raporlarda **"Lokman Abi" ibaresi GEÇMEZ**;
   yalnızca **"Westalife Residence"** kullanılır. (Klasör adı yalnızca geliştirme makinesinde
   depolama konumudur — sistemin kimliğine girmez.)

**Teyit (21.10.2026):** Tüm `*.cs / *.json / *.yml` kod tabanı tarandı → mutlak yol veya "Lokman Abi" ibaresi **yok**.

---

## KURAL #1 — KURUŞU KURUŞUNA MUTABAKAT (Kritik Finansal Kural)

> **Kaynak:** Proje sahibi, 20.10.2026
> **Uygulanacak faz:** Faz 3 (Hesaplama Motoru)

Kat maliklerinden tahsil edilecek **toplam tutar**, sisteme girilen **Bina Doğalgaz Faturası tutarını kuruşu kuruşuna birebir** karşılamalıdır.

### Yöntem: En Büyük Kalanlar (Largest Remainder Method / Hamilton Metodu)

1. Tüm dairelerin borçlarını normal hesapla, **kuruş bazında yuvarla** (tüm hesaplamalar `decimal`/`long` kuruş cinsinden tam sayı mantığıyla yapılır; floating-point **yasak**).
2. Yuvarlanmış borçların toplamı ile faturanın ilgili yüzdesi (örn. %30 sabit gider tutarı) arasındaki **kuruş farkını** (penny gap) bul.
3. Bu farkı, **virgülden sonraki küsüratı en yüksek** olan dairelerden başlayarak **1'er kuruş** olarak dağıt.
4. Eşitlik (tie) durumunda belirleyici ikincil sıralama uygulanacak (örn. tüketimi yüksek → kapı numarası küçük olan; Faz 3'te netleştirilecek ve kodda sabit olarak tanımlanacak — magic number yok).

### Amaç
Sistemin ürettiği raporların genel toplamı, faturayı veren kurumun (Akmercan) faturasıyla **kuruşu kuruşuna mutabık** kalmalıdır.

### Kontrol Kuralı (Faz 3 + Faz 4 raporlama)
`Σ(daire borçları) == fatura tutarı` → DEĞİLSE hesaplama **hata fırlatır**, rapor üretilmez.
Bu kontrol hem hesaplama motorunda hem de PDF/Excel raporunda zorunlu doğrulama (invariant) olarak yer alacaktır.

---

## ÖRNEK FATURA ANALİZİ (Akmercan — T6772024000027623)

### Fatura Künyesi
| Alan | Değer |
|---|---|
| Abone | WESTALIFE RESIDANCE KAT MALİKLERİ AP (Bina ana aboneliği) |
| Abone No | 10060828 · Sayaç No: 2160191 · Tarife: Konut |
| İlk Okuma | 16.05.2024 / Endeks: 140.399 |
| Son Okuma | 20.06.2024 / Endeks: 140.951 |
| **Fatura Dönemi** | **16.05.2024 → 20.06.2024** (okuma tarihleri arası = SCD lookup dönemi) |
| Sayaçtan Ölçülen Hacim | 552,00 m³ |
| Basınç Düzeltme Katsayısı (K) | 1,225880 |
| Düzeltilmiş Tüketim | 693,25 m³ |
| Üst Isıl Değer (kcal/m³) | 9.027,6679 |
| **Tüketilen Enerji (kWh)** | **7.273,65 kWh** ← bina ana sayacı toplam enerjisi |
| Birim Fiyat | 0,56251503 TL/kWh |

### Tutar Kırılımı (KURUŞ HASSASİYETİYLE) — DÜZELTİLMİŞ
> ⚠️ **Düzeltme (entegrasyon testi bulgusu):** İlk analizde kalem toplamı yanlış eşitlenmişti.
> Gerçek yapı: **Geçişme Bedeli (529,46) KDV DAHİL brüttür** (içindeki KDV = 105,89 = tam %20),
> bu KDV satırı toplama TEKRAR eklenmez.
> **Doğru mutabakat: 4.091,54 + 924,20 + 529,46 = 5.545,20 TL** ✔
> Bu yüzden `GasInvoice.TotalAmount` kullanıcı tarafından açıkça girilir (Ödenecek Bedel);
> kalem tutarları yalnızca denetim amaçlı saklanır (S1 kararıyla uyumlu).
| Kalem | Tutar (TL) | KDV %20 (TL) | Toplam (TL) |
|---|---|---|---|
| Tüketim Bedeli (7.273,65 kWh) | 4.091,54 | 924,20 | 5.015,74 |
| Geçişme Bedeli (dağıtım) | 529,46 | 105,89 | (KDV dahil) |
| **G.Toplam / Ödenecek Bedel** | | | **5.545,20** |

### Faz 1 Veri Modeline Etkiler
1. **`GasInvoice` tekil "TotalAmount" ile yetinemez** → fatura kalemleri ayrı tutulmalı:
   - `ConsumptionAmount` (Tüketim Bedeli), `ConsumptionVat`, `DistributionAmount` (Geçişme Bedeli), `DistributionVat`, `TotalAmount`.
   - Alternatif: `GasInvoiceLineItem` alt entity (kalem kodu enum: Consumption, Distribution, Vat, Rounding, Other). → **Karar: projede netleşince** (aşağıdaki açık sorulara bağlı).
2. **Okuma endeksleri faturadan bağımsız doğrulanabilir** → `MeterReading` kümülatif endeks (m³) + türetilmiş kWh tutar; binanın ana doğalgaz sayacı da sistemde bir `Meter` olarak modellenecek (kalorimetreden ayrı tip: `GasMasterMeter`).
3. **Dönem tanımı** = okuma tarihleri arası (16.05–20.06.2024); `GasInvoice`'da `ReadingDateFrom`/`ReadingDateTo` ayrıca tutulmalı (fatura düzenleme tarihinden farklı).
4. **KDV oranı (%20)** sabit KODLANMAYACAK; fatura girişinde kalemler olduğu gibi alınır (magic number yasağı + mevzuat değişebilir).
5. Eski borç (12.915,83 TL) bu sistemin kapsamı DIŞINDA (takip sistemi: borç yönetimi, fatura paylaşımı).

### AÇIK SORULARIN CEVAPLARI (20.10.2026 — Proje sahibi tarafından netleştirildi)
- **S1 CEVAP:** 30/70 dağıtımına faturadaki KDV ve Geçişme Bedeli dahil **Toplam Ödenecek Tutar** (örn. 5.545,20 TL) girecek. Ekstra kalem ayrıştırması YOK. (Danfoss çıktısı da toplam tutarı direkt %30/%70 ikiye böler.)
- **S2 CEVAP:** %70 paylaştırmasında payda (bölen) **sadece dairelerin kalorimetre kWh toplamıdır** (örn. 72 dairenin kalorimetre toplamı 6.500 kWh ise payda 6.500). Ana sayaç ile daireler arasındaki kayıp-kaçak/kazan verim kaybı farkı %70'in içine EKLENMEZ; bu fark %30'luk sabit gider (m²) kalemi içinde eritilir. (Kat maliklerinin kWh birim fiyatı şişmez.)
- **S3 CEVAP:** Evet — ana sayaç kWh'i ile dairelerin kWh toplamı arasındaki fark, yönetici panosunda (Dashboard) **"Bina Kayıp-Kaçak / Ortak Alan Tüketimi Raporu"** olarak gösterilecek. Kat malikine giden bireysel ödeme bildiriminde (Danfoss formu) sadece kendi tüketimi ve birim fiyatı yer alacak.

---

## HESAPLAMA YAPISI (Mevcut Kurallar)

| Bileşen | Oran | Dağıtım Anahtarı |
|---|---|---|
| Sabit Gider | %30 | Daire m² alanına göre (SCD Type 2 üzerinden dönemsel m²) |
| Tüketim Gideri | %70 | Kalorimetre tüketimi (kWh) |

- Her iki bileşen için de **ayrı ayrı** Largest Remainder Method uygulanacaktır.
- SCD Type 2: m² / ortak alan oranı değişimlerinde eski veri ezilmez; `ValidFrom` / `ValidTo` ile yeni kayıt açılır.
