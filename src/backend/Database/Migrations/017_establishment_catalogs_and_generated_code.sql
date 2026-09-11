BEGIN;

SET LOCAL TIME ZONE 'UTC';
SET LOCAL search_path = pg_catalog;

INSERT INTO "SIGERSA"."COMERCIALIZACION" (codigo, nombre, orden, activo)
VALUES
    ('LOCAL', 'Local', 1, true),
    ('NACIONAL', 'Nacional', 2, true),
    ('INTERNACIONAL', 'Internacional', 3, true),
    ('TODOS', 'Todos los mercados', 4, true)
ON CONFLICT (codigo) DO UPDATE
SET nombre = EXCLUDED.nombre,
    orden = EXCLUDED.orden,
    activo = true,
    modificado_en = CURRENT_TIMESTAMP,
    version_fila = "SIGERSA"."COMERCIALIZACION".version_fila + 1;

UPDATE "SIGERSA"."MERCADO_OBJETIVO"
SET activo = false,
    modificado_en = CURRENT_TIMESTAMP,
    version_fila = version_fila + 1
WHERE codigo NOT IN ('INFANTIL', 'NINOS_MENORES', 'ADULTOS', 'MUJERES_EMBARAZADAS', 'ADULTOS_MAYORES', 'TODOS_SEGMENTOS')
  AND activo = true;

INSERT INTO "SIGERSA"."MERCADO_OBJETIVO" (codigo, nombre, orden, activo)
VALUES
    ('INFANTIL', 'Infantil', 1, true),
    ('NINOS_MENORES', 'Niños menores', 2, true),
    ('ADULTOS', 'Adultos', 3, true),
    ('MUJERES_EMBARAZADAS', 'Mujeres embarazadas', 4, true),
    ('ADULTOS_MAYORES', 'Adultos mayores', 5, true),
    ('TODOS_SEGMENTOS', 'Todos los segmentos', 6, true)
ON CONFLICT (codigo) DO UPDATE
SET nombre = EXCLUDED.nombre,
    orden = EXCLUDED.orden,
    activo = true,
    modificado_en = CURRENT_TIMESTAMP,
    version_fila = "SIGERSA"."MERCADO_OBJETIVO".version_fila + 1;

UPDATE "SIGERSA"."ParametersControl"
SET "Status" = false,
    "DUser" = 'MIGRATION_017',
    "DDate" = CURRENT_TIMESTAMP,
    "MUser" = 'MIGRATION_017',
    "MDate" = CURRENT_TIMESTAMP
WHERE "KeyWord" IN (
    'COMERCIALIZACION_ESTABLECIMIENTO',
    'MERCADO_OBJETIVO',
    'NIVEL_IMPLEMENTACION_HACCP',
    'APLICACION_MUESTREO_MICROBIOLOGICO',
    'DISTRIBUCION_INABIE'
)
AND "Status" = true
AND ("CUser" IS NULL OR "CUser" <> 'MIGRATION_017');

INSERT INTO "SIGERSA"."ParametersControl"
    ("KeyWord", "NumericData", "CCode", "StringData", "Status", "CUser")
SELECT value."KeyWord", value."NumericData", value."CCode", value."StringData", true, 'MIGRATION_017'
FROM (VALUES
    ('COMERCIALIZACION_ESTABLECIMIENTO', 1, 'LOCAL', 'Local'),
    ('COMERCIALIZACION_ESTABLECIMIENTO', 2, 'NACIONAL', 'Nacional'),
    ('COMERCIALIZACION_ESTABLECIMIENTO', 3, 'INTERNAC', 'Internacional'),
    ('COMERCIALIZACION_ESTABLECIMIENTO', 4, 'TODOS', 'Todos los mercados'),
    ('MERCADO_OBJETIVO', 1, 'INFANTIL', 'Infantil'),
    ('MERCADO_OBJETIVO', 2, 'NINOS', 'Niños menores'),
    ('MERCADO_OBJETIVO', 3, 'ADULTOS', 'Adultos'),
    ('MERCADO_OBJETIVO', 4, 'EMBARAZ', 'Mujeres embarazadas'),
    ('MERCADO_OBJETIVO', 5, 'MAYORES', 'Adultos mayores'),
    ('MERCADO_OBJETIVO', 6, 'TODOS', 'Todos los segmentos'),
    ('NIVEL_IMPLEMENTACION_HACCP', 25, 'H25', 'En el 25% de las líneas de producción'),
    ('NIVEL_IMPLEMENTACION_HACCP', 75, 'H75', 'En el 75% de las líneas de producción'),
    ('NIVEL_IMPLEMENTACION_HACCP', 100, 'H100', 'En todas las líneas de producción'),
    ('APLICACION_MUESTREO_MICROBIOLOGICO', 1, 'MP', 'Solo para las materias primas'),
    ('APLICACION_MUESTREO_MICROBIOLOGICO', 2, 'AP_PT', 'Solo para las áreas de proceso y productos terminados'),
    ('APLICACION_MUESTREO_MICROBIOLOGICO', 3, 'MP_AP_PT', 'Para las materias primas, las áreas de proceso y productos terminados'),
    ('DISTRIBUCION_INABIE', 1, 'NACIONAL', 'A nivel nacional'),
    ('DISTRIBUCION_INABIE', 2, 'REGIONAL', 'A nivel regional'),
    ('DISTRIBUCION_INABIE', 3, 'LOCAL', 'A nivel local')
) AS value("KeyWord", "NumericData", "CCode", "StringData")
WHERE NOT EXISTS (
    SELECT 1
    FROM "SIGERSA"."ParametersControl" AS parameter
    WHERE parameter."KeyWord" = value."KeyWord"
      AND parameter."CCode" = value."CCode"
      AND parameter."Status" = true
);

CREATE SEQUENCE IF NOT EXISTS "SIGERSA"."SEQ_ESTABLECIMIENTO_CODIGO";

SELECT setval(
    '"SIGERSA"."SEQ_ESTABLECIMIENTO_CODIGO"'::regclass,
    GREATEST(
        COALESCE((
            SELECT MAX(substring(codigo FROM '^FO-DDA-([0-9]+)$')::bigint)
            FROM "SIGERSA"."ESTABLECIMIENTO"
        ), 0) + 1,
        1
    ),
    false
);

ALTER TABLE "SIGERSA"."ESTABLECIMIENTO"
    ALTER COLUMN codigo SET DEFAULT (
        'FO-DDA-' || lpad(nextval('"SIGERSA"."SEQ_ESTABLECIMIENTO_CODIGO"'::regclass)::text, 2, '0')
    );

COMMIT;
