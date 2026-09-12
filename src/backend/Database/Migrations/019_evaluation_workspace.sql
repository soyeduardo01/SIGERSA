BEGIN;

SET LOCAL TIME ZONE 'UTC';
SET LOCAL search_path = pg_catalog;

UPDATE "SIGERSA"."NIVEL_CRITICIDAD"
   SET nombre = CASE codigo
           WHEN 'C' THEN 'No Conformidad Crítica'
           WHEN 'M' THEN 'No Conformidad Mayor'
           ELSE 'No Conformidad Menor'
       END,
       descripcion = CASE codigo
           WHEN 'C' THEN 'Riesgo crítico que requiere acción inmediata.'
           WHEN 'M' THEN 'Incumplimiento mayor que requiere corrección prioritaria.'
           ELSE 'Incumplimiento menor sujeto a seguimiento.'
       END,
       plazo_maximo_dias = CASE codigo WHEN 'C' THEN 0 WHEN 'M' THEN 15 ELSE 30 END,
       requiere_accion_inmediata = codigo = 'C',
       activo = true
 WHERE codigo IN ('C', 'M', 'ME');

INSERT INTO "SIGERSA"."NIVEL_CRITICIDAD"
    (id, codigo, nombre, descripcion, prioridad, plazo_maximo_dias,
     requiere_accion_inmediata, vigente_desde, activo)
SELECT gen_random_uuid(), 'C', 'No Conformidad Crítica',
       'Riesgo crítico que requiere acción inmediata.',
       (SELECT COALESCE(MAX(prioridad), 0) + 1 FROM "SIGERSA"."NIVEL_CRITICIDAD"),
       0, true, CURRENT_TIMESTAMP, true
 WHERE NOT EXISTS (SELECT 1 FROM "SIGERSA"."NIVEL_CRITICIDAD" WHERE codigo = 'C');

INSERT INTO "SIGERSA"."NIVEL_CRITICIDAD"
    (id, codigo, nombre, descripcion, prioridad, plazo_maximo_dias,
     requiere_accion_inmediata, vigente_desde, activo)
SELECT gen_random_uuid(), 'M', 'No Conformidad Mayor',
       'Incumplimiento mayor que requiere corrección prioritaria.',
       (SELECT COALESCE(MAX(prioridad), 0) + 1 FROM "SIGERSA"."NIVEL_CRITICIDAD"),
       15, false, CURRENT_TIMESTAMP, true
 WHERE NOT EXISTS (SELECT 1 FROM "SIGERSA"."NIVEL_CRITICIDAD" WHERE codigo = 'M');

INSERT INTO "SIGERSA"."NIVEL_CRITICIDAD"
    (id, codigo, nombre, descripcion, prioridad, plazo_maximo_dias,
     requiere_accion_inmediata, vigente_desde, activo)
SELECT gen_random_uuid(), 'ME', 'No Conformidad Menor',
       'Incumplimiento menor sujeto a seguimiento.',
       (SELECT COALESCE(MAX(prioridad), 0) + 1 FROM "SIGERSA"."NIVEL_CRITICIDAD"),
       30, false, CURRENT_TIMESTAMP, true
 WHERE NOT EXISTS (SELECT 1 FROM "SIGERSA"."NIVEL_CRITICIDAD" WHERE codigo = 'ME');

ALTER TABLE "SIGERSA"."USUARIO"
    ADD COLUMN IF NOT EXISTS supabase_auth_user_id uuid,
    ADD COLUMN IF NOT EXISTS mfa_habilitado boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS mfa_factor_id uuid;

CREATE UNIQUE INDEX IF NOT EXISTS "UQ_USUARIO_SUPABASE_AUTH"
    ON "SIGERSA"."USUARIO" (supabase_auth_user_id)
    WHERE supabase_auth_user_id IS NOT NULL;

DO $constraint$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'CK_USUARIO_MFA_CONFIGURADO'
           AND conrelid = '"SIGERSA"."USUARIO"'::regclass
    ) THEN
        ALTER TABLE "SIGERSA"."USUARIO"
            ADD CONSTRAINT "CK_USUARIO_MFA_CONFIGURADO"
            CHECK (mfa_habilitado = false OR
                   (supabase_auth_user_id IS NOT NULL AND mfa_factor_id IS NOT NULL));
    END IF;
END
$constraint$;

CREATE TABLE IF NOT EXISTS "SIGERSA"."EVALUACION_COMPLEMENTO" (
    evaluacion_id uuid PRIMARY KEY,
    fecha_ultima_inspeccion date,
    calificacion_ultima_inspeccion varchar(120),
    fecha_inspeccion_actual date,
    calificacion_inspeccion_actual varchar(120),
    oficial_dps_das_1 varchar(200),
    oficial_dps_das_2 varchar(200),
    tecnico_digemaps_1 varchar(200),
    tecnico_digemaps_2 varchar(200),
    medidas_correctivas jsonb NOT NULL DEFAULT '[]'::jsonb,
    recomendaciones jsonb NOT NULL DEFAULT '[]'::jsonb,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_EVALUACION_COMPLEMENTO_EVALUACION" FOREIGN KEY (evaluacion_id)
        REFERENCES "SIGERSA"."EVALUACION" (id) ON DELETE CASCADE,
    CONSTRAINT "FK_EVALUACION_COMPLEMENTO_CREADO_POR" FOREIGN KEY (creado_por)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED,
    CONSTRAINT "FK_EVALUACION_COMPLEMENTO_MODIFICADO_POR" FOREIGN KEY (modificado_por)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED,
    CONSTRAINT "CK_EVALUACION_COMPLEMENTO_MEDIDAS" CHECK (jsonb_typeof(medidas_correctivas) = 'array'),
    CONSTRAINT "CK_EVALUACION_COMPLEMENTO_RECOMENDACIONES" CHECK (jsonb_typeof(recomendaciones) = 'array'),
    CONSTRAINT "CK_EVALUACION_COMPLEMENTO_VERSION" CHECK (version_fila > 0)
);

DO $trigger$
BEGIN
    IF NOT EXISTS (
        SELECT 1
          FROM pg_trigger
         WHERE tgname = 'TRG_EVALUACION_COMPLEMENTO_VERSION_FILA'
           AND tgrelid = '"SIGERSA"."EVALUACION_COMPLEMENTO"'::regclass
    ) THEN
        CREATE TRIGGER "TRG_EVALUACION_COMPLEMENTO_VERSION_FILA"
        BEFORE UPDATE ON "SIGERSA"."EVALUACION_COMPLEMENTO"
        FOR EACH ROW EXECUTE FUNCTION "SIGERSA"."FN_INCREMENTAR_VERSION_FILA"();
    END IF;
END
$trigger$;

COMMIT;
