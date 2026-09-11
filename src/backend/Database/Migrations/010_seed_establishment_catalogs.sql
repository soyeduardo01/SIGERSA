BEGIN;

SET LOCAL TIME ZONE 'UTC';
SET LOCAL search_path = pg_catalog;

INSERT INTO "SIGERSA"."DPS_DAS" (codigo, nombre, tipo, activo)
VALUES
    ('DAS-01', 'Área I de Salud', 'DAS', true),
    ('DAS-02', 'Área II de Salud', 'DAS', true),
    ('DAS-03', 'Área III de Salud', 'DAS', true),
    ('DAS-04', 'Área IV de Salud', 'DAS', true),
    ('DAS-05', 'Área V de Salud', 'DAS', true),
    ('DAS-06', 'Área VI de Salud', 'DAS', true),
    ('DAS-07', 'Área VII de Salud', 'DAS', true),
    ('DAS-08', 'Área VIII de Salud', 'DAS', true),
    ('DPS-AZUA', 'Azua', 'DPS', true),
    ('DPS-BAHORUCO', 'Bahoruco', 'DPS', true),
    ('DPS-BARAHONA', 'Barahona', 'DPS', true),
    ('DPS-DAJABON', 'Dajabón', 'DPS', true),
    ('DPS-DUARTE', 'Duarte', 'DPS', true),
    ('DPS-EL-SEIBO', 'El Seibo', 'DPS', true),
    ('DPS-ELIAS-PINA', 'Elías Piña', 'DPS', true),
    ('DPS-ESPAILLAT', 'Espaillat', 'DPS', true),
    ('DPS-HATO-MAYOR', 'Hato Mayor', 'DPS', true),
    ('DPS-HERMANAS-MIRABAL', 'Hermanas Mirabal', 'DPS', true),
    ('DPS-INDEPENDENCIA', 'Independencia', 'DPS', true),
    ('DPS-LA-ALTAGRACIA', 'La Altagracia', 'DPS', true),
    ('DPS-LA-ROMANA', 'La Romana', 'DPS', true),
    ('DPS-LA-VEGA', 'La Vega', 'DPS', true),
    ('DPS-VALVERDE', 'Mao, Valverde', 'DPS', true),
    ('DPS-MARIA-TRINIDAD-SANCHEZ', 'María Trinidad Sánchez', 'DPS', true)
ON CONFLICT (codigo) DO NOTHING;

INSERT INTO "SIGERSA"."COMERCIALIZACION" (codigo, nombre, orden, activo)
VALUES
    ('LOCAL', 'Local', 1, true),
    ('NACIONAL', 'Nacional', 2, true),
    ('INTERNACIONAL', 'Internacional', 3, true)
ON CONFLICT (codigo) DO NOTHING;

INSERT INTO "SIGERSA"."MERCADO_OBJETIVO" (codigo, nombre, orden, activo)
VALUES
    ('ADULTOS', 'Adultos', 1, true),
    ('POBLACION_VULNERABLE', 'Población vulnerable (niños, lactantes, ancianos e inmunodeprimidos)', 2, true),
    ('PUBLICO_GENERAL', 'Público en general', 3, true)
ON CONFLICT (codigo) DO NOTHING;

INSERT INTO "SIGERSA"."MOTIVO_INSPECCION"
    (codigo, nombre, requiere_detalle, ficha_completa_default, orden, activo)
VALUES
    ('SOLICITUD_PERMISO_SANITARIO', 'Solicitud de Permiso Sanitario', false, true, 1, true),
    ('INVESTIGACION_DENUNCIA', 'Investigación por denuncia', true, true, 2, true),
    ('VIGILANCIA_CONTROL_RUTINA', 'Vigilancia y control de rutina (Monitoreo oficial)', false, true, 3, true),
    ('RENOVACION_PERMISO_SANITARIO', 'Renovación de Permiso Sanitario', false, true, 4, true)
ON CONFLICT (codigo) DO NOTHING;

INSERT INTO "SIGERSA"."ParametersControl"
    ("KeyWord", "NumericData", "StringData", "Status", "CUser")
SELECT value."KeyWord", value."NumericData", value."StringData", true, 'MIGRATION_010'
FROM (VALUES
    ('NIVEL_IMPLEMENTACION_HACCP', 1, '25_POR_CIENTO'),
    ('NIVEL_IMPLEMENTACION_HACCP', 2, '75_POR_CIENTO'),
    ('NIVEL_IMPLEMENTACION_HACCP', 3, 'TODAS_LAS_LINEAS'),
    ('APLICACION_MUESTREO_MICROBIOLOGICO', 1, 'MATERIAS_PRIMAS'),
    ('APLICACION_MUESTREO_MICROBIOLOGICO', 2, 'AREAS_PROCESO_PRODUCTOS_TERMINADOS'),
    ('APLICACION_MUESTREO_MICROBIOLOGICO', 3, 'MATERIAS_PRIMAS_AREAS_PROCESO_PRODUCTOS_TERMINADOS'),
    ('DISTRIBUCION_INABIE', 1, 'NACIONAL'),
    ('DISTRIBUCION_INABIE', 2, 'REGIONAL'),
    ('DISTRIBUCION_INABIE', 3, 'LOCAL')
) AS value("KeyWord", "NumericData", "StringData")
WHERE NOT EXISTS (
    SELECT 1
    FROM "SIGERSA"."ParametersControl" AS parameter
    WHERE parameter."KeyWord" = value."KeyWord"
      AND parameter."StringData" = value."StringData"
      AND parameter."Status" = true
);

COMMIT;
