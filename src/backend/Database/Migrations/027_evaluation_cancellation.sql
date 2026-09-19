-- Cancelación motivada de inspecciones por coordinadores.
ALTER TABLE "SIGERSA"."EVALUACION"
    ADD COLUMN IF NOT EXISTS cancelada_en timestamptz,
    ADD COLUMN IF NOT EXISTS motivo_cancelacion varchar(2000);

UPDATE "SIGERSA"."EVALUACION"
   SET cancelada_en = COALESCE(cancelada_en, modificado_en, CURRENT_TIMESTAMP),
       motivo_cancelacion = COALESCE(NULLIF(btrim(motivo_cancelacion), ''),
                                    'Cancelación registrada antes de habilitar el motivo obligatorio')
 WHERE estado = 'CANCELADA';

ALTER TABLE "SIGERSA"."EVALUACION"
    DROP CONSTRAINT IF EXISTS "CK_EVALUACION_CANCELACION";

ALTER TABLE "SIGERSA"."EVALUACION"
    ADD CONSTRAINT "CK_EVALUACION_CANCELACION" CHECK (
        (estado = 'CANCELADA' AND cancelada_en IS NOT NULL
                              AND NULLIF(btrim(motivo_cancelacion), '') IS NOT NULL)
        OR
        (estado <> 'CANCELADA' AND cancelada_en IS NULL
                               AND motivo_cancelacion IS NULL)
    );
