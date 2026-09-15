BEGIN;

SET LOCAL TIME ZONE 'UTC';
SET LOCAL search_path = pg_catalog;

ALTER TABLE "SIGERSA"."NOTIFICACION"
    ADD COLUMN IF NOT EXISTS programada_para timestamptz;

UPDATE "SIGERSA"."NOTIFICACION"
   SET programada_para = creado_en
 WHERE programada_para IS NULL;

ALTER TABLE "SIGERSA"."NOTIFICACION"
    ALTER COLUMN programada_para SET DEFAULT CURRENT_TIMESTAMP,
    ALTER COLUMN programada_para SET NOT NULL;

CREATE INDEX IF NOT EXISTS "IX_NOTIFICACION_USUARIO_PROGRAMADA"
    ON "SIGERSA"."NOTIFICACION" (usuario_id, programada_para DESC)
    WHERE canal = 'INTERNA' AND leida_en IS NULL;

CREATE UNIQUE INDEX IF NOT EXISTS "UQ_NOTIFICACION_RECORDATORIO_ACTIVO"
    ON "SIGERSA"."NOTIFICACION" (usuario_id, tipo, recurso_tipo, recurso_id, programada_para)
    WHERE canal = 'INTERNA' AND leida_en IS NULL;

UPDATE "SIGERSA"."MOTIVO_INSPECCION"
   SET codigo = 'INSPECCION_CONTROL',
       nombre = 'Inspección de control',
       requiere_detalle = false,
       ficha_completa_default = false,
       orden = 5,
       activo = true,
       modificado_en = CURRENT_TIMESTAMP,
       version_fila = version_fila + 1
 WHERE codigo = 'VIGILANCIA_CONTROL_RUTINA'
   AND NOT EXISTS (
       SELECT 1 FROM "SIGERSA"."MOTIVO_INSPECCION" existing
        WHERE existing.codigo = 'INSPECCION_CONTROL');

INSERT INTO "SIGERSA"."MOTIVO_INSPECCION"
    (codigo, nombre, requiere_detalle, ficha_completa_default, orden, activo)
VALUES
    ('SOLICITUD_PERMISO_SANITARIO', 'Solicitud de Permiso Sanitario', false, true, 1, true),
    ('RENOVACION_PERMISO_SANITARIO', 'Renovación de Permiso Sanitario', false, true, 2, true),
    ('CERTIFICACION_BPM', 'Solicitud de Certificación BPM', false, true, 3, true),
    ('INSPECCION_PROGRAMADA', 'Inspección programada', false, false, 4, true),
    ('INSPECCION_CONTROL', 'Inspección de control', false, false, 5, true),
    ('INVESTIGACION_DENUNCIA', 'Investigación por denuncia', true, false, 6, true)
ON CONFLICT (codigo) DO UPDATE
   SET nombre = EXCLUDED.nombre,
       requiere_detalle = EXCLUDED.requiere_detalle,
       ficha_completa_default = EXCLUDED.ficha_completa_default,
       orden = EXCLUDED.orden,
       activo = true,
       modificado_en = CURRENT_TIMESTAMP,
       version_fila = "SIGERSA"."MOTIVO_INSPECCION".version_fila + 1;

UPDATE "SIGERSA"."MOTIVO_INSPECCION"
   SET activo = false,
       modificado_en = CURRENT_TIMESTAMP,
       version_fila = version_fila + 1
 WHERE codigo NOT IN (
       'SOLICITUD_PERMISO_SANITARIO',
       'RENOVACION_PERMISO_SANITARIO',
       'CERTIFICACION_BPM',
       'INSPECCION_PROGRAMADA',
       'INSPECCION_CONTROL',
       'INVESTIGACION_DENUNCIA');

COMMIT;
