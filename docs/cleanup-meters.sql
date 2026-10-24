BEGIN;

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
DELETE FROM "MeterReadings" WHERE "MeterId" IN (SELECT "Id" FROM to_delete);

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
DELETE FROM "Meters" WHERE "Id" IN (SELECT "Id" FROM to_delete);

COMMIT;

SELECT COUNT(*) AS kalan_sayac FROM "Meters";
SELECT COUNT(*) AS kalan_okuma FROM "MeterReadings";
SELECT COUNT(*) AS daire_basina_sayac_kontrol FROM (SELECT "ApartmentId", COUNT(*) AS c FROM "Meters" WHERE "ApartmentId" IS NOT NULL GROUP BY "ApartmentId" HAVING COUNT(*) <> 1) x;
