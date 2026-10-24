INSERT INTO "MeterReadings" ("Id", "MeterId", "IndexValue", "ConsumptionKWh", "ReadingDateUtc", "Source", "IsProcessed", "IsInitialReading", "CreatedOn")
SELECT gen_random_uuid(), m."Id", 19600, 474, '2031-12-20 12:00:00+00', 2, false, false, now()
FROM "Meters" m WHERE m."SerialNumber" = '55238358';
SELECT COUNT(*) AS eklenen FROM "MeterReadings" WHERE "ReadingDateUtc" >= '2031-12-01' AND "ReadingDateUtc" < '2032-01-01';
