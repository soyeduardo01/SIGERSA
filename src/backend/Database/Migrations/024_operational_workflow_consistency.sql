BEGIN;

SET LOCAL TIME ZONE 'UTC';
SET LOCAL search_path = pg_catalog;

-- Estados visibles de extremo a extremo para solicitudes y evaluaciones.
ALTER TABLE "SIGERSA"."SOLICITUD" DROP CONSTRAINT IF EXISTS "CK_SOLICITUD_ESTADO";
ALTER TABLE "SIGERSA"."SOLICITUD"
    ADD CONSTRAINT "CK_SOLICITUD_ESTADO" CHECK (estado IN (
        'BORRADOR', 'PENDIENTE_ASIGNACION', 'ASIGNADA', 'EN_PROCESO',
        'RESUELTA', 'CANCELADA', 'RECHAZADA'
    ));

INSERT INTO "SIGERSA"."ParametersControl"
    ("KeyWord", "NumericData", "StringData", "Status", "CUser")
SELECT 'ESTADO_SOLICITUD', value."NumericData", value."StringData", true, 'MIGRATION_024'
FROM (VALUES
    (1, 'BORRADOR'),
    (2, 'PENDIENTE_ASIGNACION'),
    (3, 'ASIGNADA'),
    (4, 'EN_PROCESO'),
    (5, 'RESUELTA'),
    (6, 'CANCELADA'),
    (7, 'RECHAZADA')
) AS value("NumericData", "StringData")
WHERE NOT EXISTS (
    SELECT 1 FROM "SIGERSA"."ParametersControl" parameter
     WHERE parameter."KeyWord" = 'ESTADO_SOLICITUD'
       AND parameter."StringData" = value."StringData"
       AND parameter."Status" = true
);

INSERT INTO "SIGERSA"."ParametersControl"
    ("KeyWord", "NumericData", "StringData", "Status", "CUser")
SELECT 'ESTADO_EVALUACION', 9, 'NO_APROBADA', true, 'MIGRATION_024'
WHERE NOT EXISTS (
    SELECT 1 FROM "SIGERSA"."ParametersControl"
     WHERE "KeyWord" = 'ESTADO_EVALUACION'
       AND "StringData" = 'NO_APROBADA'
       AND "Status" = true
);

UPDATE "SIGERSA"."ParametersControl"
   SET "NumericData" = CASE "StringData"
       WHEN 'NO_APROBADA' THEN 9
       WHEN 'CERRADA' THEN 10
       WHEN 'CANCELADA' THEN 11
   END,
       "MUser" = 'MIGRATION_024',
       "MDate" = CURRENT_TIMESTAMP
 WHERE "KeyWord" = 'ESTADO_EVALUACION'
   AND "Status" = true
   AND "StringData" IN ('NO_APROBADA', 'CERRADA', 'CANCELADA');

-- Una respuesta evaluable solo puede producir un hallazgo vigente por ítem.
CREATE UNIQUE INDEX IF NOT EXISTS "UQ_NC_EVALUACION_ITEM"
    ON "SIGERSA"."NO_CONFORMIDAD" (evaluacion_id, item_ficha_id);

-- Recupera solicitudes y hallazgos creados antes de este flujo automático.
UPDATE "SIGERSA"."SOLICITUD" request
   SET estado = CASE
       WHEN inspection_case.estado = 'CERRADO' THEN 'RESUELTA'
       WHEN EXISTS (
           SELECT 1 FROM "SIGERSA"."EVALUACION" evaluation
            WHERE evaluation.caso_id = inspection_case.id
              AND evaluation.estado NOT IN ('ASIGNADA', 'CANCELADA')
       ) THEN 'EN_PROCESO'
       ELSE 'ASIGNADA'
   END,
       modificado_en = CURRENT_TIMESTAMP,
       modificado_por = COALESCE(inspection_case.modificado_por, inspection_case.creado_por),
       version_fila = request.version_fila + 1
  FROM "SIGERSA"."CASO" inspection_case
 WHERE inspection_case.solicitud_id = request.id
   AND request.estado = 'PENDIENTE_ASIGNACION';

INSERT INTO "SIGERSA"."NO_CONFORMIDAD"
    (id, evaluacion_id, item_ficha_id, respuesta_usuario_id,
     nivel_criticidad_id, codigo, descripcion, estado, detectada_en,
     creado_por, modificado_por)
SELECT gen_random_uuid(), response.evaluacion_id, response.item_ficha_id, response.id,
       response.nivel_criticidad_id,
       'NC-' || upper(substr(replace(response.id::text, '-', ''), 1, 10)),
       COALESCE(NULLIF(response.observacion, ''), NULLIF(response.comentario, ''), item.titulo),
       'ABIERTA', response.respondido_en, response.respondido_por, response.respondido_por
  FROM "SIGERSA"."RESPUESTA_USUARIO" response
  JOIN "SIGERSA"."ITEM_FICHA" item ON item.id = response.item_ficha_id
 WHERE response.valor_texto = 'NO_CUMPLE'
   AND response.nivel_criticidad_id IS NOT NULL
   AND NOT EXISTS (
       SELECT 1 FROM "SIGERSA"."NO_CONFORMIDAD" finding
        WHERE finding.evaluacion_id = response.evaluacion_id
          AND finding.item_ficha_id = response.item_ficha_id
   );

INSERT INTO "SIGERSA"."EVIDENCIA_NO_CONFORMIDAD"
    (evidencia_id, no_conformidad_id, creado_por)
SELECT evidence_response.evidencia_id, finding.id, finding.creado_por
  FROM "SIGERSA"."NO_CONFORMIDAD" finding
  JOIN "SIGERSA"."EVIDENCIA_RESPUESTA" evidence_response
    ON evidence_response.respuesta_usuario_id = finding.respuesta_usuario_id
ON CONFLICT (evidencia_id, no_conformidad_id) DO NOTHING;

COMMIT;
