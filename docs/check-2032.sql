SELECT COUNT(*) AS toplam_sayac FROM "Meters";
SELECT COUNT(*) AS okuma_2032 FROM "MeterReadings" WHERE "ReadingDateUtc" >= '2032-01-01' AND "ReadingDateUtc" < '2032-02-01';
SELECT COUNT(*) AS daire_basina_1_olmayan FROM (SELECT "ApartmentId" FROM "Meters" WHERE "ApartmentId" IS NOT NULL GROUP BY "ApartmentId" HAVING COUNT(*) <> 1) x;
