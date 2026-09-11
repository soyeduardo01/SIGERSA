BEGIN;

SET LOCAL TIME ZONE 'UTC';
SET LOCAL search_path = pg_catalog;

-- Catálogos pequeños y estables administrados mediante ParametersControl.
-- NumericData conserva el orden y, para el riesgo del alimento, también los puntos.
INSERT INTO "SIGERSA"."ParametersControl"
    ("KeyWord", "NumericData", "StringData", "Status", "CUser")
SELECT value."KeyWord", value."NumericData", value."StringData", true, 'MIGRATION_007'
FROM (VALUES
    ('ESTADO_ESTABLECIMIENTO', 1, 'ACTIVO'),
    ('ESTADO_ESTABLECIMIENTO', 2, 'INACTIVO'),
    ('ESTADO_ESTABLECIMIENTO', 3, 'SUSPENDIDO'),
    ('NIVEL_RIESGO_ALIMENTO', 1, 'RIESGO_BAJO'),
    ('NIVEL_RIESGO_ALIMENTO', 2, 'RIESGO_MEDIO'),
    ('NIVEL_RIESGO_ALIMENTO', 3, 'RIESGO_ALTO')
) AS value("KeyWord", "NumericData", "StringData")
WHERE NOT EXISTS (
    SELECT 1
    FROM "SIGERSA"."ParametersControl" AS parameter
    WHERE parameter."KeyWord" = value."KeyWord"
      AND parameter."StringData" = value."StringData"
      AND parameter."Status" = true
);

COMMIT;
