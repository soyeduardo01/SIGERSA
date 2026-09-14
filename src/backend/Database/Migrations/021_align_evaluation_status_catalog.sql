BEGIN;

SET LOCAL TIME ZONE 'UTC';
SET LOCAL search_path = pg_catalog;

-- PENDIENTE_REVISION fue sustituido por los estados explícitos del flujo operativo.
UPDATE "SIGERSA"."ParametersControl"
   SET "Status" = false,
       "MUser" = 'MIGRATION_021',
       "MDate" = CURRENT_TIMESTAMP,
       "DUser" = 'MIGRATION_021',
       "DDate" = CURRENT_TIMESTAMP
 WHERE "KeyWord" = 'ESTADO_EVALUACION'
   AND "StringData" = 'PENDIENTE_REVISION'
   AND "Status" = true;

INSERT INTO "SIGERSA"."ParametersControl"
    ("KeyWord", "NumericData", "StringData", "Status", "CUser")
SELECT 'ESTADO_EVALUACION', value."NumericData", value."StringData", true, 'MIGRATION_021'
FROM (VALUES
    (4, 'FINALIZADA'),
    (5, 'ENVIADA'),
    (6, 'EN_REVISION')
) AS value("NumericData", "StringData")
WHERE NOT EXISTS (
    SELECT 1
      FROM "SIGERSA"."ParametersControl" parameter
     WHERE parameter."KeyWord" = 'ESTADO_EVALUACION'
       AND parameter."StringData" = value."StringData"
       AND parameter."Status" = true
);

UPDATE "SIGERSA"."ParametersControl"
   SET "NumericData" = CASE "StringData"
           WHEN 'ASIGNADA' THEN 1
           WHEN 'EN_EJECUCION' THEN 2
           WHEN 'PAUSADA' THEN 3
           WHEN 'FINALIZADA' THEN 4
           WHEN 'ENVIADA' THEN 5
           WHEN 'EN_REVISION' THEN 6
           WHEN 'EN_CORRECCION' THEN 7
           WHEN 'APROBADA' THEN 8
           WHEN 'CERRADA' THEN 9
           WHEN 'CANCELADA' THEN 10
       END,
       "MUser" = 'MIGRATION_021',
       "MDate" = CURRENT_TIMESTAMP
 WHERE "KeyWord" = 'ESTADO_EVALUACION'
   AND "Status" = true
   AND "StringData" IN (
       'ASIGNADA', 'EN_EJECUCION', 'PAUSADA', 'FINALIZADA', 'ENVIADA',
       'EN_REVISION', 'EN_CORRECCION', 'APROBADA', 'CERRADA', 'CANCELADA'
   );

COMMIT;
