BEGIN;

-- Los roles heredados se transforman preservando sus UUID para no romper
-- asignaciones existentes. EMPRESA pasa al perfil de menor privilegio.
UPDATE "SIGERSA"."ROL"
SET codigo = 'TECNICO_EVALUADOR',
    nombre = 'Técnico Evaluador',
    descripcion = 'Ejecución de evaluaciones e inspecciones asignadas.',
    modificado_en = CURRENT_TIMESTAMP
WHERE id = '10000000-0000-0000-0000-000000000003'
  AND codigo = 'TECNICO';

UPDATE "SIGERSA"."ROL"
SET codigo = 'USUARIO_DELEGADO',
    nombre = 'Usuario Delegado',
    descripcion = 'Actuación limitada en representación de una empresa.',
    modificado_en = CURRENT_TIMESTAMP
WHERE id = '10000000-0000-0000-0000-000000000004'
  AND codigo = 'EMPRESA';

INSERT INTO "SIGERSA"."ROL"
    (id, codigo, nombre, descripcion, es_privilegiado)
VALUES
    ('10000000-0000-0000-0000-000000000005', 'ADMINISTRADOR_EMPRESA',
     'Administrador Empresa',
     'Administración de usuarios, perfil y solicitudes de su empresa.', false)
ON CONFLICT (codigo) DO UPDATE
SET nombre = EXCLUDED.nombre,
    descripcion = EXCLUDED.descripcion,
    es_privilegiado = EXCLUDED.es_privilegiado,
    activo = true,
    modificado_en = CURRENT_TIMESTAMP;

INSERT INTO "SIGERSA"."PERMISO"
    (id, codigo, nombre, descripcion, recurso, accion)
VALUES
    ('20000000-0000-0000-0000-000000000014', 'COMPANY_USERS.MANAGE',
     'Administrar usuarios de empresa',
     'Alta y mantenimiento de usuarios dentro de la empresa propia.',
     'COMPANY_USERS', 'MANAGE'),
    ('20000000-0000-0000-0000-000000000015', 'COMPANY_PROFILE.MANAGE',
     'Administrar perfil de empresa',
     'Mantenimiento del perfil y establecimientos de la empresa propia.',
     'COMPANY_PROFILE', 'MANAGE'),
    ('20000000-0000-0000-0000-000000000016', 'REQUESTS.MANAGE',
     'Administrar solicitudes',
     'Crear y mantener solicitudes dentro del ámbito autorizado.',
     'REQUESTS', 'MANAGE')
ON CONFLICT (codigo) DO UPDATE
SET nombre = EXCLUDED.nombre,
    descripcion = EXCLUDED.descripcion,
    recurso = EXCLUDED.recurso,
    accion = EXCLUDED.accion,
    activo = true,
    modificado_en = CURRENT_TIMESTAMP;

INSERT INTO "SIGERSA"."ROL_PERMISO" (id, rol_id, permiso_id)
SELECT gen_random_uuid(), role.id, permission.id
FROM "SIGERSA"."ROL" AS role
CROSS JOIN "SIGERSA"."PERMISO" AS permission
WHERE
    role.codigo = 'ADMINISTRADOR'
    OR (role.codigo = 'COORDINADOR' AND permission.codigo IN (
        'PARAMETERS.READ', 'TEMPLATES.READ', 'CASES.MANAGE', 'EVALUATIONS.REVIEW',
        'EVIDENCES.READ', 'REPORTS.READ', 'AUDIT.READ', 'REQUESTS.MANAGE'
    ))
    OR (role.codigo = 'TECNICO_EVALUADOR' AND permission.codigo IN (
        'PARAMETERS.READ', 'TEMPLATES.READ', 'EVALUATIONS.EXECUTE',
        'EVIDENCES.UPLOAD', 'EVIDENCES.READ', 'REPORTS.READ'
    ))
    OR (role.codigo = 'USUARIO_DELEGADO' AND permission.codigo IN (
        'PARAMETERS.READ', 'TEMPLATES.READ', 'EVIDENCES.READ',
        'CORRECTIONS.OWN', 'REPORTS.READ', 'REQUESTS.MANAGE'
    ))
    OR (role.codigo = 'ADMINISTRADOR_EMPRESA' AND permission.codigo IN (
        'PARAMETERS.READ', 'TEMPLATES.READ', 'EVIDENCES.READ',
        'CORRECTIONS.OWN', 'REPORTS.READ', 'REQUESTS.MANAGE',
        'COMPANY_USERS.MANAGE', 'COMPANY_PROFILE.MANAGE'
    ))
