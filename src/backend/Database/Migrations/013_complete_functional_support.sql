BEGIN;

ALTER TABLE "SIGERSA"."EMPRESA"
    ADD COLUMN IF NOT EXISTS direccion text,
    ADD COLUMN IF NOT EXISTS municipio_id uuid;

DO $migration$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'FK_EMPRESA_MUNICIPIO'
           AND conrelid = '"SIGERSA"."EMPRESA"'::regclass
    ) THEN
        ALTER TABLE "SIGERSA"."EMPRESA"
            ADD CONSTRAINT "FK_EMPRESA_MUNICIPIO" FOREIGN KEY (municipio_id)
            REFERENCES "SIGERSA"."MUNICIPIO" (id) ON DELETE RESTRICT;
    END IF;
END
$migration$;

ALTER TABLE "SIGERSA"."RESPUESTA_USUARIO"
    ADD COLUMN IF NOT EXISTS comentario text;

INSERT INTO "SIGERSA"."ParametersControl"
    ("KeyWord", "NumericData", "StringData", "Status", "CUser")
SELECT value."KeyWord", value."NumericData", value."StringData", true, 'MIGRATION_013'
FROM (VALUES
    ('ESTADO_USUARIO_GESTION', 3::numeric, 'PENDIENTE_VALIDACION'),
    ('ESTADO_USUARIO_GESTION', 4::numeric, 'RECHAZADO')
) AS value("KeyWord", "NumericData", "StringData")
WHERE NOT EXISTS (
    SELECT 1 FROM "SIGERSA"."ParametersControl" existing
     WHERE existing."KeyWord" = value."KeyWord"
       AND existing."StringData" = value."StringData"
);

CREATE TABLE IF NOT EXISTS "SIGERSA"."USUARIO_DOCUMENTO_AUTORIZACION" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    usuario_id uuid NOT NULL,
    bucket_name varchar(100) NOT NULL,
    supabase_path varchar(1000) NOT NULL,
    nombre_original varchar(255) NOT NULL,
    file_size bigint NOT NULL,
    mime_type varchar(150) NOT NULL,
    hash varchar(128) NOT NULL,
    vigente boolean NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_USUARIO_DOC_AUT_USUARIO" FOREIGN KEY (usuario_id)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_USUARIO_DOC_AUT_CREADO_POR" FOREIGN KEY (creado_por)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED,
    CONSTRAINT "FK_USUARIO_DOC_AUT_MODIFICADO_POR" FOREIGN KEY (modificado_por)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED,
    CONSTRAINT "UQ_USUARIO_DOC_AUT_RUTA" UNIQUE (bucket_name, supabase_path),
    CONSTRAINT "CK_USUARIO_DOC_AUT_ARCHIVO" CHECK (file_size > 0 AND bucket_name <> '' AND supabase_path <> ''),
    CONSTRAINT "CK_USUARIO_DOC_AUT_VERSION" CHECK (version_fila > 0)
);

CREATE UNIQUE INDEX IF NOT EXISTS "UQ_USUARIO_DOC_AUT_VIGENTE"
    ON "SIGERSA"."USUARIO_DOCUMENTO_AUTORIZACION" (usuario_id)
    WHERE vigente = true;

CREATE TABLE IF NOT EXISTS "SIGERSA"."SOLICITUD_DOCUMENTO" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    solicitud_id uuid NOT NULL,
    tipo_documento varchar(80) NOT NULL,
    bucket_name varchar(100) NOT NULL,
    supabase_path varchar(1000) NOT NULL,
    nombre_original varchar(255) NOT NULL,
    file_size bigint NOT NULL,
    mime_type varchar(150) NOT NULL,
    hash varchar(128) NOT NULL,
    obligatorio boolean NOT NULL DEFAULT true,
    activo boolean NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_SOLICITUD_DOCUMENTO_SOLICITUD" FOREIGN KEY (solicitud_id)
        REFERENCES "SIGERSA"."SOLICITUD" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_SOLICITUD_DOCUMENTO_CREADO_POR" FOREIGN KEY (creado_por)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED,
    CONSTRAINT "FK_SOLICITUD_DOCUMENTO_MODIFICADO_POR" FOREIGN KEY (modificado_por)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED,
    CONSTRAINT "UQ_SOLICITUD_DOCUMENTO_RUTA" UNIQUE (bucket_name, supabase_path),
    CONSTRAINT "CK_SOLICITUD_DOCUMENTO_ARCHIVO" CHECK (file_size > 0 AND bucket_name <> '' AND supabase_path <> ''),
    CONSTRAINT "CK_SOLICITUD_DOCUMENTO_VERSION" CHECK (version_fila > 0)
);

