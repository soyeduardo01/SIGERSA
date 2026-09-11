BEGIN;

SET LOCAL search_path = pg_catalog;

ALTER TABLE "SIGERSA"."ESTABLECIMIENTO"
    ADD COLUMN IF NOT EXISTS rechazos_microbiologicos_ultimos_5_anios integer NOT NULL DEFAULT 0;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'CK_ESTABLECIMIENTO_RECHAZOS_MICROBIOLOGICOS'
          AND conrelid = '"SIGERSA"."ESTABLECIMIENTO"'::regclass
    ) THEN
        ALTER TABLE "SIGERSA"."ESTABLECIMIENTO"
            ADD CONSTRAINT "CK_ESTABLECIMIENTO_RECHAZOS_MICROBIOLOGICOS"
            CHECK (rechazos_microbiologicos_ultimos_5_anios >= 0);
    END IF;
END $$;

COMMIT;