ON CONFLICT (rol_id, permiso_id) DO UPDATE
SET activo = true,
    modificado_en = CURRENT_TIMESTAMP;

UPDATE "SIGERSA"."USUARIO_ROL" AS user_role
SET empresa_ambito_id = app_user.empresa_id,
    modificado_en = CURRENT_TIMESTAMP
FROM "SIGERSA"."USUARIO" AS app_user,
     "SIGERSA"."ROL" AS role
WHERE user_role.usuario_id = app_user.id
  AND user_role.rol_id = role.id
  AND role.codigo IN ('ADMINISTRADOR_EMPRESA', 'USUARIO_DELEGADO')
  AND user_role.empresa_ambito_id IS NULL
  AND app_user.empresa_id IS NOT NULL;

DO $validation$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM "SIGERSA"."USUARIO_ROL" AS user_role
        JOIN "SIGERSA"."ROL" AS role ON role.id = user_role.rol_id
        WHERE role.codigo IN ('ADMINISTRADOR_EMPRESA', 'USUARIO_DELEGADO')
          AND user_role.empresa_ambito_id IS NULL
    ) THEN
        RAISE EXCEPTION 'Existen roles empresariales sin empresa_ambito_id; corrija sus usuarios antes de continuar';
    END IF;
END;
$validation$;

CREATE OR REPLACE FUNCTION "SIGERSA"."FN_VALIDAR_AMBITO_ROL_EMPRESA"()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
DECLARE
    v_rol_codigo varchar(50);
    v_usuario_empresa uuid;
    v_establecimiento_empresa uuid;
BEGIN
    SELECT codigo INTO v_rol_codigo
    FROM "SIGERSA"."ROL"
    WHERE id = NEW.rol_id;

    IF v_rol_codigo IN ('ADMINISTRADOR_EMPRESA', 'USUARIO_DELEGADO') THEN
        SELECT empresa_id INTO v_usuario_empresa
        FROM "SIGERSA"."USUARIO"
        WHERE id = NEW.usuario_id;
        NEW.empresa_ambito_id := COALESCE(NEW.empresa_ambito_id, v_usuario_empresa);
        IF NEW.empresa_ambito_id IS NULL THEN
            RAISE EXCEPTION 'Los roles empresariales requieren empresa_ambito_id';
        END IF;
        IF v_usuario_empresa IS NOT NULL AND v_usuario_empresa <> NEW.empresa_ambito_id THEN
            RAISE EXCEPTION 'El ámbito del rol no coincide con la empresa del usuario';
        END IF;
        IF NEW.establecimiento_ambito_id IS NOT NULL THEN
            SELECT empresa_id INTO v_establecimiento_empresa
            FROM "SIGERSA"."ESTABLECIMIENTO"
            WHERE id = NEW.establecimiento_ambito_id;
            IF v_establecimiento_empresa IS DISTINCT FROM NEW.empresa_ambito_id THEN
                RAISE EXCEPTION 'El establecimiento no pertenece al ámbito empresarial del rol';
            END IF;
        END IF;
    END IF;
    RETURN NEW;
END;
$function$;

DROP TRIGGER IF EXISTS "TRG_USUARIO_ROL_AMBITO_EMPRESA" ON "SIGERSA"."USUARIO_ROL";
CREATE TRIGGER "TRG_USUARIO_ROL_AMBITO_EMPRESA"
BEFORE INSERT OR UPDATE OF usuario_id, rol_id, empresa_ambito_id, establecimiento_ambito_id
ON "SIGERSA"."USUARIO_ROL"
FOR EACH ROW
EXECUTE FUNCTION "SIGERSA"."FN_VALIDAR_AMBITO_ROL_EMPRESA"();

COMMIT;