CREATE INDEX IF NOT EXISTS "IX_SOLICITUD_DOCUMENTO_SOLICITUD"
    ON "SIGERSA"."SOLICITUD_DOCUMENTO" (solicitud_id, activo, tipo_documento);

CREATE TABLE IF NOT EXISTS "SIGERSA"."PROGRAMACION_HISTORIAL" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    programacion_id uuid NOT NULL,
    accion varchar(20) NOT NULL,
    inicio_anterior timestamptz,
    fin_anterior timestamptz,
    inicio_nuevo timestamptz,
    fin_nuevo timestamptz,
    prioridad_anterior smallint,
    prioridad_nueva smallint,
    estado_anterior varchar(20),
    estado_nuevo varchar(20) NOT NULL,
    motivo text NOT NULL,
    realizado_por uuid NOT NULL,
    realizado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_PROGRAMACION_HISTORIAL_PROGRAMACION" FOREIGN KEY (programacion_id)
        REFERENCES "SIGERSA"."PROGRAMACION" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_PROGRAMACION_HISTORIAL_REALIZADOR" FOREIGN KEY (realizado_por)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_PROGRAMACION_HISTORIAL_CREADO_POR" FOREIGN KEY (creado_por)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED,
    CONSTRAINT "FK_PROGRAMACION_HISTORIAL_MODIFICADO_POR" FOREIGN KEY (modificado_por)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED,
    CONSTRAINT "CK_PROGRAMACION_HISTORIAL_ACCION" CHECK (accion IN ('CREADA', 'REPROGRAMADA', 'CANCELADA')),
    CONSTRAINT "CK_PROGRAMACION_HISTORIAL_PRIORIDAD" CHECK (
        (prioridad_anterior IS NULL OR prioridad_anterior BETWEEN 1 AND 5)
        AND (prioridad_nueva IS NULL OR prioridad_nueva BETWEEN 1 AND 5)),
    CONSTRAINT "CK_PROGRAMACION_HISTORIAL_VERSION" CHECK (version_fila > 0)
);

CREATE INDEX IF NOT EXISTS "IX_PROGRAMACION_HISTORIAL_PROGRAMACION"
    ON "SIGERSA"."PROGRAMACION_HISTORIAL" (programacion_id, realizado_en DESC);

DO $triggers$
DECLARE
    tabla text;
BEGIN
    FOREACH tabla IN ARRAY ARRAY[
        'USUARIO_DOCUMENTO_AUTORIZACION', 'SOLICITUD_DOCUMENTO', 'PROGRAMACION_HISTORIAL'
    ]
    LOOP
        IF NOT EXISTS (
            SELECT 1
              FROM pg_trigger
             WHERE tgname = 'TRG_' || tabla || '_VERSION_FILA'
               AND tgrelid = format('"SIGERSA".%I', tabla)::regclass
        ) THEN
            EXECUTE format(
                'CREATE TRIGGER %I BEFORE UPDATE ON "SIGERSA".%I FOR EACH ROW EXECUTE FUNCTION "SIGERSA"."FN_INCREMENTAR_VERSION_FILA"()',
                'TRG_' || tabla || '_VERSION_FILA', tabla);
        END IF;
    END LOOP;
END
$triggers$;

COMMIT;
