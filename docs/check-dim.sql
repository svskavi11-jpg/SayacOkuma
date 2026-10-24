SELECT a."DoorNumber", d."Id", d."SquareMeters", d."ValidFrom", d."ValidTo"
FROM "ApartmentDimensions" d
JOIN "Apartments" a ON a."Id" = d."ApartmentId"
WHERE d."Id" = '385b7d3b-5db1-4fc7-ae8f-3ce1b283242b';

SELECT a."DoorNumber", d."Id", d."SquareMeters", d."ValidFrom", d."ValidTo",
       CASE WHEN d."ValidTo" IS NULL THEN 'AKTIF' ELSE 'KAPALI' END AS durum
FROM "ApartmentDimensions" d
JOIN "Apartments" a ON a."Id" = d."ApartmentId"
ORDER BY a."DoorNumber"::int, d."ValidFrom";
