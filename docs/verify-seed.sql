-- Etap 5.3: E2E test tekrarlarindan biriken sayac temizligi.
-- Her dairenin EN GUNCEL 1 sayaci tutulur; digerlerinin okumalari once silinir (FK Restrict), sonra sayaclar.

-- 0) Onizleme: kac kayit silinecek?
WITH ranked AS (
    SELECT m."Id",
           ROW_NUMBER() OVER (
               PARTITION BY m."ApartmentId"
               ORDER BY m."CreatedOn" DESC, m."Id" DESC) AS rn
    FROM "Meters" m
    WHERE m."ApartmentId" IS NOT NULL
),
to_delete AS (
    SELECT "Id" FROM ranked WHERE rn > 1
)
SELECT
    (SELECT COUNT(*) FROM to_delete) AS silinecek_sayac,
    (SELECT COUNT(*) FROM "MeterReadings" WHERE "MeterId" IN (SELECT "Id" FROM to_delete)) AS silinecek_okuma;
