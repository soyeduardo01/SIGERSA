BEGIN;

INSERT INTO "SIGERSA"."ROL" (id, codigo, nombre, descripcion, es_privilegiado, activo)
VALUES ('10000000-0000-0000-0000-000000000006', 'LABORATORISTA', 'Laboratorista',
        'Empleado institucional autorizado exclusivamente para emitir denuncias y alertas.', false, true)
ON CONFLICT (codigo) DO UPDATE
SET nombre = EXCLUDED.nombre, descripcion = EXCLUDED.descripcion, activo = true;

INSERT INTO "SIGERSA"."PERMISO" (id, codigo, nombre, descripcion, recurso, accion, activo)
VALUES
    (gen_random_uuid(), 'DASHBOARD.READ', 'Consultar dashboard', 'Consulta del tablero según el ámbito de sesión.', 'DASHBOARD', 'READ', true),
    (gen_random_uuid(), 'PROFILE.MANAGE', 'Gestionar perfil propio', 'Consulta y edición exclusiva del perfil de la sesión.', 'PROFILE', 'MANAGE', true),
    (gen_random_uuid(), 'NOTIFICATIONS.READ', 'Consultar notificaciones propias', 'Consulta y sincronización exclusiva de la sesión.', 'NOTIFICATIONS', 'READ', true),
    (gen_random_uuid(), 'SURVEILLANCE.MANAGE', 'Emitir alertas y denuncias', 'Registro de alertas LAPCH y denuncias institucionales.', 'SURVEILLANCE', 'MANAGE', true)
ON CONFLICT (codigo) DO UPDATE
SET nombre = EXCLUDED.nombre, descripcion = EXCLUDED.descripcion,
    recurso = EXCLUDED.recurso, accion = EXCLUDED.accion, activo = true;

DELETE FROM "SIGERSA"."ROL_PERMISO" role_permission
 USING "SIGERSA"."ROL" role
 WHERE role_permission.rol_id = role.id
   AND role.codigo IN ('ADMINISTRADOR_EMPRESA', 'USUARIO_DELEGADO', 'LABORATORISTA');

INSERT INTO "SIGERSA"."ROL_PERMISO" (id, rol_id, permiso_id)
SELECT gen_random_uuid(), role.id, permission.id
  FROM "SIGERSA"."ROL" role
  CROSS JOIN "SIGERSA"."PERMISO" permission
 WHERE (role.codigo = 'ADMINISTRADOR_EMPRESA' AND permission.codigo IN (
            'DASHBOARD.READ', 'COMPANY_USERS.MANAGE', 'PROFILE.MANAGE', 'NOTIFICATIONS.READ'))
    OR (role.codigo = 'USUARIO_DELEGADO' AND permission.codigo IN (
            'REQUESTS.MANAGE', 'REPORTS.READ', 'PROFILE.MANAGE', 'NOTIFICATIONS.READ'))
    OR (role.codigo = 'LABORATORISTA' AND permission.codigo IN (
            'SURVEILLANCE.MANAGE', 'PROFILE.MANAGE', 'NOTIFICATIONS.READ'))
ON CONFLICT (rol_id, permiso_id) DO NOTHING;

ALTER TABLE "SIGERSA"."DENUNCIA"
    ADD COLUMN IF NOT EXISTS coordinador_asignado_id uuid;

DO $migration$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_DENUNCIA_COORDINADOR') THEN
        ALTER TABLE "SIGERSA"."DENUNCIA"
            ADD CONSTRAINT "FK_DENUNCIA_COORDINADOR" FOREIGN KEY (coordinador_asignado_id)
            REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT;
    END IF;
END
$migration$;

ALTER TABLE "SIGERSA"."CONTACTO"
    ADD COLUMN IF NOT EXISTS tipo_identificacion varchar(12);

ALTER TABLE "SIGERSA"."CONTACTO" DROP CONSTRAINT IF EXISTS "CK_CONTACTO_TIPO_IDENTIFICACION";
ALTER TABLE "SIGERSA"."CONTACTO"
    ADD CONSTRAINT "CK_CONTACTO_TIPO_IDENTIFICACION"
    CHECK (tipo_identificacion IS NULL OR tipo_identificacion IN ('CEDULA', 'PASAPORTE'));

CREATE OR REPLACE FUNCTION "SIGERSA"."FN_LIMITE_EVIDENCIAS_ITEM"()
RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE
    v_total integer;
BEGIN
    SELECT COUNT(*) INTO v_total
      FROM "SIGERSA"."EVIDENCIA_RESPUESTA" er
      JOIN "SIGERSA"."RESPUESTA_USUARIO" r ON r.id = er.respuesta_usuario_id
      JOIN "SIGERSA"."EVIDENCIA" e ON e.id = er.evidencia_id
     WHERE r.id = NEW.respuesta_usuario_id
       AND e.eliminada = false;
    IF v_total >= 3 THEN
        RAISE EXCEPTION 'Cada ítem admite un máximo de 3 evidencias.' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END;
$$;

DROP TRIGGER IF EXISTS "TRG_LIMITE_EVIDENCIAS_ITEM" ON "SIGERSA"."EVIDENCIA_RESPUESTA";
CREATE TRIGGER "TRG_LIMITE_EVIDENCIAS_ITEM"
BEFORE INSERT ON "SIGERSA"."EVIDENCIA_RESPUESTA"
FOR EACH ROW EXECUTE FUNCTION "SIGERSA"."FN_LIMITE_EVIDENCIAS_ITEM"();

COMMIT;
