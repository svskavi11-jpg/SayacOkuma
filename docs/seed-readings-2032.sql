-- Etap 5.4 canl? test verisi: 2032 Ocak d?nemi i?in her daireye 850 kWh t?ketim okumas?.
INSERT INTO "MeterReadings" ("Id", "MeterId", "IndexValue", "ConsumptionKWh", "ReadingDateUtc", "Source", "IsProcessed", "CreatedOn")
SELECT gen_random_uuid(), m."Id",
       COALESCE(r."IndexValue", 0) + 850,
       850,
       '2032-01-20 12:00:00+00', 1, false, now()
FROM "Meters" m
LEFT JOIN LATERAL (
    SELECT "IndexValue" FROM "MeterReadings" rr
    WHERE rr."MeterId" = m."Id"
    ORDER BY "ReadingDateUtc" DESC LIMIT 1
) r ON true
WHERE m."ApartmentId" IS NOT NULL;

SELECT COUNT(*) AS eklenen_okuma FROM "MeterReadings" WHERE "ReadingDateUtc" >= '2032-01-01' AND "ReadingDateUtc" < '2032-02-01';
