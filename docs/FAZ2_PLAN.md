# FAZ 2 PLANI — M-Bus Okuma Katmanı (Infrastructure)
> Durum: ONAY BEKLİYOR · Referans: docs\BUSINESS_RULES.md

## 0. Kütüphane Keşfi (Önemli Bulgu)
`Valley.Net.Protocols.MeterBus` NuGet'teki eski `1.0.3` (2019, netstandard2.0) sürümüyle değil,
**v4.0.0** ile kullanılacak — GitHub deposu (sympthom/Valley.Net.Protocols.MeterBus, MIT) .NET 10'a
rewrite edilmiş ve şu yetenekler doğrulandı:

- **Transports:** Serial (COM/USB), TCP, UDP — `IsConnected`, `IDisposable`, hostname desteği,
  echo suppression, Windows-uyumlu timeout'lar. `MBusDeframer` frame checksum/L-field/stop byte
  doğrular, gürültüden resync yapar.
- **Master:** Async `MBusMaster`, bus lock, adres başına FCB, `MBusMasterOptions` içinde retry,
  `RequestAllTelegramsAsync`, `ScanAsync`, `SelectSlaveAsync`, ACK okuma, yanıt adresi kontrolü.
- **Parsing (EN 13757-3):** Tam BCD decode (8 haneli değerler dahil), signed 8/24/48-bit int'ler,
  date/time tipleri G/F/I/J, VIF/VIFE tabloları (kWh = Energy VIF), DeviceType isimleri,
  üretici verisi (DIF 0x0F/0x1F), şifreli payload tespiti. libmbus golden testi (70 gerçek telegram)
  + 1160 unit test ile doğrulanmış.
- **DI:** `AddMBusCore(options)` ile singleton master kaydı; `TracingMBusTransport` frame loglama.
- **Hata modeli:** `MBusException`, `TimeoutException`, cancellation propagation.

