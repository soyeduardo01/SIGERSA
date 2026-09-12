BEGIN;

SET LOCAL TIME ZONE 'UTC';
SET LOCAL search_path = pg_catalog;

INSERT INTO "SIGERSA"."ParametersControl"
    ("KeyWord", "NumericData", "CCode", "StringData", "Status", "CUser")
SELECT 'TIPO_ESTABLECIMIENTO', value."NumericData", value."CCode", value."StringData", true, 'MIGRATION_018'
FROM (VALUES
    (1, 'PLANTA', 'Planta procesadora de alimentos'),
    (2, 'RESTAUR', 'Restaurante'),
    (3, 'SUPER', 'Supermercado'),
    (4, 'ALMACEN', 'Almacén o depósito'),
    (5, 'PANADERIA', 'Panadería'),
    (6, 'COMEDOR', 'Comedor'),
    (7, 'MERCADO', 'Mercado'),
    (8, 'DISTRIB', 'Distribuidora'),
    (9, 'IMPORT', 'Importadora'),
    (10, 'FARMACIA', 'Farmacia'),
    (11, 'OTRO', 'Otro')
) AS value("NumericData", "CCode", "StringData")
WHERE NOT EXISTS (
    SELECT 1
    FROM "SIGERSA"."ParametersControl" AS parameter
    WHERE parameter."KeyWord" = 'TIPO_ESTABLECIMIENTO'
      AND parameter."CCode" = value."CCode"
      AND parameter."Status" = true
);

COMMIT;
