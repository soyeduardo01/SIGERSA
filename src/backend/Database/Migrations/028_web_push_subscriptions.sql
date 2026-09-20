BEGIN;

SET LOCAL TIME ZONE 'UTC';
SET LOCAL search_path = pg_catalog;

CREATE TABLE IF NOT EXISTS "SIGERSA"."SUSCRIPCION_PUSH" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    usuario_id uuid NOT NULL,
    endpoint varchar(2048) NOT NULL,
    clave_p256dh varchar(256) NOT NULL,
    clave_auth varchar(128) NOT NULL,
    expira_en timestamptz,
    agente_usuario varchar(500),
    activa boolean NOT NULL DEFAULT true,
    ultimo_envio_en timestamptz,
    ultimo_fallo_en timestamptz,
    fallos_consecutivos integer NOT NULL DEFAULT 0,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_SUSCRIPCION_PUSH_USUARIO" FOREIGN KEY (usuario_id)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE CASCADE,
    CONSTRAINT "UQ_SUSCRIPCION_PUSH_ENDPOINT" UNIQUE (endpoint),
    CONSTRAINT "CK_SUSCRIPCION_PUSH_FALLOS" CHECK (fallos_consecutivos >= 0),
    CONSTRAINT "CK_SUSCRIPCION_PUSH_VERSION" CHECK (version_fila > 0)
);

CREATE INDEX IF NOT EXISTS "IX_SUSCRIPCION_PUSH_USUARIO_ACTIVA"
    ON "SIGERSA"."SUSCRIPCION_PUSH" (usuario_id, activa)
    WHERE activa = true;

COMMIT;
