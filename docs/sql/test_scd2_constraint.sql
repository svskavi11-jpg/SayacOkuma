-- SCD Type 2 exclusion constraint davranışsal testi (geçici, transaction içinde — ROLLBACK ile iz bırakmaz)
BEGIN;

INSERT INTO "Buildings" ("Id", "Name", "Address", "FixedCostRatio", "IsActive", "CreatedOn")
VALUES (gen_random_uuid(), 'Westalife Residence', 'Zonguldak', 0.30, true, now());

INSERT INTO "Apartments" ("Id", "BuildingId", "DoorNumber", "Title", "IsActive", "CreatedOn")
SELECT gen_random_uuid(), "Id", '1', 'Daire 1', true, now() FROM "Buildings" LIMIT 1;

-- 1. aktif dimension (ValidTo = NULL) — BAŞARILI olmalı
WITH a AS (SELECT "Id" FROM "Apartments" LIMIT 1)
INSERT INTO "ApartmentDimensions" ("Id", "ApartmentId", "SquareMeters", "CommonAreaRatio", "ValidFrom", "ValidTo", "CreatedOn")
SELECT gen_random_uuid(), "Id", 120, 0.05, '2024-01-01T00:00:00Z', NULL, now() FROM a;

-- 2. dimension ÇAKIŞAN aralıkla (ValidTo = NULL) — EXCLUSION CONSTRAINT HATASI bekleniyor
WITH a AS (SELECT "Id" FROM "Apartments" LIMIT 1)
INSERT INTO "ApartmentDimensions" ("Id", "ApartmentId", "SquareMeters", "CommonAreaRatio", "ValidFrom", "ValidTo", "CreatedOn")
SELECT gen_random_uuid(), "Id", 125, 0.05, '2024-06-01T00:00:00Z', NULL, now() FROM a;

-- 3. ÇAKIŞMAYAN aralık (kayıt kapalı aralık) — BAŞARILI olmalı
WITH a AS (SELECT "Id" FROM "Apartments" LIMIT 1)
INSERT INTO "ApartmentDimensions" ("Id", "ApartmentId", "SquareMeters", "CommonAreaRatio", "ValidFrom", "ValidTo", "CreatedOn")
SELECT gen_random_uuid(), "Id", 125, 0.05, '2024-06-01T00:00:00Z', '2025-06-01T00:00:00Z', now() FROM a;

ROLLBACK;
