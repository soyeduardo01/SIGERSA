BEGIN;

SET LOCAL TIME ZONE 'UTC';
SET LOCAL search_path = pg_catalog;

-- CAT-15 es una categoría terminal: se representa con una subcategoría del mismo nombre.
INSERT INTO "SIGERSA"."SUBCATEGORIA_ALIMENTO"
    (categoria_alimento_id, codigo, nombre, nivel_riesgo, orden, fuente, activo)
SELECT category.id, 'SUB-102', 'Alimentos preparados', 1, 102,
       'Corrección funcional: categoría terminal Alimentos preparados', true
  FROM "SIGERSA"."CATEGORIA_ALIMENTO" category
 WHERE category.codigo = 'CAT-15'
ON CONFLICT (categoria_alimento_id, codigo) DO UPDATE
   SET nombre = EXCLUDED.nombre,
       nivel_riesgo = EXCLUDED.nivel_riesgo,
       orden = EXCLUDED.orden,
       activo = true,
       modificado_en = CURRENT_TIMESTAMP;

-- Unifica el catálogo visible y los estados persistidos del seguimiento de hallazgos.
ALTER TABLE "SIGERSA"."NO_CONFORMIDAD" DROP CONSTRAINT IF EXISTS "CK_NC_ESTADO";

UPDATE "SIGERSA"."NO_CONFORMIDAD"
   SET estado = CASE estado
       WHEN 'CORREGIDA' THEN 'VALIDADO'
       WHEN 'VERIFICADA' THEN 'VALIDADO'
       WHEN 'CERRADA' THEN 'CERRADO'
       ELSE estado
   END,
       modificado_en = CURRENT_TIMESTAMP,
       version_fila = version_fila + 1
 WHERE estado IN ('CORREGIDA', 'VERIFICADA', 'CERRADA');

ALTER TABLE "SIGERSA"."NO_CONFORMIDAD"
    ADD CONSTRAINT "CK_NC_ESTADO"
    CHECK (estado IN ('ABIERTA', 'EN_CORRECCION', 'VALIDADO', 'CERRADO'));

UPDATE "SIGERSA"."ParametersControl"
   SET "StringData" = 'ABIERTA', "MUser" = 'MIGRATION_026', "MDate" = CURRENT_TIMESTAMP
 WHERE "KeyWord" = 'ESTADO_HALLAZGO' AND "StringData" = 'ABIERTO'
   AND NOT EXISTS (
       SELECT 1 FROM "SIGERSA"."ParametersControl" existing
        WHERE existing."KeyWord" = 'ESTADO_HALLAZGO'
          AND existing."StringData" = 'ABIERTA');

DELETE FROM "SIGERSA"."ParametersControl"
 WHERE "KeyWord" = 'ESTADO_HALLAZGO' AND "StringData" = 'ABIERTO';

COMMIT;
