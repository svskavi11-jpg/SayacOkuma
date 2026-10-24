SELECT a."DoorNumber", d."SquareMeters", d."ValidFrom", d."ValidTo",
       CASE WHEN d."ValidTo" IS NULL THEN 'AKTIF' ELSE 'KAPALI' END AS durum
FROM "ApartmentDimensions" d
JOIN "Apartments" a ON a."Id" = d."ApartmentId"
WHERE a."DoorNumber" = '1'
ORDER BY d."ValidFrom";
