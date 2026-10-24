# FAZ 7 — SaaS (Multi-Tenant) ve Kimlik Yönetimi Hazırlık Planı

> Bu doküman Faz 6 kapanışında yazılmıştır. Faz 6'da kod yazılmamış, yalnızca altyapı kararları kaydedilmiştir.

## 1. Çoklu Kiracı (Multi-Tenant) Stratejisi — SEÇİM: (A)

### Seçilen Yol: (A) Tek DB + Paylaşımlı Şema + TenantId
- Tüm iş verisi entity'lerine (`Building`, `Apartment`, `ApartmentDimension`, `Meter`, `MeterReading`, `GasInvoice`, `ApartmentInvoice`) `TenantId` eklenir — **`BaseEntity`'e değil**, `ITenantEntity` arayüzü üzerinden (temiz ayrım; `AppSettings` kiracı bazlı çoğalacak, aynı arayüzü alır).
- **Global Query Filter:** `builder.Entity<T>().HasQueryFilter(t => t.TenantId == _tenantContext.TenantId)` — tüm handler/sorgular OTOMATİK filtrelenir; mevcut CQRS handler'larında değişiklik minimal.
- **Migration geçişi:** Yeni migration `TenantId` kolonunu NULL ekler → mevcut Westalife verisi tek kiracıya backfill edilir (script) → kolon NOT NULL + index `IX_<table>_TenantId` yapılır. Mevcut veri bozulmaz.
- **Kimlik çözümleme:** JWT claim `tenantId` → `ITenantContext` (scoped) → query filter + `SaveChanges` interceptor (yeni entity'lere otomatik TenantId yazar).
- (B) DB-per-tenant: kurumsal müşteriler için ileride opsiyon (connection-per-tenant factory).

### Kiracı Bazlı Ayarlar
`AppSettings` tek satır yerine **her kiracıya bir satır** olur (TenantId ile) — şirket adı, bina adı, oranlar kiracı bazında yönetilir. Building de kiracıya bağlı olduğu için mirror mantığı değişmez.

## 2. Kimlik Yönetimi (Identity + JWT)

- **ASP.NET Core Identity + JWT Bearer:**
  - Roller: `SiteYonetici` (tam yetki), `Muhasebeci` (fatura+hesaplama+rapor), `OkumaPersoneli` (okuma tetikleme), kiracı başına sınırsız kullanıcı
  - Endpoint'ler: `POST /auth/login`, `POST /auth/refresh`, `GET/PUT /users` (SiteYonetici)
  - JWT claim'leri: `tenantId`, `role`, `userId` → `ITenantContext` + `[Authorize(Roles = ...)]`
- **Frontend hazır deseni:** `api/client.ts`'e tek token interceptor + 401'de `/login` redirect; `ProtectedRoute` sarmalayıcı; menü roller filtreler (desenler Faz 5'te kuruldu, eklenmesi dakikalar sürer)
- **İlk kullanıcı:** Tenant provisioning sırasında seed edilir (Faz 6'daki HasData deseninin aynısı)

## 3. Docker Etkisi — SIFIR YAPISAL DEĞİŞİKLİK
- Aynı imaj/compose çalışır; API stateless (oturum DB'de/JWT'de) → `replicas` ile yatay ölçekleme mümkün
- Tek Postgres yeterli; tenant bazlı izole DB istenirse connection-per-tenant ile compose'a ek instance eklenir
- Ek env: `JWT_SECRET`, `JWT_ISSUER`, `JWT_AUDIENCE` (`.env.example` genişletilir)
- Çoklu instance'ta Serilog: dosya yerine console-only (Docker log driver) + opsiyonel Seq/ELK

## 4. Operasyonel Teerrüf Notları (Faz 6'da kurulan temeller)
- **Backup:** `pg_dump` günlük cron + restore dokümanı (docker exec ile)
- **HTTPS:** Caddy/Traefik önüne alma (web container'ının 80 portu arkada kalır) — cert otomatik yenileme
- **Loglar:** volume'daki `logs/` klasörü periyodik arşivlenir; çoklu instance'ta stdout'a geçilir
- **Yeni kiracı açılışı (runbook):** kiracı kaydı → ilk kullanıcı → bina + daireler (Excel import!) → sayaçlar (Excel import + başlangıç endeksleri) → ilk fatura dönemi
