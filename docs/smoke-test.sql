-- Faz 1 duman testi #2: SAVEPOINT'li — tüm senaryolar tek transaction'da test edilir
-- Beklenen: 1,2 BAŞARILI; 3 HATA (çakışma); 4 BAŞARILI (kapat→yeni); 5 HATA (2. aktif); 6 BAŞARILI (fatura); 7 HATA (dönem çakışması)

BEGIN;

INSERT INTO "Buildings" ("Id","Name","Address","FixedCostRatio","IsActive","CreatedOn")
VALUES ('11111111-1111-1111-1111-111111111111','Westalife Residence','Zonguldak',0.30,true,now());

INSERT INTO "Apartments" ("Id","BuildingId","DoorNumber","Title","IsActive","CreatedOn")
VALUES ('22222222-2222-2222-2222-222222222222','11111111-1111-1111-1111-111111111111','1','Daire 1',true,now());

-- İlk SCD kaydı (aktif)
INSERT INTO "ApartmentDimensions" ("Id","ApartmentId","SquareMeters","CommonAreaRatio","ValidFrom","CreatedOn")
VALUES ('33333333-3333-3333-3333-333333333333','22222222-2222-2222-2222-222222222222',120,0.05,'2024-01-01T09:00:00+03',now());

-- SENARYO 3: ÇAKIŞAN SCD kaydı → HATA OLMALI
SAVEPOINT s3;
INSERT INTO "ApartmentDimensions" ("Id","ApartmentId","SquareMeters","CommonAreaRatio","ValidFrom","CreatedOn")
VALUES ('44444444-4444-4444-4444-444444444444','22222222-2222-2222-2222-222222222222',130,0.05,'2024-06-01T09:00:00+03',now());
ROLLBACK TO SAVEPOINT s3;

-- SENARYO 4: Eski kaydı kapat → çakışmayan yeni kayıt → BAŞARILI OLMALI
UPDATE "ApartmentDimensions" SET "ValidTo"='2024-06-01T09:00:00+03'
WHERE "Id"='33333333-3333-3333-3333-333333333333';
INSERT INTO "ApartmentDimensions" ("Id","ApartmentId","SquareMeters","CommonAreaRatio","ValidFrom","CreatedOn")
VALUES ('44444444-4444-4444-4444-444444444444','22222222-2222-2222-2222-222222222222',130,0.05,'2024-06-01T09:00:00+03',now());

-- SENARYO 5: İKİNCİ aktif kayıt (ValidTo NULL) → HATA OLMALI (partial unique)
SAVEPOINT s5;
INSERT INTO "ApartmentDimensions" ("Id","ApartmentId","SquareMeters","CommonAreaRatio","ValidFrom","CreatedOn")
VALUES ('55555555-5555-5555-5555-555555555555','22222222-2222-2222-2222-222222222222',130,0.05,'2024-07-01T09:00:00+03',now());
ROLLBACK TO SAVEPOINT s5;

-- SENARYO 6: Örnek Akmercan faturası → BAŞARILI OLMALI
INSERT INTO "GasInvoices" ("Id","InvoiceNumber","SubscriberNumber","MasterMeterSerial",
    "ReadingDateFrom","ReadingDateTo","IssuedOn","CorrectedVolumeM3","TotalEnergyKWh",
    "PressureCorrectionFactor","ConsumptionAmount","ConsumptionVat","DistributionAmount","DistributionVat","TotalAmount","CreatedOn")
VALUES ('66666666-6666-6666-6666-666666666666','T6772024000027623','10060828','2160191',
    '2024-05-16T00:00:00+03','2024-06-20T00:00:00+03','2024-06-20T15:20:34+03',
    693.25, 7273.65, 1.225880, 4091.54, 924.20, 529.46, 105.89, 5545.20, now());

-- SENARYO 7: ÇAKIŞAN dönemde ikinci fatura → HATA OLMALI (exclusion)
SAVEPOINT s7;
INSERT INTO "GasInvoices" ("Id","InvoiceNumber","SubscriberNumber","MasterMeterSerial",
    "ReadingDateFrom","ReadingDateTo","IssuedOn","CorrectedVolumeM3","TotalEnergyKWh",
    "PressureCorrectionFactor","ConsumptionAmount","ConsumptionVat","DistributionAmount","DistributionVat","TotalAmount","CreatedOn")
VALUES ('77777777-7777-7777-7777-777777777777','T6772024000027624','10060828','2160191',
    '2024-06-01T00:00:00+03','2024-07-01T00:00:00+03','2024-07-01T15:20:34+03',
    500.00, 5000.00, 1.225880, 3000.00, 600.00, 300.00, 60.00, 3960.00, now());
ROLLBACK TO SAVEPOINT s7;

ROLLBACK;
