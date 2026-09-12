BEGIN;

SET LOCAL TIME ZONE 'UTC';
SET LOCAL search_path = pg_catalog;

INSERT INTO "SIGERSA"."MOTIVO_INSPECCION"
    (codigo, nombre, requiere_detalle, ficha_completa_default, orden, activo)
VALUES
    ('CERTIFICACION_BPM', 'Solicitud de Certificación de Buenas Prácticas de Manufactura (BPM)', false, true, 5, true)
ON CONFLICT (codigo) DO UPDATE
   SET nombre = EXCLUDED.nombre,
       ficha_completa_default = true,
       activo = true,
       modificado_en = CURRENT_TIMESTAMP;

COMMIT;
