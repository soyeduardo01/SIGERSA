BEGIN;

ALTER TABLE "SIGERSA"."EVIDENCIA"
    ADD COLUMN IF NOT EXISTS idempotency_key uuid;

-- El hash prueba integridad, pero un mismo archivo puede respaldar más de una
-- respuesta o evaluación; por eso no debe actuar como identidad global.
ALTER TABLE "SIGERSA"."EVIDENCIA"
    DROP CONSTRAINT IF EXISTS "UQ_EVIDENCIA_HASH";

CREATE INDEX IF NOT EXISTS "IX_EVIDENCIA_HASH"
    ON "SIGERSA"."EVIDENCIA" (hash);

CREATE UNIQUE INDEX IF NOT EXISTS "UQ_EVIDENCIA_IDEMPOTENCY_KEY"
    ON "SIGERSA"."EVIDENCIA" (idempotency_key)
    WHERE idempotency_key IS NOT NULL;

COMMENT ON COLUMN "SIGERSA"."EVIDENCIA".idempotency_key IS
    'Identificador estable de la operación offline; permite confirmar una carga sin duplicar metadatos.';

COMMIT;
