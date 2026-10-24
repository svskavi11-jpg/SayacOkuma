SELECT a."DoorNumber", d."SquareMeters", d."ValidFrom", d."ValidTo",
       CASE WHEN d."ValidTo" IS NULL THEN 'AKTIF' ELSE 'KAPALI' END AS durum
FROM "ApartmentDimensions" d
JOIN "Apartments" a ON a."Id" = d."ApartmentId"
WHERE d."SquareMeters" = 123.45
ORDER BY a."DoorNumber", d."ValidFrom";