## 1. IMeterReader Soyutlaması (Factory + Strategy)
Donanım izolasyonu kuralı gereği arayüz **Application** katmanında, implementasyonlar
**Infrastructure** katmanında kalır:

    src\application\Westalife.Application\Common\Interfaces\
      ├── IMeterReader.cs                  → Task<MeterReadingBatch> ReadAllMetersAsync(ct)
      ├── MeterReadingResult.cs (DTO)      → SerialNumber, MeterType, IndexValue, ConsumptionKWh,
      │                                      ReadingDateUtc, IsSuccess, ErrorMessage, RawTelegramHex
      └── MeteringTransportConfig.cs       → TransportType enum (MbusUsb / MbusEthernet / Simulation),
                                             PortName, BaudRate, TcpHost, TcpPort, Timeout'lar

    src\infrastructure\Westalife.Infrastructure\Metering\
      ├── Transports\MeterBusTransportFactory.cs  → (Factory Pattern) TransportType'a göre
      │                                              SerialMBusTransport veya TcpMBusTransport üretir
      ├── MBusMeterReader.cs                      → (Strategy #1) Gerçek donanım: Valley v4.0.0
      │                                              MBusMaster ile okuma + parse
      ├── SimulatedMeterReader.cs                 → (Strategy #2) Simülatör (bkz. madde 4)
      ├── MeterReaderFactory.cs                   → (Factory Pattern) Config'e göre doğru IMeterReader
      │                                              stratejisini döndürür
      └── Parsing\DanfossDataRecordMapper.cs      → DataRecord → MeterReadingResult dönüşümü

DI'da tek ayarlarla strateji seçilir: `Metering:Mode` = Simulation | MbusUsb | MbusEthernet.
Kodda if/else donanım kontrolü olmaz — strateji farkı tamamen fabrika içinde.

## 2. Veri Parse Etme (Danfoss / Izar Center)
- Valley v4.0.0 telegram'ı `VariableDataPacket.DataRecords` olarak verir; her record DIF/VIF taşır.
- **DanfossDataRecordMapper** seçerek map eder:
  - Energy (VIF 0x0E ailesi, VariableDataQuantityUnit.Energy) → kümülatif kWh (`IndexValue`)
  - Dönem tüketimi (varsa delta register) → `ConsumptionKWh`
  - Date/Time (VIF tip G/F) → `ReadingDateUtc` (UTC'ye normalize)
  - Identification → seri numarası (BCD, libmbus aritmetiği)
- Her telegram'ın **hex dump'ı** `RawTelegramHex` alanında saklanır (denetim izi / fatura
  endeksi çapraz doğrulama için).
- Izar Center: master olarak tüm alt istasyonları `RequestAllTelegramsAsync` ile gezer;
  sekonder adresleme gerekirse `SelectSlaveAsync`.
- Parse edilemeyen record'lar batch'i düşürmez; `IsSuccess=false` + `ErrorMessage` ile işaretlenir.

## 3. Hata Yönetimi ve Retry (Polly)
Katmanlı politika (Serilog ile her adım loglanır):
1. **Transport seviyesi:** Valley'nin kendi `MBusMasterOptions` retry'ları (frame seviyesi, FCB'yi
   koruyarak tekrar) — kablo gürültüsü/tek frame kaybı için.
2. **Operasyon seviyesi (Polly):**
   - Retry: 3 deneme, exponential backoff (2s → 4s → 8s) + jitter; yalnızca `TimeoutException`,
     `MBusException` ve IO istisnalarında.
   - Circuit Breaker: 5 ardışık hata → 1 dk açık; bus'a zarar vermemek ve çökmemek için.
   - Timeout: her sayaç okuması için ayrı CancellationToken (M-Bus 9600 baud gerçekçi süreler).
3. **Sonuç modeli:** Hiçbir hata exception olarak Application'a sızmaz; `MeterReadingResult.IsSuccess`
   ile raporlanır. Başarısız sayaçlar atlanır, oturum özeti (başarılı/başarısız sayısı) üretilir.
   Sistem ASLA çökmez.

## 4. Simülatör (MockMeterReader)
`SimulatedMeterReader : IMeterReader` — gerçek donanımsız tüm akışı çalıştırır:
- Kayıtlı metriklerden (SerialNumber listesi — Faz 1'deki Meters tablosundan veya config'den)
  gerçekçi değerler: 72 daire + 1 ana sayaç; kümülatif endeksler monoton artar, dönem tüketimi
  daire başına mevsimsel/rassal profil takip eder (deterministik seed'li).
- Hata senaryoları config ile tetiklenebilir (örn. "Sayaç 12-37 timeout versin") → retry/CB testi.
- `Metering:Mode=Simulation` ile dev/prod kodu HİÇ değişmeden seçilir.

## 5. Faz 2 Teslim Listesi (Definition of Done)
1. Application interfaces + DTO'lar — derlenir
2. Infrastructure Metering klasörü: TransportFactory, MBusMeterReader, SimulatedMeterReader,
   MeterReaderFactory, DanfossDataRecordMapper
3. Polly paketi + Retry/CircuitBreaker politikası (Infrastructure içinde, dışarı sızmaz)
4. Serilog ile frame + operasyon loglama (TracingMBusTransport entegrasyonu)
5. Smoke test: Simulation modunda 72+1 sayaç okuma → DTO listesi (DB yazımı YOK — Faz 3'te
   MediatR komutuyla yapılacak)
6. Unit testler: DanfossDataRecordMapper (gerçek telegram hex örnekleriyle), retry politikası,
   SimulatedMeterReader determinizmi

## 6. Açık Sorular (onayla birlikte netleşsin)
- **S1:** Polly paketi mi, .NET 10 yerleşik resilience API'si mi? (Polly öneriyorum: siz istediniz
  + endüstri standardı.)
- **S2:** Test projesi (xUnit) Faz 2'de mi kurulsun, Faz 6'da mı?
- **S3:** Izar Center'ın gerçek cihaz modeli/seri no ve M-Bus bağlantı parametreleri (baud rate,
  birincil adresler) elinizde varsa paylaşın — simülatör profilini buna göre kurarım.

