BEGIN;

SET LOCAL TIME ZONE 'UTC';
SET LOCAL search_path = pg_catalog;

INSERT INTO "SIGERSA"."ParametersControl"
    ("KeyWord", "NumericData", "StringData", "Status", "CUser")
SELECT value."KeyWord", value."NumericData", value."StringData", true, 'MIGRATION_012'
FROM (VALUES
    ('ESTADO_EMPRESA', 1, 'ACTIVA'),
    ('ESTADO_EMPRESA', 2, 'INACTIVA'),
    ('ESTADO_EMPRESA', 3, 'SUSPENDIDA'),
    ('ESTADO_ACTIVIDAD', 1, 'ACTIVO'),
    ('ESTADO_ACTIVIDAD', 2, 'INACTIVO'),
    ('ESTADO_ALERTA', 1, 'RECIBIDA'),
    ('ESTADO_ALERTA', 2, 'EN_ANALISIS'),
    ('ESTADO_ALERTA', 3, 'VINCULADA'),
    ('ESTADO_ALERTA', 4, 'CERRADA'),
    ('ESTADO_HALLAZGO', 1, 'ABIERTO'),
    ('ESTADO_HALLAZGO', 2, 'EN_CORRECCION'),
    ('ESTADO_HALLAZGO', 3, 'VALIDADO'),
    ('ESTADO_HALLAZGO', 4, 'CERRADO'),
    ('ESTADO_INFORME', 1, 'BORRADOR'),
    ('ESTADO_INFORME', 2, 'GENERADO'),
    ('ESTADO_INFORME', 3, 'PUBLICADO'),
    ('RESULTADO_AUDITORIA', 1, 'EXITOSO'),
    ('RESULTADO_AUDITORIA', 2, 'DENEGADO'),
    ('RESULTADO_AUDITORIA', 3, 'ERROR'),
    ('ESTADO_PROGRAMACION', 1, 'PROGRAMADA'),
    ('ESTADO_PROGRAMACION', 2, 'REPROGRAMADA'),
    ('ESTADO_PROGRAMACION', 3, 'CANCELADA'),
    ('ESTADO_PROGRAMACION', 4, 'COMPLETADA'),
    ('ESTADO_CORRECCION', 1, 'PENDIENTE'),
    ('ESTADO_CORRECCION', 2, 'EN_PROCESO'),
    ('ESTADO_CORRECCION', 3, 'ENVIADA'),
    ('ESTADO_CORRECCION', 4, 'ACEPTADA'),
    ('ESTADO_CORRECCION', 5, 'RECHAZADA'),
    ('ESTADO_CORRECCION', 6, 'VENCIDA'),
    ('TIPO_RESPONSABLE_CORRECCION', 1, 'EMPRESA'),
    ('TIPO_RESPONSABLE_CORRECCION', 2, 'TECNICO'),
    ('TIPO_EVIDENCIA', 1, 'FOTOGRAFIA'),
    ('TIPO_EVIDENCIA', 2, 'DOCUMENTO'),
    ('TIPO_EVIDENCIA', 3, 'VIDEO'),
    ('TIPO_IDENTIFICACION', 1, 'CEDULA'),
    ('TIPO_IDENTIFICACION', 2, 'PASAPORTE'),
    ('TIPO_IDENTIFICACION', 3, 'RNC'),
    ('TIPO_IDENTIFICACION', 4, 'OTRO'),
    ('ESTADO_USUARIO_GESTION', 1, 'ACTIVO'),
    ('ESTADO_USUARIO_GESTION', 2, 'SUSPENDIDO'),
    ('DECISION_ANALISIS', 1, 'PROCEDE'),
    ('DECISION_ANALISIS', 2, 'NO_PROCEDE'),
    ('DECISION_ANALISIS', 3, 'REQUIERE_INFORMACION')
) AS value("KeyWord", "NumericData", "StringData")
WHERE NOT EXISTS (
    SELECT 1
    FROM "SIGERSA"."ParametersControl" AS parameter
    WHERE parameter."KeyWord" = value."KeyWord"
      AND parameter."StringData" = value."StringData"
      AND parameter."Status" = true
);

INSERT INTO "SIGERSA"."ParametersControl"
    ("KeyWord", "NumericData", "CCode", "StringData", "Status", "CUser")
SELECT 'TIPO_SECCION_ALLITEMS', value."NumericData", value."CCode", value."StringData", true, 'MIGRATION_012'
FROM (VALUES
    (1, 'C', 'Capítulo'),
    (2, 'S', 'Sección'),
    (3, 'SS', 'Subsección'),
    (4, 'A', 'Agrupación'),
    (5, 'I', 'Pregunta')
) AS value("NumericData", "CCode", "StringData")
WHERE NOT EXISTS (
    SELECT 1
    FROM "SIGERSA"."ParametersControl" AS parameter
    WHERE parameter."KeyWord" = 'TIPO_SECCION_ALLITEMS'
      AND parameter."CCode" = value."CCode"
      AND parameter."Status" = true
);

COMMIT;
