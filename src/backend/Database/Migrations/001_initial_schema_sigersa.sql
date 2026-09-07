BEGIN;

SET LOCAL TIME ZONE 'UTC';
SET LOCAL search_path = pg_catalog;

-- =============================================================================
-- A. Inicialización del esquema
-- =============================================================================

CREATE SCHEMA IF NOT EXISTS "SIGERSA";

COMMENT ON SCHEMA "SIGERSA" IS
    'Esquema obligatorio para todos los objetos de negocio de SIGERSA.';

-- =============================================================================
-- B. Tipos enumerados
-- =============================================================================

CREATE TYPE "SIGERSA"."ESTADO_CASO" AS ENUM (
    'BORRADOR',
    'PENDIENTE_ANALISIS',
    'PENDIENTE_ASIGNACION',
    'PENDIENTE_PROGRAMACION',
    'PROGRAMADO',
    'ASIGNADO',
    'EN_EJECUCION',
    'PENDIENTE_REVISION',
    'EN_CORRECCION',
    'APROBADO',
    'CERRADO',
    'CERRADO_NO_PROCEDE',
    'CANCELADO'
);

CREATE TYPE "SIGERSA"."ESTADO_FICHA" AS ENUM (
    'BORRADOR',
    'EN_REVISION',
    'PUBLICADA',
    'RETIRADA',
    'ARCHIVADA'
);

CREATE TYPE "SIGERSA"."ESTADO_EVALUACION" AS ENUM (
    'ASIGNADA',
    'EN_EJECUCION',
    'PAUSADA',
    'PENDIENTE_REVISION',
    'EN_CORRECCION',
    'APROBADA',
    'CERRADA',
    'CANCELADA'
);

CREATE TYPE "SIGERSA"."TIPO_ITEM_FICHA" AS ENUM (
    'SECCION',
    'SUBSECCION',
    'GRUPO',
    'CRITERIO',
    'PREGUNTA',
    'TEXTO_INFORMATIVO',
    'SUBTOTAL'
);

CREATE TYPE "SIGERSA"."TIPO_RESPUESTA" AS ENUM (
    'OPCION_UNICA',
    'OPCION_MULTIPLE',
    'TEXTO_CORTO',
    'TEXTO_LARGO',
    'ENTERO',
    'DECIMAL',
    'FECHA',
    'HORA',
    'FECHA_HORA',
    'BOOLEANO',
    'ARCHIVO'
);

CREATE TYPE "SIGERSA"."NIVEL_RIESGO" AS ENUM (
    'BAJO',
    'MEDIO',
    'ALTO',
    'NO_CALCULABLE'
);

CREATE TYPE "SIGERSA"."ESTADO_SINCRONIZACION" AS ENUM (
    'PENDIENTE',
    'EN_PROCESO',
    'SINCRONIZADA',
    'ERROR',
    'CONFLICTO'
);

-- =============================================================================
-- C. Tablas maestras, catálogos, usuarios y permisos
-- Todas las tablas incluyen auditoría y versión para concurrencia optimista.
-- =============================================================================

CREATE TABLE "SIGERSA"."EMPRESA" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    razon_social varchar(250) NOT NULL,
    rnc_normalizado varchar(30) NOT NULL,
    nombre_comercial varchar(250),
    actividad_economica varchar(250),
    telefono varchar(40),
    correo varchar(320),
    estado varchar(20) NOT NULL DEFAULT 'ACTIVA',
    representantes jsonb NOT NULL DEFAULT '[]'::jsonb,
    activo boolean NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "UQ_EMPRESA_RNC" UNIQUE (rnc_normalizado),
    CONSTRAINT "CK_EMPRESA_ESTADO" CHECK (estado IN ('ACTIVA', 'INACTIVA', 'SUSPENDIDA')),
    CONSTRAINT "CK_EMPRESA_REPRESENTANTES" CHECK (jsonb_typeof(representantes) = 'array'),
    CONSTRAINT "CK_EMPRESA_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."CATEGORIA_ALIMENTO" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo varchar(50) NOT NULL,
    nombre varchar(200) NOT NULL,
    descripcion text,
    orden integer NOT NULL DEFAULT 0,
    vigente_desde timestamptz,
    vigente_hasta timestamptz,
    fuente varchar(250),
    activo boolean NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "UQ_CATEGORIA_ALIMENTO_CODIGO" UNIQUE (codigo),
    CONSTRAINT "CK_CATEGORIA_ORDEN" CHECK (orden >= 0),
    CONSTRAINT "CK_CATEGORIA_VIGENCIA" CHECK (vigente_hasta IS NULL OR vigente_desde IS NULL OR vigente_hasta > vigente_desde),
    CONSTRAINT "CK_CATEGORIA_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."SUBCATEGORIA_ALIMENTO" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    categoria_alimento_id uuid NOT NULL,
    codigo varchar(50) NOT NULL,
    nombre varchar(200) NOT NULL,
    descripcion text,
    nivel_riesgo smallint,
    orden integer NOT NULL DEFAULT 0,
    vigente_desde timestamptz,
    vigente_hasta timestamptz,
    fuente varchar(250),
    activo boolean NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_SUBCATEGORIA_CATEGORIA" FOREIGN KEY (categoria_alimento_id)
        REFERENCES "SIGERSA"."CATEGORIA_ALIMENTO" (id) ON DELETE RESTRICT,
    CONSTRAINT "UQ_SUBCATEGORIA_CODIGO" UNIQUE (categoria_alimento_id, codigo),
    CONSTRAINT "UQ_SUBCATEGORIA_ID_CATEGORIA" UNIQUE (id, categoria_alimento_id),
    CONSTRAINT "CK_SUBCATEGORIA_RIESGO" CHECK (nivel_riesgo IS NULL OR nivel_riesgo BETWEEN 1 AND 3),
    CONSTRAINT "CK_SUBCATEGORIA_ORDEN" CHECK (orden >= 0),
    CONSTRAINT "CK_SUBCATEGORIA_VIGENCIA" CHECK (vigente_hasta IS NULL OR vigente_desde IS NULL OR vigente_hasta > vigente_desde),
    CONSTRAINT "CK_SUBCATEGORIA_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."ROL" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo varchar(50) NOT NULL,
    nombre varchar(120) NOT NULL,
    descripcion text,
    es_privilegiado boolean NOT NULL DEFAULT false,
    activo boolean NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "UQ_ROL_CODIGO" UNIQUE (codigo),
    CONSTRAINT "CK_ROL_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."PERMISO" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo varchar(100) NOT NULL,
    nombre varchar(150) NOT NULL,
    descripcion text,
    recurso varchar(100) NOT NULL,
    accion varchar(50) NOT NULL,
    activo boolean NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "UQ_PERMISO_CODIGO" UNIQUE (codigo),
    CONSTRAINT "CK_PERMISO_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."NIVEL_CRITICIDAD" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo varchar(20) NOT NULL,
    nombre varchar(100) NOT NULL,
    descripcion text NOT NULL,
    prioridad smallint NOT NULL,
    plazo_maximo_dias integer,
    requiere_accion_inmediata boolean NOT NULL DEFAULT false,
    vigente_desde timestamptz,
    vigente_hasta timestamptz,
    activo boolean NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "UQ_NIVEL_CRITICIDAD_CODIGO" UNIQUE (codigo),
    CONSTRAINT "UQ_NIVEL_CRITICIDAD_PRIORIDAD" UNIQUE (prioridad),
    CONSTRAINT "CK_CRITICIDAD_PRIORIDAD" CHECK (prioridad > 0),
    CONSTRAINT "CK_CRITICIDAD_PLAZO" CHECK (plazo_maximo_dias IS NULL OR plazo_maximo_dias >= 0),
    CONSTRAINT "CK_CRITICIDAD_VIGENCIA" CHECK (vigente_hasta IS NULL OR vigente_desde IS NULL OR vigente_hasta > vigente_desde),
    CONSTRAINT "CK_CRITICIDAD_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."PROVINCIA" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo varchar(50) NOT NULL,
    nombre varchar(150) NOT NULL,
    activo boolean NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "UQ_PROVINCIA_CODIGO" UNIQUE (codigo),
    CONSTRAINT "CK_PROVINCIA_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."MUNICIPIO" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    provincia_id uuid NOT NULL,
    codigo varchar(50) NOT NULL,
    nombre varchar(150) NOT NULL,
    activo boolean NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_MUNICIPIO_PROVINCIA" FOREIGN KEY (provincia_id)
        REFERENCES "SIGERSA"."PROVINCIA" (id) ON DELETE RESTRICT,
    CONSTRAINT "UQ_MUNICIPIO_CODIGO" UNIQUE (provincia_id, codigo),
    CONSTRAINT "CK_MUNICIPIO_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."DPS_DAS" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo varchar(50) NOT NULL,
    nombre varchar(200) NOT NULL,
    tipo varchar(10) NOT NULL,
    activo boolean NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "UQ_DPS_DAS_CODIGO" UNIQUE (codigo),
    CONSTRAINT "CK_DPS_DAS_TIPO" CHECK (tipo IN ('DPS', 'DAS')),
    CONSTRAINT "CK_DPS_DAS_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."COMERCIALIZACION" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo varchar(50) NOT NULL,
    nombre varchar(150) NOT NULL,
    orden integer NOT NULL DEFAULT 0,
    activo boolean NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "UQ_COMERCIALIZACION_CODIGO" UNIQUE (codigo),
    CONSTRAINT "CK_COMERCIALIZACION_ORDEN" CHECK (orden >= 0),
    CONSTRAINT "CK_COMERCIALIZACION_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."MERCADO_OBJETIVO" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo varchar(50) NOT NULL,
    nombre varchar(150) NOT NULL,
    orden integer NOT NULL DEFAULT 0,
    activo boolean NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "UQ_MERCADO_OBJETIVO_CODIGO" UNIQUE (codigo),
    CONSTRAINT "CK_MERCADO_OBJETIVO_ORDEN" CHECK (orden >= 0),
    CONSTRAINT "CK_MERCADO_OBJETIVO_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."MOTIVO_INSPECCION" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo varchar(50) NOT NULL,
    nombre varchar(180) NOT NULL,
    requiere_detalle boolean NOT NULL DEFAULT false,
    ficha_completa_default boolean NOT NULL DEFAULT true,
    orden integer NOT NULL DEFAULT 0,
    activo boolean NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "UQ_MOTIVO_INSPECCION_CODIGO" UNIQUE (codigo),
    CONSTRAINT "CK_MOTIVO_INSPECCION_ORDEN" CHECK (orden >= 0),
    CONSTRAINT "CK_MOTIVO_INSPECCION_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."ESTABLECIMIENTO" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    empresa_id uuid NOT NULL,
    municipio_id uuid,
    dps_das_id uuid,
    comercializacion_id uuid,
    codigo varchar(50) NOT NULL,
    nombre varchar(250) NOT NULL,
    calle varchar(250),
    numero_direccion varchar(50),
    telefono varchar(40),
    correo varchar(320),
    fecha_inicio_operaciones timestamptz,
    permiso_sanitario_numero varchar(100),
    permiso_sanitario_vence_en timestamptz,
    productos_descripcion text,
    produccion_anual numeric(18, 4),
    empleados_mujeres integer,
    empleados_hombres integer,
    haccp_implementado boolean,
    nivel_haccp_porcentaje numeric(5, 2),
    plan_muestreo_microbiologico boolean,
    aplicacion_muestreo_codigo varchar(50),
    es_suplidor_inabie boolean,
    distribucion_inabie_codigo varchar(50),
    latitud numeric(9, 6),
    longitud numeric(9, 6),
    precision_m numeric(12, 3),
    ubicacion_capturada_en timestamptz,
    ubicacion_origen varchar(50),
    estado varchar(20) NOT NULL DEFAULT 'ACTIVO',
    activo boolean NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_ESTABLECIMIENTO_EMPRESA" FOREIGN KEY (empresa_id)
        REFERENCES "SIGERSA"."EMPRESA" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_ESTABLECIMIENTO_MUNICIPIO" FOREIGN KEY (municipio_id)
        REFERENCES "SIGERSA"."MUNICIPIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_ESTABLECIMIENTO_DPS_DAS" FOREIGN KEY (dps_das_id)
        REFERENCES "SIGERSA"."DPS_DAS" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_ESTABLECIMIENTO_COMERCIALIZACION" FOREIGN KEY (comercializacion_id)
        REFERENCES "SIGERSA"."COMERCIALIZACION" (id) ON DELETE RESTRICT,
    CONSTRAINT "UQ_ESTABLECIMIENTO_CODIGO" UNIQUE (empresa_id, codigo),
    CONSTRAINT "UQ_ESTABLECIMIENTO_ID_EMPRESA" UNIQUE (id, empresa_id),
    CONSTRAINT "CK_ESTABLECIMIENTO_PRODUCCION" CHECK (produccion_anual IS NULL OR produccion_anual >= 0),
    CONSTRAINT "CK_ESTABLECIMIENTO_EMPLEADAS" CHECK (empleados_mujeres IS NULL OR empleados_mujeres >= 0),
    CONSTRAINT "CK_ESTABLECIMIENTO_EMPLEADOS" CHECK (empleados_hombres IS NULL OR empleados_hombres >= 0),
    CONSTRAINT "CK_ESTABLECIMIENTO_LATITUD" CHECK (latitud IS NULL OR latitud BETWEEN -90 AND 90),
    CONSTRAINT "CK_ESTABLECIMIENTO_LONGITUD" CHECK (longitud IS NULL OR longitud BETWEEN -180 AND 180),
    CONSTRAINT "CK_ESTABLECIMIENTO_PRECISION" CHECK (precision_m IS NULL OR precision_m >= 0),
    CONSTRAINT "CK_ESTABLECIMIENTO_COORDENADAS" CHECK ((latitud IS NULL) = (longitud IS NULL)),
    CONSTRAINT "CK_ESTABLECIMIENTO_ESTADO" CHECK (estado IN ('ACTIVO', 'INACTIVO', 'SUSPENDIDO')),
    CONSTRAINT "CK_ESTABLECIMIENTO_HACCP" CHECK (
        (haccp_implementado IS NULL AND nivel_haccp_porcentaje IS NULL)
        OR (haccp_implementado = false AND nivel_haccp_porcentaje IS NULL)
        OR (haccp_implementado = true AND nivel_haccp_porcentaje IN (25.00, 75.00, 100.00))
    ),
    CONSTRAINT "CK_ESTABLECIMIENTO_MUESTREO" CHECK (
        (plan_muestreo_microbiologico IS NULL AND aplicacion_muestreo_codigo IS NULL)
        OR (plan_muestreo_microbiologico = false AND aplicacion_muestreo_codigo IS NULL)
        OR (plan_muestreo_microbiologico = true AND aplicacion_muestreo_codigo IS NOT NULL)
    ),
    CONSTRAINT "CK_ESTABLECIMIENTO_INABIE" CHECK (
        (es_suplidor_inabie IS NULL AND distribucion_inabie_codigo IS NULL)
        OR (es_suplidor_inabie = false AND distribucion_inabie_codigo IS NULL)
        OR (es_suplidor_inabie = true AND distribucion_inabie_codigo IS NOT NULL)
    ),
    CONSTRAINT "CK_ESTABLECIMIENTO_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."CONTACTO" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    empresa_id uuid NOT NULL,
    establecimiento_id uuid,
    tipo varchar(40) NOT NULL,
    nombre_completo varchar(250) NOT NULL,
    identificacion_normalizada varchar(100),
    telefono varchar(40),
    correo varchar(320),
    principal boolean NOT NULL DEFAULT false,
    activo boolean NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_CONTACTO_EMPRESA" FOREIGN KEY (empresa_id)
        REFERENCES "SIGERSA"."EMPRESA" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_CONTACTO_ESTABLECIMIENTO_EMPRESA" FOREIGN KEY (establecimiento_id, empresa_id)
        REFERENCES "SIGERSA"."ESTABLECIMIENTO" (id, empresa_id) ON DELETE RESTRICT,
    CONSTRAINT "CK_CONTACTO_TIPO" CHECK (tipo IN ('LEGAL', 'CALIDAD', 'PRINCIPAL', 'OTRO')),
    CONSTRAINT "CK_CONTACTO_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."USUARIO" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    empresa_id uuid,
    nombre_completo varchar(250) NOT NULL,
    tipo_identificacion varchar(30) NOT NULL,
    identificacion_normalizada varchar(100) NOT NULL,
    correo varchar(320) NOT NULL,
    correo_normalizado varchar(320) NOT NULL,
    telefono varchar(40),
    password_hash text NOT NULL,
    estado varchar(30) NOT NULL DEFAULT 'PENDIENTE_VALIDACION',
    ultimo_acceso_en timestamptz,
    fallos_acceso integer NOT NULL DEFAULT 0,
    bloqueado_hasta timestamptz,
    activo boolean NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_USUARIO_EMPRESA" FOREIGN KEY (empresa_id)
        REFERENCES "SIGERSA"."EMPRESA" (id) ON DELETE RESTRICT,
    CONSTRAINT "UQ_USUARIO_IDENTIFICACION" UNIQUE (identificacion_normalizada),
    CONSTRAINT "UQ_USUARIO_CORREO" UNIQUE (correo_normalizado),
    CONSTRAINT "CK_USUARIO_CORREO_NORMALIZADO" CHECK (correo_normalizado = lower(correo_normalizado)),
    CONSTRAINT "CK_USUARIO_ESTADO" CHECK (estado IN (
        'PENDIENTE_VALIDACION', 'ACTIVO', 'RECHAZADO', 'SUSPENDIDO', 'DESACTIVADO', 'BLOQUEADO'
    )),
    CONSTRAINT "CK_USUARIO_FALLOS" CHECK (fallos_acceso >= 0),
    CONSTRAINT "CK_USUARIO_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."OTP_RECUPERACION" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    usuario_id uuid NOT NULL,
    otp_hash text NOT NULL,
    expira_en timestamptz NOT NULL,
    intentos integer NOT NULL DEFAULT 0,
    usado_en timestamptz,
    invalidado_en timestamptz,
    estado varchar(20) NOT NULL DEFAULT 'ACTIVO',
    ip_hash varchar(128),
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_OTP_USUARIO" FOREIGN KEY (usuario_id)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "CK_OTP_INTENTOS" CHECK (intentos >= 0),
    CONSTRAINT "CK_OTP_ESTADO" CHECK (estado IN ('ACTIVO', 'USADO', 'EXPIRADO', 'INVALIDADO', 'BLOQUEADO')),
    CONSTRAINT "CK_OTP_EXPIRACION" CHECK (expira_en > creado_en),
    CONSTRAINT "CK_OTP_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."REFRESH_TOKEN" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    usuario_id uuid NOT NULL,
    token_hash text NOT NULL,
    familia_token_id uuid NOT NULL,
    expira_en timestamptz NOT NULL,
    revocado_en timestamptz,
    reemplazado_por_id uuid,
    dispositivo varchar(250),
    ip_hash varchar(128),
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "UQ_REFRESH_TOKEN_HASH" UNIQUE (token_hash),
    CONSTRAINT "FK_REFRESH_TOKEN_USUARIO" FOREIGN KEY (usuario_id)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_REFRESH_TOKEN_REEMPLAZO" FOREIGN KEY (reemplazado_por_id)
        REFERENCES "SIGERSA"."REFRESH_TOKEN" (id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED,
    CONSTRAINT "CK_REFRESH_TOKEN_EXPIRACION" CHECK (expira_en > creado_en),
    CONSTRAINT "CK_REFRESH_TOKEN_REVOCACION" CHECK (revocado_en IS NULL OR revocado_en >= creado_en),
    CONSTRAINT "CK_REFRESH_TOKEN_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."ROL_PERMISO" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    rol_id uuid NOT NULL,
    permiso_id uuid NOT NULL,
    activo boolean NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_ROL_PERMISO_ROL" FOREIGN KEY (rol_id)
        REFERENCES "SIGERSA"."ROL" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_ROL_PERMISO_PERMISO" FOREIGN KEY (permiso_id)
        REFERENCES "SIGERSA"."PERMISO" (id) ON DELETE RESTRICT,
    CONSTRAINT "UQ_ROL_PERMISO" UNIQUE (rol_id, permiso_id),
    CONSTRAINT "CK_ROL_PERMISO_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."USUARIO_ROL" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    usuario_id uuid NOT NULL,
    rol_id uuid NOT NULL,
    empresa_ambito_id uuid,
    establecimiento_ambito_id uuid,
    vigente_desde timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    vigente_hasta timestamptz,
    activo boolean NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_USUARIO_ROL_USUARIO" FOREIGN KEY (usuario_id)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_USUARIO_ROL_ROL" FOREIGN KEY (rol_id)
        REFERENCES "SIGERSA"."ROL" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_USUARIO_ROL_EMPRESA" FOREIGN KEY (empresa_ambito_id)
        REFERENCES "SIGERSA"."EMPRESA" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_USUARIO_ROL_ESTABLECIMIENTO" FOREIGN KEY (establecimiento_ambito_id)
        REFERENCES "SIGERSA"."ESTABLECIMIENTO" (id) ON DELETE RESTRICT,
    CONSTRAINT "CK_USUARIO_ROL_VIGENCIA" CHECK (vigente_hasta IS NULL OR vigente_hasta > vigente_desde),
    CONSTRAINT "CK_USUARIO_ROL_VERSION" CHECK (version_fila > 0)
);

CREATE UNIQUE INDEX "UQ_USUARIO_ROL_AMBITO"
    ON "SIGERSA"."USUARIO_ROL" (usuario_id, rol_id, empresa_ambito_id, establecimiento_ambito_id)
    NULLS NOT DISTINCT;

CREATE TABLE "SIGERSA"."ESTABLECIMIENTO_MERCADO" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    establecimiento_id uuid NOT NULL,
    mercado_objetivo_id uuid NOT NULL,
    activo boolean NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_EST_MERCADO_ESTABLECIMIENTO" FOREIGN KEY (establecimiento_id)
        REFERENCES "SIGERSA"."ESTABLECIMIENTO" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_EST_MERCADO_MERCADO" FOREIGN KEY (mercado_objetivo_id)
        REFERENCES "SIGERSA"."MERCADO_OBJETIVO" (id) ON DELETE RESTRICT,
    CONSTRAINT "UQ_ESTABLECIMIENTO_MERCADO" UNIQUE (establecimiento_id, mercado_objetivo_id),
    CONSTRAINT "CK_EST_MERCADO_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."ESTABLECIMIENTO_PRODUCTO" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    establecimiento_id uuid NOT NULL,
    subcategoria_alimento_id uuid NOT NULL,
    descripcion_producto varchar(300) NOT NULL,
    volumen_mensual numeric(18, 4),
    unidad_medida varchar(30),
    activo boolean NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_EST_PRODUCTO_ESTABLECIMIENTO" FOREIGN KEY (establecimiento_id)
        REFERENCES "SIGERSA"."ESTABLECIMIENTO" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_EST_PRODUCTO_SUBCATEGORIA" FOREIGN KEY (subcategoria_alimento_id)
        REFERENCES "SIGERSA"."SUBCATEGORIA_ALIMENTO" (id) ON DELETE RESTRICT,
    CONSTRAINT "UQ_ESTABLECIMIENTO_PRODUCTO" UNIQUE (establecimiento_id, subcategoria_alimento_id, descripcion_producto),
    CONSTRAINT "CK_EST_PRODUCTO_VOLUMEN" CHECK (volumen_mensual IS NULL OR volumen_mensual >= 0),
    CONSTRAINT "CK_EST_PRODUCTO_VERSION" CHECK (version_fila > 0)
);

-- =============================================================================
-- D. Reglas versionadas y núcleo dinámico de fichas
-- =============================================================================

CREATE TABLE "SIGERSA"."REGLA_CALIFICACION_VERSION" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo varchar(50) NOT NULL,
    version integer NOT NULL,
    nombre varchar(200) NOT NULL,
    porcentaje_aprobacion numeric(7, 4) NOT NULL,
    max_nc_criticas integer NOT NULL DEFAULT 0,
    max_nc_mayores integer NOT NULL DEFAULT 0,
    estado varchar(20) NOT NULL DEFAULT 'BORRADOR',
    definicion jsonb NOT NULL,
    hash_definicion varchar(128),
    publicado_en timestamptz,
    publicado_por uuid,
    vigente_desde timestamptz,
    vigente_hasta timestamptz,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "UQ_REGLA_CALIFICACION_VERSION" UNIQUE (codigo, version),
    CONSTRAINT "FK_REGLA_CALIFICACION_PUBLICADOR" FOREIGN KEY (publicado_por)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "CK_REGLA_CALIFICACION_NUM_VERSION" CHECK (version > 0),
    CONSTRAINT "CK_REGLA_CALIFICACION_PORCENTAJE" CHECK (porcentaje_aprobacion BETWEEN 0 AND 100),
    CONSTRAINT "CK_REGLA_CALIFICACION_NC" CHECK (max_nc_criticas >= 0 AND max_nc_mayores >= 0),
    CONSTRAINT "CK_REGLA_CALIFICACION_ESTADO" CHECK (estado IN ('BORRADOR', 'PUBLICADA', 'RETIRADA')),
    CONSTRAINT "CK_REGLA_CALIFICACION_JSON" CHECK (jsonb_typeof(definicion) = 'object'),
    CONSTRAINT "CK_REGLA_CALIFICACION_PUBLICACION" CHECK (
        estado <> 'PUBLICADA' OR (publicado_en IS NOT NULL AND publicado_por IS NOT NULL AND hash_definicion IS NOT NULL)
    ),
    CONSTRAINT "CK_REGLA_CALIFICACION_VIGENCIA" CHECK (vigente_hasta IS NULL OR vigente_desde IS NULL OR vigente_hasta > vigente_desde),
    CONSTRAINT "CK_REGLA_CALIFICACION_FILA" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."REGLA_RIESGO_VERSION" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo varchar(50) NOT NULL,
    version integer NOT NULL,
    nombre varchar(200) NOT NULL,
    estado varchar(20) NOT NULL DEFAULT 'BORRADOR',
    formula_total text NOT NULL,
    definicion jsonb NOT NULL,
    peso_total numeric(7, 6) NOT NULL,
    hash_definicion varchar(128),
    publicado_en timestamptz,
    publicado_por uuid,
    vigente_desde timestamptz,
    vigente_hasta timestamptz,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "UQ_REGLA_RIESGO_VERSION" UNIQUE (codigo, version),
    CONSTRAINT "FK_REGLA_RIESGO_PUBLICADOR" FOREIGN KEY (publicado_por)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "CK_REGLA_RIESGO_NUM_VERSION" CHECK (version > 0),
    CONSTRAINT "CK_REGLA_RIESGO_ESTADO" CHECK (estado IN ('BORRADOR', 'PUBLICADA', 'RETIRADA')),
    CONSTRAINT "CK_REGLA_RIESGO_JSON" CHECK (jsonb_typeof(definicion) = 'object'),
    CONSTRAINT "CK_REGLA_RIESGO_PESO" CHECK (peso_total BETWEEN 0 AND 1),
    CONSTRAINT "CK_REGLA_RIESGO_PUBLICACION" CHECK (
        estado <> 'PUBLICADA'
        OR (peso_total = 1 AND publicado_en IS NOT NULL AND publicado_por IS NOT NULL AND hash_definicion IS NOT NULL)
    ),
    CONSTRAINT "CK_REGLA_RIESGO_VIGENCIA" CHECK (vigente_hasta IS NULL OR vigente_desde IS NULL OR vigente_hasta > vigente_desde),
    CONSTRAINT "CK_REGLA_RIESGO_FILA" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."FACTOR_RIESGO" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    regla_riesgo_version_id uuid NOT NULL,
    codigo varchar(50) NOT NULL,
    nombre varchar(200) NOT NULL,
    peso numeric(7, 6) NOT NULL,
    tipo_fuente varchar(50) NOT NULL,
    orden integer NOT NULL,
    obligatorio boolean NOT NULL DEFAULT true,
    activo boolean NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_FACTOR_RIESGO_REGLA" FOREIGN KEY (regla_riesgo_version_id)
        REFERENCES "SIGERSA"."REGLA_RIESGO_VERSION" (id) ON DELETE RESTRICT,
    CONSTRAINT "UQ_FACTOR_RIESGO_CODIGO" UNIQUE (regla_riesgo_version_id, codigo),
    CONSTRAINT "UQ_FACTOR_RIESGO_ORDEN" UNIQUE (regla_riesgo_version_id, orden),
    CONSTRAINT "UQ_FACTOR_RIESGO_ID_REGLA" UNIQUE (id, regla_riesgo_version_id),
    CONSTRAINT "CK_FACTOR_RIESGO_PESO" CHECK (peso BETWEEN 0 AND 1),
    CONSTRAINT "CK_FACTOR_RIESGO_ORDEN" CHECK (orden >= 0),
    CONSTRAINT "CK_FACTOR_RIESGO_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."FACTOR_RIESGO_OPCION" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    factor_riesgo_id uuid NOT NULL,
    codigo varchar(50) NOT NULL,
    nombre varchar(200) NOT NULL,
    valor_minimo numeric(18, 6),
    valor_maximo numeric(18, 6),
    incluye_minimo boolean NOT NULL DEFAULT true,
    incluye_maximo boolean NOT NULL DEFAULT true,
    puntaje numeric(12, 4) NOT NULL,
    orden integer NOT NULL,
    activo boolean NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_FACTOR_OPCION_FACTOR" FOREIGN KEY (factor_riesgo_id)
        REFERENCES "SIGERSA"."FACTOR_RIESGO" (id) ON DELETE RESTRICT,
    CONSTRAINT "UQ_FACTOR_OPCION_CODIGO" UNIQUE (factor_riesgo_id, codigo),
    CONSTRAINT "UQ_FACTOR_OPCION_ORDEN" UNIQUE (factor_riesgo_id, orden),
    CONSTRAINT "UQ_FACTOR_OPCION_ID_FACTOR" UNIQUE (id, factor_riesgo_id),
    CONSTRAINT "CK_FACTOR_OPCION_RANGO" CHECK (valor_maximo IS NULL OR valor_minimo IS NULL OR valor_maximo >= valor_minimo),
    CONSTRAINT "CK_FACTOR_OPCION_PUNTAJE" CHECK (puntaje BETWEEN 1 AND 3),
    CONSTRAINT "CK_FACTOR_OPCION_ORDEN" CHECK (orden >= 0),
    CONSTRAINT "CK_FACTOR_OPCION_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."RANGO_RIESGO" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    regla_riesgo_version_id uuid NOT NULL,
    codigo varchar(50) NOT NULL,
    nivel_riesgo "SIGERSA"."NIVEL_RIESGO" NOT NULL,
    limite_inferior numeric(12, 4) NOT NULL,
    incluye_inferior boolean NOT NULL DEFAULT true,
    limite_superior numeric(12, 4),
    incluye_superior boolean NOT NULL DEFAULT true,
    frecuencia varchar(30) NOT NULL,
    meses_frecuencia integer NOT NULL,
    orden integer NOT NULL,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_RANGO_RIESGO_REGLA" FOREIGN KEY (regla_riesgo_version_id)
        REFERENCES "SIGERSA"."REGLA_RIESGO_VERSION" (id) ON DELETE RESTRICT,
    CONSTRAINT "UQ_RANGO_RIESGO_CODIGO" UNIQUE (regla_riesgo_version_id, codigo),
    CONSTRAINT "UQ_RANGO_RIESGO_ORDEN" UNIQUE (regla_riesgo_version_id, orden),
    CONSTRAINT "CK_RANGO_RIESGO_NIVEL" CHECK (nivel_riesgo <> 'NO_CALCULABLE'),
    CONSTRAINT "CK_RANGO_RIESGO_LIMITES" CHECK (limite_superior IS NULL OR limite_superior > limite_inferior),
    CONSTRAINT "CK_RANGO_RIESGO_FRECUENCIA" CHECK (frecuencia IN ('ANUAL', 'SEMESTRAL', 'TRIMESTRAL')),
    CONSTRAINT "CK_RANGO_RIESGO_MESES" CHECK (meses_frecuencia > 0),
    CONSTRAINT "CK_RANGO_RIESGO_ORDEN" CHECK (orden >= 0),
    CONSTRAINT "CK_RANGO_RIESGO_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."SUBCATEGORIA_RIESGO_VERSION" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    subcategoria_alimento_id uuid NOT NULL,
    regla_riesgo_version_id uuid NOT NULL,
    nivel_riesgo "SIGERSA"."NIVEL_RIESGO" NOT NULL,
    valor_riesgo numeric(12, 4) NOT NULL,
    fuente varchar(250),
    validado boolean NOT NULL DEFAULT false,
    vigente_desde timestamptz,
    vigente_hasta timestamptz,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_SUBCAT_RIESGO_SUBCATEGORIA" FOREIGN KEY (subcategoria_alimento_id)
        REFERENCES "SIGERSA"."SUBCATEGORIA_ALIMENTO" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_SUBCAT_RIESGO_REGLA" FOREIGN KEY (regla_riesgo_version_id)
        REFERENCES "SIGERSA"."REGLA_RIESGO_VERSION" (id) ON DELETE RESTRICT,
    CONSTRAINT "UQ_SUBCAT_RIESGO_VERSION" UNIQUE (subcategoria_alimento_id, regla_riesgo_version_id),
    CONSTRAINT "CK_SUBCAT_RIESGO_NIVEL" CHECK (nivel_riesgo <> 'NO_CALCULABLE'),
    CONSTRAINT "CK_SUBCAT_RIESGO_VALOR" CHECK (valor_riesgo BETWEEN 1 AND 3),
    CONSTRAINT "CK_SUBCAT_RIESGO_VIGENCIA" CHECK (vigente_hasta IS NULL OR vigente_desde IS NULL OR vigente_hasta > vigente_desde),
    CONSTRAINT "CK_SUBCAT_RIESGO_FILA" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."FICHA_INSPECCION" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    ficha_raiz_id uuid NOT NULL,
    version_anterior_id uuid,
    codigo varchar(50) NOT NULL,
    nombre varchar(200) NOT NULL,
    descripcion text,
    version integer NOT NULL,
    estado "SIGERSA"."ESTADO_FICHA" NOT NULL DEFAULT 'BORRADOR',
    vigente_desde timestamptz,
    vigente_hasta timestamptz,
    puntaje_maximo_referencia numeric(12, 4),
    regla_calificacion_id uuid NOT NULL,
    hash_definicion varchar(128),
    definicion_snapshot jsonb,
    publicado_en timestamptz,
    publicado_por uuid,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "UQ_FICHA_CODIGO_VERSION" UNIQUE (codigo, version),
    CONSTRAINT "UQ_FICHA_RAIZ_VERSION" UNIQUE (ficha_raiz_id, version),
    CONSTRAINT "FK_FICHA_RAIZ" FOREIGN KEY (ficha_raiz_id)
        REFERENCES "SIGERSA"."FICHA_INSPECCION" (id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED,
    CONSTRAINT "FK_FICHA_VERSION_ANTERIOR" FOREIGN KEY (version_anterior_id)
        REFERENCES "SIGERSA"."FICHA_INSPECCION" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_FICHA_REGLA_CALIFICACION" FOREIGN KEY (regla_calificacion_id)
        REFERENCES "SIGERSA"."REGLA_CALIFICACION_VERSION" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_FICHA_PUBLICADOR" FOREIGN KEY (publicado_por)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "CK_FICHA_VERSION" CHECK (version > 0),
    CONSTRAINT "CK_FICHA_PUNTAJE" CHECK (puntaje_maximo_referencia IS NULL OR puntaje_maximo_referencia >= 0),
    CONSTRAINT "CK_FICHA_VIGENCIA" CHECK (vigente_hasta IS NULL OR vigente_desde IS NULL OR vigente_hasta > vigente_desde),
    CONSTRAINT "CK_FICHA_PUBLICACION" CHECK (
        estado <> 'PUBLICADA'
        OR (
            vigente_desde IS NOT NULL
            AND publicado_en IS NOT NULL
            AND publicado_por IS NOT NULL
            AND hash_definicion IS NOT NULL
            AND definicion_snapshot IS NOT NULL
        )
    ),
    CONSTRAINT "CK_FICHA_SNAPSHOT" CHECK (definicion_snapshot IS NULL OR jsonb_typeof(definicion_snapshot) = 'object'),
    CONSTRAINT "CK_FICHA_VERSION_FILA" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."ITEM_FICHA" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    ficha_inspeccion_id uuid NOT NULL,
    parent_item_ficha_id uuid,
    codigo varchar(80) NOT NULL,
    tipo_item "SIGERSA"."TIPO_ITEM_FICHA" NOT NULL,
    titulo varchar(500) NOT NULL,
    descripcion text,
    ayuda text,
    es_evaluable boolean NOT NULL DEFAULT false,
    tipo_respuesta "SIGERSA"."TIPO_RESPUESTA",
    nivel smallint NOT NULL,
    orden integer NOT NULL,
    obligatorio boolean NOT NULL DEFAULT false,
    permite_no_aplica boolean NOT NULL DEFAULT false,
    peso numeric(12, 6),
    puntaje_maximo numeric(12, 4),
    nivel_criticidad_id uuid,
    requiere_observacion boolean NOT NULL DEFAULT false,
    requiere_evidencia boolean NOT NULL DEFAULT false,
    reglas_visibilidad jsonb,
    reglas_validacion jsonb,
    activo boolean NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_ITEM_FICHA" FOREIGN KEY (ficha_inspeccion_id)
        REFERENCES "SIGERSA"."FICHA_INSPECCION" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_ITEM_PADRE_MISMA_FICHA" FOREIGN KEY (parent_item_ficha_id, ficha_inspeccion_id)
        REFERENCES "SIGERSA"."ITEM_FICHA" (id, ficha_inspeccion_id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED,
    CONSTRAINT "FK_ITEM_CRITICIDAD" FOREIGN KEY (nivel_criticidad_id)
        REFERENCES "SIGERSA"."NIVEL_CRITICIDAD" (id) ON DELETE RESTRICT,
    CONSTRAINT "UQ_ITEM_FICHA_CODIGO" UNIQUE (ficha_inspeccion_id, codigo),
    CONSTRAINT "UQ_ITEM_ID_FICHA" UNIQUE (id, ficha_inspeccion_id),
    CONSTRAINT "CK_ITEM_NIVEL" CHECK (nivel >= 0),
    CONSTRAINT "CK_ITEM_ORDEN" CHECK (orden >= 0),
    CONSTRAINT "CK_ITEM_PESO" CHECK (peso IS NULL OR peso BETWEEN 0 AND 1),
    CONSTRAINT "CK_ITEM_PUNTAJE" CHECK (puntaje_maximo IS NULL OR puntaje_maximo >= 0),
    CONSTRAINT "CK_ITEM_EVALUABLE" CHECK (
        (es_evaluable = true AND tipo_respuesta IS NOT NULL)
        OR (es_evaluable = false AND tipo_respuesta IS NULL AND puntaje_maximo IS NULL)
    ),
    CONSTRAINT "CK_ITEM_REGLAS_VISIBILIDAD" CHECK (reglas_visibilidad IS NULL OR jsonb_typeof(reglas_visibilidad) = 'object'),
    CONSTRAINT "CK_ITEM_REGLAS_VALIDACION" CHECK (reglas_validacion IS NULL OR jsonb_typeof(reglas_validacion) = 'object'),
    CONSTRAINT "CK_ITEM_VERSION" CHECK (version_fila > 0)
);

CREATE UNIQUE INDEX "UQ_ITEM_FICHA_ORDEN_HERMANO"
    ON "SIGERSA"."ITEM_FICHA" (ficha_inspeccion_id, parent_item_ficha_id, orden)
    NULLS NOT DISTINCT
    WHERE activo = true;

CREATE TABLE "SIGERSA"."OPCION_RESPUESTA" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo varchar(50) NOT NULL,
    nombre varchar(150) NOT NULL,
    descripcion text,
    tipo_semantico varchar(30) NOT NULL,
    activo boolean NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "UQ_OPCION_RESPUESTA_CODIGO" UNIQUE (codigo),
    CONSTRAINT "CK_OPCION_TIPO_SEMANTICO" CHECK (tipo_semantico IN ('CUMPLIMIENTO', 'BOOLEANO', 'RIESGO', 'OTRO')),
    CONSTRAINT "CK_OPCION_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."ITEM_OPCION" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    item_ficha_id uuid NOT NULL,
    opcion_respuesta_id uuid NOT NULL,
    orden integer NOT NULL,
    puntaje numeric(12, 4),
    excluye_denominador boolean NOT NULL DEFAULT false,
    genera_no_conformidad boolean NOT NULL DEFAULT false,
    nivel_criticidad_id uuid,
    requiere_observacion boolean NOT NULL DEFAULT false,
    requiere_evidencia boolean NOT NULL DEFAULT false,
    valor_riesgo numeric(12, 4),
    activo boolean NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_ITEM_OPCION_ITEM" FOREIGN KEY (item_ficha_id)
        REFERENCES "SIGERSA"."ITEM_FICHA" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_ITEM_OPCION_OPCION" FOREIGN KEY (opcion_respuesta_id)
        REFERENCES "SIGERSA"."OPCION_RESPUESTA" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_ITEM_OPCION_CRITICIDAD" FOREIGN KEY (nivel_criticidad_id)
        REFERENCES "SIGERSA"."NIVEL_CRITICIDAD" (id) ON DELETE RESTRICT,
    CONSTRAINT "UQ_ITEM_OPCION" UNIQUE (item_ficha_id, opcion_respuesta_id),
    CONSTRAINT "UQ_ITEM_OPCION_ID_ITEM" UNIQUE (id, item_ficha_id),
    CONSTRAINT "UQ_ITEM_OPCION_ORDEN" UNIQUE (item_ficha_id, orden),
    CONSTRAINT "CK_ITEM_OPCION_ORDEN" CHECK (orden >= 0),
    CONSTRAINT "CK_ITEM_OPCION_PUNTAJE" CHECK (puntaje IS NULL OR puntaje >= 0),
    CONSTRAINT "CK_ITEM_OPCION_RIESGO" CHECK (valor_riesgo IS NULL OR valor_riesgo >= 0),
    CONSTRAINT "CK_ITEM_OPCION_VERSION" CHECK (version_fila > 0)
);

-- =============================================================================
-- E. Núcleo transaccional: solicitudes, casos, evaluaciones y hallazgos
-- =============================================================================

CREATE TABLE "SIGERSA"."SOLICITUD" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    numero varchar(50),
    idempotency_key uuid NOT NULL,
    empresa_id uuid NOT NULL,
    establecimiento_id uuid,
    solicitante_id uuid NOT NULL,
    motivo_inspeccion_id uuid NOT NULL,
    motivo_detalle text,
    tipo_establecimiento varchar(100),
    observaciones text,
    documentacion_requerida_snapshot jsonb NOT NULL DEFAULT '{}'::jsonb,
    estado varchar(30) NOT NULL DEFAULT 'BORRADOR',
    enviada_en timestamptz,
    cancelada_en timestamptz,
    activo boolean NOT NULL DEFAULT true,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "UQ_SOLICITUD_NUMERO" UNIQUE (numero),
    CONSTRAINT "UQ_SOLICITUD_IDEMPOTENCIA" UNIQUE (idempotency_key),
    CONSTRAINT "FK_SOLICITUD_EMPRESA" FOREIGN KEY (empresa_id)
        REFERENCES "SIGERSA"."EMPRESA" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_SOLICITUD_ESTABLECIMIENTO_EMPRESA" FOREIGN KEY (establecimiento_id, empresa_id)
        REFERENCES "SIGERSA"."ESTABLECIMIENTO" (id, empresa_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_SOLICITUD_SOLICITANTE" FOREIGN KEY (solicitante_id)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_SOLICITUD_MOTIVO" FOREIGN KEY (motivo_inspeccion_id)
        REFERENCES "SIGERSA"."MOTIVO_INSPECCION" (id) ON DELETE RESTRICT,
    CONSTRAINT "CK_SOLICITUD_ESTADO" CHECK (estado IN ('BORRADOR', 'ENVIADA', 'CANCELADA', 'RECHAZADA')),
    CONSTRAINT "CK_SOLICITUD_DOCUMENTACION" CHECK (jsonb_typeof(documentacion_requerida_snapshot) = 'object'),
    CONSTRAINT "CK_SOLICITUD_ENVIO" CHECK (estado <> 'ENVIADA' OR (numero IS NOT NULL AND enviada_en IS NOT NULL)),
    CONSTRAINT "CK_SOLICITUD_CANCELACION" CHECK (estado <> 'CANCELADA' OR cancelada_en IS NOT NULL),
    CONSTRAINT "CK_SOLICITUD_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."ALERTA_LAPCH" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    numero_alerta varchar(50) NOT NULL,
    fecha_alerta timestamptz NOT NULL,
    empresa_id uuid,
    establecimiento_id uuid,
    producto varchar(300) NOT NULL,
    descripcion text NOT NULL,
    prioridad smallint NOT NULL DEFAULT 3,
    resultado varchar(30),
    registrada_por uuid NOT NULL,
    documentos_snapshot jsonb NOT NULL DEFAULT '[]'::jsonb,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "UQ_ALERTA_LAPCH_NUMERO" UNIQUE (numero_alerta),
    CONSTRAINT "FK_ALERTA_EMPRESA" FOREIGN KEY (empresa_id)
        REFERENCES "SIGERSA"."EMPRESA" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_ALERTA_ESTABLECIMIENTO" FOREIGN KEY (establecimiento_id)
        REFERENCES "SIGERSA"."ESTABLECIMIENTO" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_ALERTA_REGISTRADOR" FOREIGN KEY (registrada_por)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "CK_ALERTA_PRIORIDAD" CHECK (prioridad BETWEEN 1 AND 5),
    CONSTRAINT "CK_ALERTA_RESULTADO" CHECK (resultado IS NULL OR resultado IN ('PROCEDE_EVALUACION', 'NO_PROCEDE', 'REQUIERE_INFORMACION')),
    CONSTRAINT "CK_ALERTA_DOCUMENTOS" CHECK (jsonb_typeof(documentos_snapshot) = 'array'),
    CONSTRAINT "CK_ALERTA_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."DENUNCIA" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    numero varchar(50) NOT NULL,
    establecimiento_id uuid,
    tipo_denuncia varchar(100) NOT NULL,
    fecha_recepcion timestamptz NOT NULL,
    canal varchar(50) NOT NULL,
    denunciante_cifrado text,
    es_anonima boolean NOT NULL DEFAULT false,
    es_confidencial boolean NOT NULL DEFAULT true,
    descripcion text NOT NULL,
    resultado varchar(30),
    registrada_por uuid NOT NULL,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "UQ_DENUNCIA_NUMERO" UNIQUE (numero),
    CONSTRAINT "FK_DENUNCIA_ESTABLECIMIENTO" FOREIGN KEY (establecimiento_id)
        REFERENCES "SIGERSA"."ESTABLECIMIENTO" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_DENUNCIA_REGISTRADOR" FOREIGN KEY (registrada_por)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "CK_DENUNCIA_RESULTADO" CHECK (resultado IS NULL OR resultado IN ('PROCEDE', 'NO_PROCEDE', 'REMITIDA_OTRO_PROCESO', 'REQUIERE_INFORMACION')),
    CONSTRAINT "CK_DENUNCIA_ANONIMATO" CHECK (es_anonima = false OR denunciante_cifrado IS NULL),
    CONSTRAINT "CK_DENUNCIA_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."CASO" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    numero varchar(50) NOT NULL,
    solicitud_id uuid,
    alerta_lapch_id uuid,
    denuncia_id uuid,
    empresa_id uuid NOT NULL,
    establecimiento_id uuid NOT NULL,
    motivo_inspeccion_id uuid,
    origen varchar(30) NOT NULL,
    estado "SIGERSA"."ESTADO_CASO" NOT NULL DEFAULT 'BORRADOR',
    prioridad smallint NOT NULL DEFAULT 3,
    responsable_actual_id uuid,
    decision_analisis varchar(30),
    motivo_decision text,
    abierto_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    cerrado_en timestamptz,
    proxima_inspeccion_en timestamptz,
    metadatos_origen jsonb NOT NULL DEFAULT '{}'::jsonb,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "UQ_CASO_NUMERO" UNIQUE (numero),
    CONSTRAINT "UQ_CASO_SOLICITUD" UNIQUE (solicitud_id),
    CONSTRAINT "UQ_CASO_ALERTA" UNIQUE (alerta_lapch_id),
    CONSTRAINT "UQ_CASO_DENUNCIA" UNIQUE (denuncia_id),
    CONSTRAINT "UQ_CASO_ID_ESTABLECIMIENTO" UNIQUE (id, establecimiento_id),
    CONSTRAINT "FK_CASO_SOLICITUD" FOREIGN KEY (solicitud_id)
        REFERENCES "SIGERSA"."SOLICITUD" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_CASO_ALERTA" FOREIGN KEY (alerta_lapch_id)
        REFERENCES "SIGERSA"."ALERTA_LAPCH" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_CASO_DENUNCIA" FOREIGN KEY (denuncia_id)
        REFERENCES "SIGERSA"."DENUNCIA" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_CASO_EMPRESA" FOREIGN KEY (empresa_id)
        REFERENCES "SIGERSA"."EMPRESA" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_CASO_ESTABLECIMIENTO_EMPRESA" FOREIGN KEY (establecimiento_id, empresa_id)
        REFERENCES "SIGERSA"."ESTABLECIMIENTO" (id, empresa_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_CASO_RESPONSABLE" FOREIGN KEY (responsable_actual_id)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_CASO_MOTIVO" FOREIGN KEY (motivo_inspeccion_id)
        REFERENCES "SIGERSA"."MOTIVO_INSPECCION" (id) ON DELETE RESTRICT,
    CONSTRAINT "CK_CASO_ORIGEN" CHECK (origen IN ('SOLICITUD_EMPRESA', 'PROGRAMACION', 'ALERTA_LAPCH', 'DENUNCIA')),
    CONSTRAINT "CK_CASO_PRIORIDAD" CHECK (prioridad BETWEEN 1 AND 5),
    CONSTRAINT "CK_CASO_DECISION" CHECK (decision_analisis IS NULL OR decision_analisis IN ('PROCEDE', 'NO_PROCEDE', 'REQUIERE_INFORMACION')),
    CONSTRAINT "CK_CASO_CIERRE" CHECK (cerrado_en IS NULL OR cerrado_en >= abierto_en),
    CONSTRAINT "CK_CASO_METADATOS" CHECK (jsonb_typeof(metadatos_origen) = 'object'),
    CONSTRAINT "CK_CASO_ORIGEN_EXCLUSIVO" CHECK (
        (origen = 'SOLICITUD_EMPRESA' AND solicitud_id IS NOT NULL AND alerta_lapch_id IS NULL AND denuncia_id IS NULL)
        OR (origen = 'ALERTA_LAPCH' AND solicitud_id IS NULL AND alerta_lapch_id IS NOT NULL AND denuncia_id IS NULL)
        OR (origen = 'DENUNCIA' AND solicitud_id IS NULL AND alerta_lapch_id IS NULL AND denuncia_id IS NOT NULL)
        OR (origen = 'PROGRAMACION' AND solicitud_id IS NULL AND alerta_lapch_id IS NULL AND denuncia_id IS NULL)
    ),
    CONSTRAINT "CK_CASO_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."CASO_TRANSICION" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    caso_id uuid NOT NULL,
    estado_anterior "SIGERSA"."ESTADO_CASO",
    estado_nuevo "SIGERSA"."ESTADO_CASO" NOT NULL,
    motivo text,
    ejecutado_por uuid NOT NULL,
    ejecutado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    correlacion_id uuid,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_CASO_TRANSICION_CASO" FOREIGN KEY (caso_id)
        REFERENCES "SIGERSA"."CASO" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_CASO_TRANSICION_EJECUTOR" FOREIGN KEY (ejecutado_por)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "CK_CASO_TRANSICION_CAMBIO" CHECK (estado_anterior IS NULL OR estado_anterior <> estado_nuevo),
    CONSTRAINT "CK_CASO_TRANSICION_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."PROGRAMACION" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    caso_id uuid NOT NULL,
    ficha_inspeccion_id uuid,
    inicio_programado timestamptz NOT NULL,
    fin_programado timestamptz NOT NULL,
    prioridad smallint NOT NULL DEFAULT 3,
    estado varchar(30) NOT NULL DEFAULT 'PROGRAMADA',
    observaciones text,
    motivo_cambio text,
    programado_por uuid NOT NULL,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_PROGRAMACION_CASO" FOREIGN KEY (caso_id)
        REFERENCES "SIGERSA"."CASO" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_PROGRAMACION_FICHA" FOREIGN KEY (ficha_inspeccion_id)
        REFERENCES "SIGERSA"."FICHA_INSPECCION" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_PROGRAMACION_USUARIO" FOREIGN KEY (programado_por)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "UQ_PROGRAMACION_ID_CASO" UNIQUE (id, caso_id),
    CONSTRAINT "CK_PROGRAMACION_FECHAS" CHECK (fin_programado > inicio_programado),
    CONSTRAINT "CK_PROGRAMACION_PRIORIDAD" CHECK (prioridad BETWEEN 1 AND 5),
    CONSTRAINT "CK_PROGRAMACION_ESTADO" CHECK (estado IN ('PROGRAMADA', 'REPROGRAMADA', 'CANCELADA', 'COMPLETADA')),
    CONSTRAINT "CK_PROGRAMACION_CAMBIO" CHECK (estado NOT IN ('REPROGRAMADA', 'CANCELADA') OR motivo_cambio IS NOT NULL),
    CONSTRAINT "CK_PROGRAMACION_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."ASIGNACION" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    programacion_id uuid NOT NULL,
    evaluador_id uuid NOT NULL,
    es_principal boolean NOT NULL DEFAULT false,
    estado varchar(20) NOT NULL DEFAULT 'ACTIVA',
    motivo_asignacion text,
    asignado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    revocado_en timestamptz,
    asignado_por uuid NOT NULL,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_ASIGNACION_PROGRAMACION" FOREIGN KEY (programacion_id)
        REFERENCES "SIGERSA"."PROGRAMACION" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_ASIGNACION_EVALUADOR" FOREIGN KEY (evaluador_id)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_ASIGNACION_AUTOR" FOREIGN KEY (asignado_por)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "UQ_ASIGNACION_PROGRAMACION_EVALUADOR" UNIQUE (programacion_id, evaluador_id),
    CONSTRAINT "CK_ASIGNACION_ESTADO" CHECK (estado IN ('ACTIVA', 'REVOCADA')),
    CONSTRAINT "CK_ASIGNACION_REVOCACION" CHECK (
        (estado = 'ACTIVA' AND revocado_en IS NULL)
        OR (estado = 'REVOCADA' AND revocado_en IS NOT NULL)
    ),
    CONSTRAINT "CK_ASIGNACION_VERSION" CHECK (version_fila > 0)
);

CREATE UNIQUE INDEX "UQ_ASIGNACION_PRINCIPAL_ACTIVA"
    ON "SIGERSA"."ASIGNACION" (programacion_id)
    WHERE es_principal = true AND estado = 'ACTIVA';

CREATE TABLE "SIGERSA"."EVALUACION" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    numero varchar(50) NOT NULL,
    caso_id uuid NOT NULL,
    programacion_id uuid,
    establecimiento_id uuid NOT NULL,
    ficha_inspeccion_id uuid NOT NULL,
    evaluador_principal_id uuid NOT NULL,
    estado "SIGERSA"."ESTADO_EVALUACION" NOT NULL DEFAULT 'ASIGNADA',
    alcance varchar(30) NOT NULL DEFAULT 'COMPLETO',
    programada_inicio_en timestamptz,
    programada_fin_en timestamptz,
    iniciada_en timestamptz,
    finalizada_en timestamptz,
    enviada_en timestamptz,
    aprobada_en timestamptz,
    cerrada_en timestamptz,
    latitud_inicio numeric(9, 6),
    longitud_inicio numeric(9, 6),
    precision_m numeric(12, 3),
    puntaje_obtenido numeric(14, 4),
    puntaje_aplicable numeric(14, 4),
    porcentaje_cumplimiento numeric(7, 4),
    riesgo_producto numeric(12, 4),
    riesgo_establecimiento numeric(12, 4),
    riesgo_total numeric(12, 4),
    nivel_riesgo "SIGERSA"."NIVEL_RIESGO",
    frecuencia varchar(30),
    version_regla_riesgo_id uuid NOT NULL,
    snapshot_calculo jsonb,
    hash_calculo varchar(128),
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "UQ_EVALUACION_NUMERO" UNIQUE (numero),
    CONSTRAINT "FK_EVALUACION_CASO_ESTABLECIMIENTO" FOREIGN KEY (caso_id, establecimiento_id)
        REFERENCES "SIGERSA"."CASO" (id, establecimiento_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_EVALUACION_PROGRAMACION_CASO" FOREIGN KEY (programacion_id, caso_id)
        REFERENCES "SIGERSA"."PROGRAMACION" (id, caso_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_EVALUACION_FICHA" FOREIGN KEY (ficha_inspeccion_id)
        REFERENCES "SIGERSA"."FICHA_INSPECCION" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_EVALUACION_EVALUADOR" FOREIGN KEY (evaluador_principal_id)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_EVALUACION_REGLA_RIESGO" FOREIGN KEY (version_regla_riesgo_id)
        REFERENCES "SIGERSA"."REGLA_RIESGO_VERSION" (id) ON DELETE RESTRICT,
    CONSTRAINT "CK_EVALUACION_PROGRAMACION" CHECK (programada_fin_en IS NULL OR programada_inicio_en IS NULL OR programada_fin_en > programada_inicio_en),
    CONSTRAINT "CK_EVALUACION_HITOS" CHECK (
        (finalizada_en IS NULL OR iniciada_en IS NULL OR finalizada_en >= iniciada_en)
        AND (enviada_en IS NULL OR iniciada_en IS NULL OR enviada_en >= iniciada_en)
        AND (aprobada_en IS NULL OR enviada_en IS NULL OR aprobada_en >= enviada_en)
        AND (cerrada_en IS NULL OR iniciada_en IS NULL OR cerrada_en >= iniciada_en)
    ),
    CONSTRAINT "CK_EVALUACION_LATITUD" CHECK (latitud_inicio IS NULL OR latitud_inicio BETWEEN -90 AND 90),
    CONSTRAINT "CK_EVALUACION_LONGITUD" CHECK (longitud_inicio IS NULL OR longitud_inicio BETWEEN -180 AND 180),
    CONSTRAINT "CK_EVALUACION_PRECISION" CHECK (precision_m IS NULL OR precision_m >= 0),
    CONSTRAINT "CK_EVALUACION_COORDENADAS" CHECK ((latitud_inicio IS NULL) = (longitud_inicio IS NULL)),
    CONSTRAINT "CK_EVALUACION_PUNTAJES" CHECK (
        (puntaje_obtenido IS NULL OR puntaje_obtenido >= 0)
        AND (puntaje_aplicable IS NULL OR puntaje_aplicable >= 0)
        AND (puntaje_obtenido IS NULL OR puntaje_aplicable IS NULL OR puntaje_obtenido <= puntaje_aplicable)
    ),
    CONSTRAINT "CK_EVALUACION_PORCENTAJE" CHECK (porcentaje_cumplimiento IS NULL OR porcentaje_cumplimiento BETWEEN 0 AND 100),
    CONSTRAINT "CK_EVALUACION_RIESGOS" CHECK (
        (riesgo_producto IS NULL OR riesgo_producto BETWEEN 1 AND 3)
        AND (riesgo_establecimiento IS NULL OR riesgo_establecimiento BETWEEN 1 AND 3)
        AND (riesgo_total IS NULL OR riesgo_total BETWEEN 1 AND 9)
    ),
    CONSTRAINT "CK_EVALUACION_FRECUENCIA" CHECK (frecuencia IS NULL OR frecuencia IN ('ANUAL', 'SEMESTRAL', 'TRIMESTRAL')),
    CONSTRAINT "CK_EVALUACION_ALCANCE" CHECK (alcance IN ('COMPLETO', 'FOCALIZADO', 'SEGUIMIENTO')),
    CONSTRAINT "CK_EVALUACION_SNAPSHOT" CHECK (snapshot_calculo IS NULL OR jsonb_typeof(snapshot_calculo) = 'object'),
    CONSTRAINT "CK_EVALUACION_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."EVALUACION_FACTOR_RIESGO" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    evaluacion_id uuid NOT NULL,
    factor_riesgo_id uuid NOT NULL,
    factor_riesgo_opcion_id uuid,
    valor_entrada numeric(18, 6),
    valor_texto varchar(250),
    puntaje_factor numeric(12, 4) NOT NULL,
    peso_aplicado numeric(7, 6) NOT NULL,
    valor_ponderado numeric(14, 6) NOT NULL,
    justificacion text,
    calculado_por uuid NOT NULL,
    calculado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_EVAL_FACTOR_EVALUACION" FOREIGN KEY (evaluacion_id)
        REFERENCES "SIGERSA"."EVALUACION" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_EVAL_FACTOR_FACTOR" FOREIGN KEY (factor_riesgo_id)
        REFERENCES "SIGERSA"."FACTOR_RIESGO" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_EVAL_FACTOR_OPCION" FOREIGN KEY (factor_riesgo_opcion_id, factor_riesgo_id)
        REFERENCES "SIGERSA"."FACTOR_RIESGO_OPCION" (id, factor_riesgo_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_EVAL_FACTOR_CALCULADOR" FOREIGN KEY (calculado_por)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "UQ_EVAL_FACTOR" UNIQUE (evaluacion_id, factor_riesgo_id),
    CONSTRAINT "CK_EVAL_FACTOR_ENTRADA" CHECK (num_nonnulls(valor_entrada, valor_texto) <= 1),
    CONSTRAINT "CK_EVAL_FACTOR_PUNTAJE" CHECK (puntaje_factor BETWEEN 1 AND 3),
    CONSTRAINT "CK_EVAL_FACTOR_PESO" CHECK (peso_aplicado BETWEEN 0 AND 1),
    CONSTRAINT "CK_EVAL_FACTOR_PONDERADO" CHECK (valor_ponderado = puntaje_factor * peso_aplicado),
    CONSTRAINT "CK_EVAL_FACTOR_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."RESPUESTA_USUARIO" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    evaluacion_id uuid NOT NULL,
    item_ficha_id uuid NOT NULL,
    item_opcion_id uuid,
    valor_texto text,
    valor_numero numeric(20, 6),
    valor_booleano boolean,
    valor_fecha_hora timestamptz,
    valor_json jsonb,
    observacion text,
    nivel_criticidad_id uuid,
    puntaje_obtenido numeric(14, 4),
    maximo_aplicable numeric(14, 4),
    es_no_aplica boolean NOT NULL DEFAULT false,
    respondido_por uuid NOT NULL,
    respondido_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    fecha_cliente timestamptz,
    idempotency_key uuid NOT NULL,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_RESPUESTA_EVALUACION" FOREIGN KEY (evaluacion_id)
        REFERENCES "SIGERSA"."EVALUACION" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_RESPUESTA_ITEM" FOREIGN KEY (item_ficha_id)
        REFERENCES "SIGERSA"."ITEM_FICHA" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_RESPUESTA_ITEM_OPCION" FOREIGN KEY (item_opcion_id, item_ficha_id)
        REFERENCES "SIGERSA"."ITEM_OPCION" (id, item_ficha_id) ON DELETE RESTRICT,
    CONSTRAINT "FK_RESPUESTA_CRITICIDAD" FOREIGN KEY (nivel_criticidad_id)
        REFERENCES "SIGERSA"."NIVEL_CRITICIDAD" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_RESPUESTA_AUTOR" FOREIGN KEY (respondido_por)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "UQ_RESPUESTA_EVALUACION_ITEM" UNIQUE (evaluacion_id, item_ficha_id),
    CONSTRAINT "UQ_RESPUESTA_IDEMPOTENCIA" UNIQUE (idempotency_key),
    CONSTRAINT "CK_RESPUESTA_UN_VALOR" CHECK (
        num_nonnulls(item_opcion_id, valor_texto, valor_numero, valor_booleano, valor_fecha_hora, valor_json) <= 1
    ),
    CONSTRAINT "CK_RESPUESTA_NO_APLICA" CHECK (es_no_aplica = false OR item_opcion_id IS NOT NULL),
    CONSTRAINT "CK_RESPUESTA_PUNTAJES" CHECK (
        (puntaje_obtenido IS NULL OR puntaje_obtenido >= 0)
        AND (maximo_aplicable IS NULL OR maximo_aplicable >= 0)
        AND (puntaje_obtenido IS NULL OR maximo_aplicable IS NULL OR puntaje_obtenido <= maximo_aplicable)
    ),
    CONSTRAINT "CK_RESPUESTA_JSON" CHECK (valor_json IS NULL OR jsonb_typeof(valor_json) IN ('array', 'object')),
    CONSTRAINT "CK_RESPUESTA_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."RESPUESTA_OPCION" (
    respuesta_usuario_id uuid NOT NULL,
    item_opcion_id uuid NOT NULL,
    orden integer NOT NULL,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "PK_RESPUESTA_OPCION" PRIMARY KEY (respuesta_usuario_id, item_opcion_id),
    CONSTRAINT "FK_RESPUESTA_OPCION_RESPUESTA" FOREIGN KEY (respuesta_usuario_id)
        REFERENCES "SIGERSA"."RESPUESTA_USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_RESPUESTA_OPCION_ITEM" FOREIGN KEY (item_opcion_id)
        REFERENCES "SIGERSA"."ITEM_OPCION" (id) ON DELETE RESTRICT,
    CONSTRAINT "UQ_RESPUESTA_OPCION_ORDEN" UNIQUE (respuesta_usuario_id, orden),
    CONSTRAINT "CK_RESPUESTA_OPCION_ORDEN" CHECK (orden >= 0),
    CONSTRAINT "CK_RESPUESTA_OPCION_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."NO_CONFORMIDAD" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    evaluacion_id uuid NOT NULL,
    item_ficha_id uuid NOT NULL,
    respuesta_usuario_id uuid,
    nivel_criticidad_id uuid NOT NULL,
    codigo varchar(50) NOT NULL,
    descripcion text NOT NULL,
    estado varchar(30) NOT NULL DEFAULT 'ABIERTA',
    detectada_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    cerrada_en timestamptz,
    cierre_justificacion text,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_NC_EVALUACION" FOREIGN KEY (evaluacion_id)
        REFERENCES "SIGERSA"."EVALUACION" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_NC_ITEM" FOREIGN KEY (item_ficha_id)
        REFERENCES "SIGERSA"."ITEM_FICHA" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_NC_RESPUESTA" FOREIGN KEY (respuesta_usuario_id)
        REFERENCES "SIGERSA"."RESPUESTA_USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_NC_CRITICIDAD" FOREIGN KEY (nivel_criticidad_id)
        REFERENCES "SIGERSA"."NIVEL_CRITICIDAD" (id) ON DELETE RESTRICT,
    CONSTRAINT "UQ_NC_EVALUACION_CODIGO" UNIQUE (evaluacion_id, codigo),
    CONSTRAINT "CK_NC_ESTADO" CHECK (estado IN ('ABIERTA', 'EN_CORRECCION', 'CORREGIDA', 'VERIFICADA', 'CERRADA')),
    CONSTRAINT "CK_NC_CIERRE" CHECK (cerrada_en IS NULL OR cerrada_en >= detectada_en),
    CONSTRAINT "CK_NC_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."CORRECCION" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    evaluacion_id uuid NOT NULL,
    numero_revision integer NOT NULL,
    tipo_responsable varchar(30) NOT NULL,
    asignada_a_id uuid,
    observacion_coordinador text NOT NULL,
    estado varchar(20) NOT NULL DEFAULT 'PENDIENTE',
    fecha_limite timestamptz NOT NULL,
    solicitada_por uuid NOT NULL,
    solicitada_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    enviada_en timestamptz,
    resuelta_en timestamptz,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_CORRECCION_EVALUACION" FOREIGN KEY (evaluacion_id)
        REFERENCES "SIGERSA"."EVALUACION" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_CORRECCION_ASIGNADA" FOREIGN KEY (asignada_a_id)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_CORRECCION_SOLICITANTE" FOREIGN KEY (solicitada_por)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "UQ_CORRECCION_REVISION" UNIQUE (evaluacion_id, numero_revision),
    CONSTRAINT "CK_CORRECCION_REVISION" CHECK (numero_revision > 0),
    CONSTRAINT "CK_CORRECCION_RESPONSABLE" CHECK (tipo_responsable IN ('TECNICO', 'EMPRESA')),
    CONSTRAINT "CK_CORRECCION_ESTADO" CHECK (estado IN ('PENDIENTE', 'EN_PROCESO', 'ENVIADA', 'ACEPTADA', 'RECHAZADA', 'VENCIDA')),
    CONSTRAINT "CK_CORRECCION_FECHAS" CHECK (
        fecha_limite >= solicitada_en
        AND (enviada_en IS NULL OR enviada_en >= solicitada_en)
        AND (resuelta_en IS NULL OR resuelta_en >= solicitada_en)
    ),
    CONSTRAINT "CK_CORRECCION_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."CORRECCION_CAMPO" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    correccion_id uuid NOT NULL,
    item_ficha_id uuid,
    respuesta_usuario_id uuid,
    motivo text NOT NULL,
    valor_anterior_snapshot jsonb,
    valor_nuevo_snapshot jsonb,
    estado varchar(20) NOT NULL DEFAULT 'PENDIENTE',
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_CORRECCION_CAMPO_CORRECCION" FOREIGN KEY (correccion_id)
        REFERENCES "SIGERSA"."CORRECCION" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_CORRECCION_CAMPO_ITEM" FOREIGN KEY (item_ficha_id)
        REFERENCES "SIGERSA"."ITEM_FICHA" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_CORRECCION_CAMPO_RESPUESTA" FOREIGN KEY (respuesta_usuario_id)
        REFERENCES "SIGERSA"."RESPUESTA_USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "CK_CORRECCION_CAMPO_REFERENCIA" CHECK (item_ficha_id IS NOT NULL OR respuesta_usuario_id IS NOT NULL),
    CONSTRAINT "CK_CORRECCION_CAMPO_ANTERIOR" CHECK (valor_anterior_snapshot IS NULL OR jsonb_typeof(valor_anterior_snapshot) IN ('object', 'array')),
    CONSTRAINT "CK_CORRECCION_CAMPO_NUEVO" CHECK (valor_nuevo_snapshot IS NULL OR jsonb_typeof(valor_nuevo_snapshot) IN ('object', 'array')),
    CONSTRAINT "CK_CORRECCION_CAMPO_ESTADO" CHECK (estado IN ('PENDIENTE', 'CORREGIDO', 'ACEPTADO', 'RECHAZADO')),
    CONSTRAINT "CK_CORRECCION_CAMPO_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."EVIDENCIA" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    evaluacion_id uuid NOT NULL,
    subida_por uuid NOT NULL,
    bucket_name varchar(100) NOT NULL,
    supabase_path varchar(1000) NOT NULL,
    nombre_original varchar(255) NOT NULL,
    nombre_seguro varchar(255) NOT NULL,
    file_size bigint NOT NULL,
    mime_type varchar(150) NOT NULL,
    hash varchar(128) NOT NULL,
    hash_algoritmo varchar(20) NOT NULL DEFAULT 'SHA-256',
    tipo_evidencia varchar(50) NOT NULL,
    estado_sincronizacion "SIGERSA"."ESTADO_SINCRONIZACION" NOT NULL DEFAULT 'PENDIENTE',
    fecha_dispositivo timestamptz,
    fecha_servidor timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    latitud numeric(9, 6),
    longitud numeric(9, 6),
    precision_m numeric(12, 3),
    eliminada boolean NOT NULL DEFAULT false,
    eliminada_en timestamptz,
    eliminada_por uuid,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_EVIDENCIA_EVALUACION" FOREIGN KEY (evaluacion_id)
        REFERENCES "SIGERSA"."EVALUACION" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_EVIDENCIA_AUTOR" FOREIGN KEY (subida_por)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_EVIDENCIA_ELIMINADOR" FOREIGN KEY (eliminada_por)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "UQ_EVIDENCIA_SUPABASE" UNIQUE (bucket_name, supabase_path),
    CONSTRAINT "UQ_EVIDENCIA_HASH" UNIQUE (hash),
    CONSTRAINT "CK_EVIDENCIA_BUCKET" CHECK (bucket_name <> '' AND bucket_name !~ '[/\\]'),
    CONSTRAINT "CK_EVIDENCIA_PATH" CHECK (supabase_path <> '' AND supabase_path !~ '^[/\\]'),
    CONSTRAINT "CK_EVIDENCIA_TAMANO" CHECK (file_size > 0),
    CONSTRAINT "CK_EVIDENCIA_HASH" CHECK (
        (hash_algoritmo = 'SHA-256' AND hash ~ '^[0-9A-Fa-f]{64}$')
        OR (hash_algoritmo <> 'SHA-256' AND length(hash) >= 32)
    ),
    CONSTRAINT "CK_EVIDENCIA_LATITUD" CHECK (latitud IS NULL OR latitud BETWEEN -90 AND 90),
    CONSTRAINT "CK_EVIDENCIA_LONGITUD" CHECK (longitud IS NULL OR longitud BETWEEN -180 AND 180),
    CONSTRAINT "CK_EVIDENCIA_PRECISION" CHECK (precision_m IS NULL OR precision_m >= 0),
    CONSTRAINT "CK_EVIDENCIA_COORDENADAS" CHECK ((latitud IS NULL) = (longitud IS NULL)),
    CONSTRAINT "CK_EVIDENCIA_ELIMINACION" CHECK (
        (eliminada = false AND eliminada_en IS NULL AND eliminada_por IS NULL)
        OR (eliminada = true AND eliminada_en IS NOT NULL AND eliminada_por IS NOT NULL)
    ),
    CONSTRAINT "CK_EVIDENCIA_VERSION" CHECK (version_fila > 0)
);

COMMENT ON COLUMN "SIGERSA"."EVIDENCIA".bucket_name IS
    'Bucket privado de Supabase Storage que contiene el objeto.';

COMMENT ON COLUMN "SIGERSA"."EVIDENCIA".supabase_path IS
    'Clave relativa del objeto dentro del bucket de Supabase Storage; nunca una ruta física local.';

CREATE TABLE "SIGERSA"."EVIDENCIA_RESPUESTA" (
    evidencia_id uuid NOT NULL,
    respuesta_usuario_id uuid NOT NULL,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "PK_EVIDENCIA_RESPUESTA" PRIMARY KEY (evidencia_id, respuesta_usuario_id),
    CONSTRAINT "FK_EVIDENCIA_RESPUESTA_EVIDENCIA" FOREIGN KEY (evidencia_id)
        REFERENCES "SIGERSA"."EVIDENCIA" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_EVIDENCIA_RESPUESTA_RESPUESTA" FOREIGN KEY (respuesta_usuario_id)
        REFERENCES "SIGERSA"."RESPUESTA_USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "CK_EVIDENCIA_RESPUESTA_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."EVIDENCIA_NO_CONFORMIDAD" (
    evidencia_id uuid NOT NULL,
    no_conformidad_id uuid NOT NULL,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "PK_EVIDENCIA_NC" PRIMARY KEY (evidencia_id, no_conformidad_id),
    CONSTRAINT "FK_EVIDENCIA_NC_EVIDENCIA" FOREIGN KEY (evidencia_id)
        REFERENCES "SIGERSA"."EVIDENCIA" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_EVIDENCIA_NC_NC" FOREIGN KEY (no_conformidad_id)
        REFERENCES "SIGERSA"."NO_CONFORMIDAD" (id) ON DELETE RESTRICT,
    CONSTRAINT "CK_EVIDENCIA_NC_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."EVIDENCIA_CORRECCION" (
    evidencia_id uuid NOT NULL,
    correccion_id uuid NOT NULL,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "PK_EVIDENCIA_CORRECCION" PRIMARY KEY (evidencia_id, correccion_id),
    CONSTRAINT "FK_EVIDENCIA_CORRECCION_EVIDENCIA" FOREIGN KEY (evidencia_id)
        REFERENCES "SIGERSA"."EVIDENCIA" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_EVIDENCIA_CORRECCION_CORRECCION" FOREIGN KEY (correccion_id)
        REFERENCES "SIGERSA"."CORRECCION" (id) ON DELETE RESTRICT,
    CONSTRAINT "CK_EVIDENCIA_CORRECCION_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."MEDIDA_CORRECTIVA" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    no_conformidad_id uuid NOT NULL,
    codigo varchar(50) NOT NULL,
    detalle text NOT NULL,
    responsable_usuario_id uuid,
    responsable_descripcion varchar(250),
    fecha_compromiso timestamptz NOT NULL,
    estado varchar(30) NOT NULL DEFAULT 'PENDIENTE',
    enviada_en timestamptz,
    verificada_en timestamptz,
    verificacion jsonb,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_MEDIDA_NC" FOREIGN KEY (no_conformidad_id)
        REFERENCES "SIGERSA"."NO_CONFORMIDAD" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_MEDIDA_RESPONSABLE" FOREIGN KEY (responsable_usuario_id)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "UQ_MEDIDA_NC_CODIGO" UNIQUE (no_conformidad_id, codigo),
    CONSTRAINT "CK_MEDIDA_RESPONSABLE" CHECK (responsable_usuario_id IS NOT NULL OR responsable_descripcion IS NOT NULL),
    CONSTRAINT "CK_MEDIDA_ESTADO" CHECK (estado IN ('PENDIENTE', 'EN_PROCESO', 'ENVIADA', 'ACEPTADA', 'RECHAZADA', 'VENCIDA', 'VERIFICADA')),
    CONSTRAINT "CK_MEDIDA_VERIFICACION" CHECK (verificacion IS NULL OR jsonb_typeof(verificacion) = 'object'),
    CONSTRAINT "CK_MEDIDA_VERSION" CHECK (version_fila > 0)
);

-- =============================================================================
-- F. Mensajería confiable y auditoría inmutable
-- =============================================================================

CREATE TABLE "SIGERSA"."INFORME" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    evaluacion_id uuid NOT NULL,
    numero varchar(50) NOT NULL,
    tipo varchar(30) NOT NULL,
    estado varchar(20) NOT NULL DEFAULT 'BORRADOR',
    version_actual integer NOT NULL DEFAULT 0,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_INFORME_EVALUACION" FOREIGN KEY (evaluacion_id)
        REFERENCES "SIGERSA"."EVALUACION" (id) ON DELETE RESTRICT,
    CONSTRAINT "UQ_INFORME_NUMERO" UNIQUE (numero),
    CONSTRAINT "UQ_INFORME_EVALUACION_TIPO" UNIQUE (evaluacion_id, tipo),
    CONSTRAINT "CK_INFORME_ESTADO" CHECK (estado IN ('BORRADOR', 'EMITIDO', 'ANULADO')),
    CONSTRAINT "CK_INFORME_VERSION_ACTUAL" CHECK (version_actual >= 0),
    CONSTRAINT "CK_INFORME_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."INFORME_VERSION" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    informe_id uuid NOT NULL,
    version integer NOT NULL,
    bucket_name varchar(100) NOT NULL,
    supabase_path varchar(1000) NOT NULL,
    file_size bigint NOT NULL,
    mime_type varchar(150) NOT NULL DEFAULT 'application/pdf',
    hash varchar(128) NOT NULL,
    hash_algoritmo varchar(20) NOT NULL DEFAULT 'SHA-256',
    es_oficial boolean NOT NULL DEFAULT false,
    generado_por uuid NOT NULL,
    generado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    emitido_por uuid,
    emitido_en timestamptz,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_INFORME_VERSION_INFORME" FOREIGN KEY (informe_id)
        REFERENCES "SIGERSA"."INFORME" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_INFORME_VERSION_GENERADOR" FOREIGN KEY (generado_por)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_INFORME_VERSION_EMISOR" FOREIGN KEY (emitido_por)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "UQ_INFORME_VERSION" UNIQUE (informe_id, version),
    CONSTRAINT "UQ_INFORME_VERSION_SUPABASE" UNIQUE (bucket_name, supabase_path),
    CONSTRAINT "UQ_INFORME_VERSION_HASH" UNIQUE (hash),
    CONSTRAINT "CK_INFORME_VERSION_NUMERO" CHECK (version > 0),
    CONSTRAINT "CK_INFORME_VERSION_ARCHIVO" CHECK (file_size > 0 AND bucket_name <> '' AND supabase_path <> ''),
    CONSTRAINT "CK_INFORME_VERSION_EMISION" CHECK (
        (es_oficial = false AND emitido_en IS NULL AND emitido_por IS NULL)
        OR (es_oficial = true AND emitido_en IS NOT NULL AND emitido_por IS NOT NULL)
    ),
    CONSTRAINT "CK_INFORME_VERSION_FILA" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."PLANTILLA_CORREO" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    codigo varchar(80) NOT NULL,
    version integer NOT NULL,
    asunto varchar(300) NOT NULL,
    cuerpo_html text NOT NULL,
    cuerpo_texto text NOT NULL,
    estado varchar(20) NOT NULL DEFAULT 'BORRADOR',
    vigente_desde timestamptz,
    vigente_hasta timestamptz,
    publicada_por uuid,
    publicada_en timestamptz,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_PLANTILLA_CORREO_PUBLICADOR" FOREIGN KEY (publicada_por)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "UQ_PLANTILLA_CORREO_VERSION" UNIQUE (codigo, version),
    CONSTRAINT "CK_PLANTILLA_CORREO_VERSION" CHECK (version > 0),
    CONSTRAINT "CK_PLANTILLA_CORREO_ESTADO" CHECK (estado IN ('BORRADOR', 'PUBLICADA', 'RETIRADA')),
    CONSTRAINT "CK_PLANTILLA_CORREO_PUBLICACION" CHECK (
        estado <> 'PUBLICADA' OR (publicada_por IS NOT NULL AND publicada_en IS NOT NULL AND vigente_desde IS NOT NULL)
    ),
    CONSTRAINT "CK_PLANTILLA_CORREO_VIGENCIA" CHECK (vigente_hasta IS NULL OR vigente_desde IS NULL OR vigente_hasta > vigente_desde),
    CONSTRAINT "CK_PLANTILLA_CORREO_FILA" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."OUTBOX" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    idempotency_key uuid NOT NULL,
    agregado_tipo varchar(100) NOT NULL,
    agregado_id uuid NOT NULL,
    tipo_evento varchar(200) NOT NULL,
    payload jsonb NOT NULL,
    encabezados jsonb NOT NULL DEFAULT '{}'::jsonb,
    ocurrido_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    disponible_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    intentos integer NOT NULL DEFAULT 0,
    proximo_intento_en timestamptz,
    procesado_en timestamptz,
    estado varchar(20) NOT NULL DEFAULT 'PENDIENTE',
    ultimo_error text,
    correlacion_id uuid,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "UQ_OUTBOX_IDEMPOTENCIA" UNIQUE (idempotency_key),
    CONSTRAINT "CK_OUTBOX_PAYLOAD" CHECK (jsonb_typeof(payload) = 'object'),
    CONSTRAINT "CK_OUTBOX_ENCABEZADOS" CHECK (jsonb_typeof(encabezados) = 'object'),
    CONSTRAINT "CK_OUTBOX_INTENTOS" CHECK (intentos >= 0),
    CONSTRAINT "CK_OUTBOX_ESTADO" CHECK (estado IN ('PENDIENTE', 'PROCESANDO', 'PROCESADO', 'FALLIDO')),
    CONSTRAINT "CK_OUTBOX_PROCESADO" CHECK (estado <> 'PROCESADO' OR procesado_en IS NOT NULL),
    CONSTRAINT "CK_OUTBOX_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."NOTIFICACION" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    usuario_id uuid NOT NULL,
    plantilla_correo_id uuid,
    outbox_id uuid,
    tipo varchar(80) NOT NULL,
    titulo varchar(300) NOT NULL,
    mensaje text NOT NULL,
    canal varchar(20) NOT NULL,
    estado varchar(20) NOT NULL DEFAULT 'PENDIENTE',
    recurso_tipo varchar(100),
    recurso_id uuid,
    leida_en timestamptz,
    enviada_en timestamptz,
    intentos integer NOT NULL DEFAULT 0,
    ultimo_error_seguro text,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_NOTIFICACION_USUARIO" FOREIGN KEY (usuario_id)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_NOTIFICACION_PLANTILLA" FOREIGN KEY (plantilla_correo_id)
        REFERENCES "SIGERSA"."PLANTILLA_CORREO" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_NOTIFICACION_OUTBOX" FOREIGN KEY (outbox_id)
        REFERENCES "SIGERSA"."OUTBOX" (id) ON DELETE RESTRICT,
    CONSTRAINT "UQ_NOTIFICACION_OUTBOX" UNIQUE (outbox_id),
    CONSTRAINT "CK_NOTIFICACION_CANAL" CHECK (canal IN ('INTERNA', 'CORREO')),
    CONSTRAINT "CK_NOTIFICACION_ESTADO" CHECK (estado IN ('PENDIENTE', 'ENVIADA', 'FALLIDA', 'LEIDA')),
    CONSTRAINT "CK_NOTIFICACION_INTENTOS" CHECK (intentos >= 0),
    CONSTRAINT "CK_NOTIFICACION_LECTURA" CHECK (leida_en IS NULL OR canal = 'INTERNA'),
    CONSTRAINT "CK_NOTIFICACION_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."OPERACION_SINCRONIZACION" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    usuario_id uuid NOT NULL,
    evaluacion_id uuid,
    idempotency_key uuid NOT NULL,
    dispositivo_id uuid NOT NULL,
    secuencia_cliente bigint NOT NULL,
    tipo_operacion varchar(100) NOT NULL,
    recurso_tipo varchar(100) NOT NULL,
    recurso_id uuid,
    version_base bigint,
    payload jsonb NOT NULL,
    payload_hash varchar(128) NOT NULL,
    estado varchar(20) NOT NULL DEFAULT 'RECIBIDA',
    codigo_resultado varchar(100),
    detalle_error_seguro text,
    fecha_cliente timestamptz NOT NULL,
    recibida_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    procesada_en timestamptz,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_OPERACION_SYNC_USUARIO" FOREIGN KEY (usuario_id)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "FK_OPERACION_SYNC_EVALUACION" FOREIGN KEY (evaluacion_id)
        REFERENCES "SIGERSA"."EVALUACION" (id) ON DELETE RESTRICT,
    CONSTRAINT "UQ_OPERACION_SYNC_IDEMPOTENCIA" UNIQUE (idempotency_key),
    CONSTRAINT "UQ_OPERACION_SYNC_SECUENCIA" UNIQUE (dispositivo_id, secuencia_cliente),
    CONSTRAINT "CK_OPERACION_SYNC_SECUENCIA" CHECK (secuencia_cliente >= 0),
    CONSTRAINT "CK_OPERACION_SYNC_VERSION_BASE" CHECK (version_base IS NULL OR version_base > 0),
    CONSTRAINT "CK_OPERACION_SYNC_PAYLOAD" CHECK (jsonb_typeof(payload) IN ('object', 'array')),
    CONSTRAINT "CK_OPERACION_SYNC_ESTADO" CHECK (estado IN ('RECIBIDA', 'PROCESANDO', 'APLICADA', 'DUPLICADA', 'CONFLICTO', 'RECHAZADA')),
    CONSTRAINT "CK_OPERACION_SYNC_PROCESADA" CHECK (procesada_en IS NULL OR procesada_en >= recibida_en),
    CONSTRAINT "CK_OPERACION_SYNC_VERSION" CHECK (version_fila > 0)
);

CREATE TABLE "SIGERSA"."AUDITORIA_EVENTO" (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    actor_id uuid,
    accion varchar(150) NOT NULL,
    recurso_tipo varchar(100) NOT NULL,
    recurso_id uuid,
    ocurrido_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    resultado varchar(30) NOT NULL,
    correlacion_id uuid,
    direccion_ip inet,
    agente_usuario text,
    motivo text,
    valores_anteriores jsonb,
    valores_nuevos jsonb,
    metadatos jsonb NOT NULL DEFAULT '{}'::jsonb,
    creado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    creado_por uuid,
    modificado_en timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    modificado_por uuid,
    version_fila bigint NOT NULL DEFAULT 1,
    CONSTRAINT "FK_AUDITORIA_ACTOR" FOREIGN KEY (actor_id)
        REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT,
    CONSTRAINT "CK_AUDITORIA_RESULTADO" CHECK (resultado IN ('EXITOSO', 'FALLIDO', 'DENEGADO')),
    CONSTRAINT "CK_AUDITORIA_ANTERIOR" CHECK (valores_anteriores IS NULL OR jsonb_typeof(valores_anteriores) = 'object'),
    CONSTRAINT "CK_AUDITORIA_NUEVO" CHECK (valores_nuevos IS NULL OR jsonb_typeof(valores_nuevos) = 'object'),
    CONSTRAINT "CK_AUDITORIA_METADATOS" CHECK (jsonb_typeof(metadatos) = 'object'),
    CONSTRAINT "CK_AUDITORIA_VERSION" CHECK (version_fila > 0)
);

-- =============================================================================
-- Integridad transversal, inmutabilidad y concurrencia
-- =============================================================================

-- Los actores de auditoría se agregan al final para resolver el ciclo de arranque
-- entre USUARIO y las tablas maestras. Se permite NULL para procesos de sistema.
DO $auditoria_fks$
DECLARE
    tabla text;
BEGIN
    FOREACH tabla IN ARRAY ARRAY[
        'USUARIO', 'ROL', 'PERMISO', 'USUARIO_ROL', 'ROL_PERMISO',
        'OTP_RECUPERACION', 'REFRESH_TOKEN', 'EMPRESA', 'ESTABLECIMIENTO',
        'CONTACTO', 'PROVINCIA', 'MUNICIPIO', 'DPS_DAS', 'COMERCIALIZACION',
        'MERCADO_OBJETIVO', 'ESTABLECIMIENTO_MERCADO', 'CATEGORIA_ALIMENTO',
        'SUBCATEGORIA_ALIMENTO', 'ESTABLECIMIENTO_PRODUCTO',
        'SUBCATEGORIA_RIESGO_VERSION', 'MOTIVO_INSPECCION', 'SOLICITUD',
        'ALERTA_LAPCH', 'DENUNCIA', 'CASO', 'CASO_TRANSICION', 'PROGRAMACION',
        'ASIGNACION', 'REGLA_CALIFICACION_VERSION', 'FICHA_INSPECCION',
        'ITEM_FICHA', 'OPCION_RESPUESTA', 'ITEM_OPCION', 'NIVEL_CRITICIDAD',
        'REGLA_RIESGO_VERSION', 'FACTOR_RIESGO', 'FACTOR_RIESGO_OPCION',
        'RANGO_RIESGO', 'EVALUACION_FACTOR_RIESGO', 'EVALUACION',
        'RESPUESTA_USUARIO', 'RESPUESTA_OPCION', 'NO_CONFORMIDAD',
        'MEDIDA_CORRECTIVA', 'CORRECCION', 'CORRECCION_CAMPO', 'EVIDENCIA',
        'EVIDENCIA_RESPUESTA', 'EVIDENCIA_NO_CONFORMIDAD',
        'EVIDENCIA_CORRECCION', 'INFORME', 'INFORME_VERSION',
        'PLANTILLA_CORREO', 'NOTIFICACION', 'OUTBOX', 'AUDITORIA_EVENTO',
        'OPERACION_SINCRONIZACION'
    ]
    LOOP
        EXECUTE format(
            'ALTER TABLE "SIGERSA".%I ADD CONSTRAINT %I FOREIGN KEY (creado_por) REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED',
            tabla,
            'FK_' || tabla || '_CREADO_POR'
        );
        EXECUTE format(
            'ALTER TABLE "SIGERSA".%I ADD CONSTRAINT %I FOREIGN KEY (modificado_por) REFERENCES "SIGERSA"."USUARIO" (id) ON DELETE RESTRICT DEFERRABLE INITIALLY DEFERRED',
            tabla,
            'FK_' || tabla || '_MODIFICADO_POR'
        );
    END LOOP;
END;
$auditoria_fks$;

CREATE FUNCTION "SIGERSA"."FN_INCREMENTAR_VERSION_FILA"()
RETURNS trigger
LANGUAGE plpgsql
AS $funcion$
BEGIN
    NEW.modificado_en := CURRENT_TIMESTAMP;
    NEW.version_fila := OLD.version_fila + 1;
    RETURN NEW;
END;
$funcion$;

DO $triggers_version$
DECLARE
    tabla text;
BEGIN
    FOREACH tabla IN ARRAY ARRAY[
        'USUARIO', 'ROL', 'PERMISO', 'USUARIO_ROL', 'ROL_PERMISO',
        'OTP_RECUPERACION', 'REFRESH_TOKEN', 'EMPRESA', 'ESTABLECIMIENTO',
        'CONTACTO', 'PROVINCIA', 'MUNICIPIO', 'DPS_DAS', 'COMERCIALIZACION',
        'MERCADO_OBJETIVO', 'ESTABLECIMIENTO_MERCADO', 'CATEGORIA_ALIMENTO',
        'SUBCATEGORIA_ALIMENTO', 'ESTABLECIMIENTO_PRODUCTO',
        'SUBCATEGORIA_RIESGO_VERSION', 'MOTIVO_INSPECCION', 'SOLICITUD',
        'ALERTA_LAPCH', 'DENUNCIA', 'CASO', 'CASO_TRANSICION', 'PROGRAMACION',
        'ASIGNACION', 'REGLA_CALIFICACION_VERSION', 'FICHA_INSPECCION',
        'ITEM_FICHA', 'OPCION_RESPUESTA', 'ITEM_OPCION', 'NIVEL_CRITICIDAD',
        'REGLA_RIESGO_VERSION', 'FACTOR_RIESGO', 'FACTOR_RIESGO_OPCION',
        'RANGO_RIESGO', 'EVALUACION_FACTOR_RIESGO', 'EVALUACION',
        'RESPUESTA_USUARIO', 'RESPUESTA_OPCION', 'NO_CONFORMIDAD',
        'MEDIDA_CORRECTIVA', 'CORRECCION', 'CORRECCION_CAMPO', 'EVIDENCIA',
        'EVIDENCIA_RESPUESTA', 'EVIDENCIA_NO_CONFORMIDAD',
        'EVIDENCIA_CORRECCION', 'INFORME', 'INFORME_VERSION',
        'PLANTILLA_CORREO', 'NOTIFICACION', 'OUTBOX',
        'OPERACION_SINCRONIZACION'
    ]
    LOOP
        EXECUTE format(
            'CREATE TRIGGER %I BEFORE UPDATE ON "SIGERSA".%I FOR EACH ROW EXECUTE FUNCTION "SIGERSA"."FN_INCREMENTAR_VERSION_FILA"()',
            'TRG_' || tabla || '_VERSION_FILA',
            tabla
        );
    END LOOP;
END;
$triggers_version$;

CREATE FUNCTION "SIGERSA"."FN_PROTEGER_REGLA_PUBLICADA"()
RETURNS trigger
LANGUAGE plpgsql
AS $funcion$
BEGIN
    IF TG_OP = 'DELETE' AND OLD.estado IN ('PUBLICADA', 'RETIRADA') THEN
        RAISE EXCEPTION 'Una regla publicada o histórica no se puede eliminar';
    END IF;

    IF TG_OP = 'UPDATE' AND OLD.estado IN ('PUBLICADA', 'RETIRADA') THEN
        IF (to_jsonb(NEW) - ARRAY['estado', 'vigente_hasta', 'modificado_en', 'modificado_por', 'version_fila'])
            IS DISTINCT FROM
           (to_jsonb(OLD) - ARRAY['estado', 'vigente_hasta', 'modificado_en', 'modificado_por', 'version_fila'])
        THEN
            RAISE EXCEPTION 'Una regla publicada es inmutable; cree una nueva versión';
        END IF;
    END IF;

    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END;
$funcion$;

CREATE TRIGGER "TRG_REGLA_CALIFICACION_PROTEGER_PUBLICADA"
BEFORE UPDATE OR DELETE ON "SIGERSA"."REGLA_CALIFICACION_VERSION"
FOR EACH ROW
EXECUTE FUNCTION "SIGERSA"."FN_PROTEGER_REGLA_PUBLICADA"();

CREATE TRIGGER "TRG_REGLA_RIESGO_PROTEGER_PUBLICADA"
BEFORE UPDATE OR DELETE ON "SIGERSA"."REGLA_RIESGO_VERSION"
FOR EACH ROW
EXECUTE FUNCTION "SIGERSA"."FN_PROTEGER_REGLA_PUBLICADA"();

CREATE FUNCTION "SIGERSA"."FN_VALIDAR_PUBLICACION_REGLA_RIESGO"()
RETURNS trigger
LANGUAGE plpgsql
AS $funcion$
DECLARE
    suma_pesos numeric;
    cantidad_rangos integer;
    primer_limite numeric;
    primer_incluye boolean;
    ultimo_sin_limite boolean;
    hay_solapamiento boolean;
    hay_hueco boolean;
BEGIN
    IF NEW.estado = 'PUBLICADA' AND OLD.estado <> 'PUBLICADA' THEN
        SELECT COALESCE(sum(factor.peso), 0)
          INTO suma_pesos
          FROM "SIGERSA"."FACTOR_RIESGO" AS factor
         WHERE factor.regla_riesgo_version_id = NEW.id
           AND factor.activo = true;

        IF suma_pesos <> 1 THEN
            RAISE EXCEPTION 'Los pesos activos de la regla de riesgo deben sumar exactamente 1.00';
        END IF;

        SELECT count(*), min(rango.limite_inferior)
          INTO cantidad_rangos, primer_limite
          FROM "SIGERSA"."RANGO_RIESGO" AS rango
         WHERE rango.regla_riesgo_version_id = NEW.id;

        SELECT rango.incluye_inferior
          INTO primer_incluye
          FROM "SIGERSA"."RANGO_RIESGO" AS rango
         WHERE rango.regla_riesgo_version_id = NEW.id
         ORDER BY rango.limite_inferior, rango.orden
         LIMIT 1;

        SELECT rango.limite_superior IS NULL
          INTO ultimo_sin_limite
          FROM "SIGERSA"."RANGO_RIESGO" AS rango
         WHERE rango.regla_riesgo_version_id = NEW.id
         ORDER BY rango.limite_inferior DESC, rango.orden DESC
         LIMIT 1;

        IF cantidad_rangos = 0 OR primer_limite <> 1 OR primer_incluye IS DISTINCT FROM true OR ultimo_sin_limite IS DISTINCT FROM true THEN
            RAISE EXCEPTION 'Los rangos de riesgo deben cubrir el dominio desde 1.00 y dejar abierto el límite superior final';
        END IF;

        SELECT EXISTS (
            SELECT 1
              FROM "SIGERSA"."RANGO_RIESGO" AS izquierdo
              JOIN "SIGERSA"."RANGO_RIESGO" AS derecho
                ON izquierdo.regla_riesgo_version_id = derecho.regla_riesgo_version_id
               AND izquierdo.id < derecho.id
             WHERE izquierdo.regla_riesgo_version_id = NEW.id
               AND numrange(
                    izquierdo.limite_inferior,
                    izquierdo.limite_superior,
                    (CASE WHEN izquierdo.incluye_inferior THEN '[' ELSE '(' END)
                    || (CASE WHEN izquierdo.limite_superior IS NOT NULL AND izquierdo.incluye_superior THEN ']' ELSE ')' END)
               ) && numrange(
                    derecho.limite_inferior,
                    derecho.limite_superior,
                    (CASE WHEN derecho.incluye_inferior THEN '[' ELSE '(' END)
                    || (CASE WHEN derecho.limite_superior IS NOT NULL AND derecho.incluye_superior THEN ']' ELSE ')' END)
               )
        ) INTO hay_solapamiento;

        IF hay_solapamiento THEN
            RAISE EXCEPTION 'Los rangos de riesgo no pueden solaparse';
        END IF;

        SELECT EXISTS (
            SELECT 1
              FROM (
                    SELECT rango.limite_inferior,
                           rango.incluye_inferior,
                           lag(rango.limite_superior) OVER (ORDER BY rango.limite_inferior, rango.orden) AS limite_anterior,
                           lag(rango.incluye_superior) OVER (ORDER BY rango.limite_inferior, rango.orden) AS incluye_anterior
                      FROM "SIGERSA"."RANGO_RIESGO" AS rango
                     WHERE rango.regla_riesgo_version_id = NEW.id
              ) AS ordenados
             WHERE ordenados.limite_anterior IS NOT NULL
               AND (
                    ordenados.limite_anterior <> ordenados.limite_inferior
                    OR (ordenados.incluye_anterior::integer + ordenados.incluye_inferior::integer) <> 1
               )
        ) INTO hay_hueco;

        IF hay_hueco THEN
            RAISE EXCEPTION 'Los rangos de riesgo deben ser continuos y sin huecos';
        END IF;
    END IF;

    RETURN NEW;
END;
$funcion$;

CREATE TRIGGER "TRG_REGLA_RIESGO_VALIDAR_PUBLICACION"
BEFORE UPDATE OF estado ON "SIGERSA"."REGLA_RIESGO_VERSION"
FOR EACH ROW
EXECUTE FUNCTION "SIGERSA"."FN_VALIDAR_PUBLICACION_REGLA_RIESGO"();

CREATE FUNCTION "SIGERSA"."FN_PROTEGER_DETALLE_REGLA_RIESGO"()
RETURNS trigger
LANGUAGE plpgsql
AS $funcion$
DECLARE
    regla_id uuid;
    estado_regla varchar(20);
BEGIN
    IF TG_TABLE_NAME IN ('FACTOR_RIESGO', 'RANGO_RIESGO', 'SUBCATEGORIA_RIESGO_VERSION') THEN
        regla_id := CASE WHEN TG_OP = 'DELETE' THEN OLD.regla_riesgo_version_id ELSE NEW.regla_riesgo_version_id END;
    ELSE
        SELECT factor.regla_riesgo_version_id
          INTO regla_id
          FROM "SIGERSA"."FACTOR_RIESGO" AS factor
         WHERE factor.id = CASE WHEN TG_OP = 'DELETE' THEN OLD.factor_riesgo_id ELSE NEW.factor_riesgo_id END;
    END IF;

    SELECT regla.estado
      INTO estado_regla
      FROM "SIGERSA"."REGLA_RIESGO_VERSION" AS regla
     WHERE regla.id = regla_id;

    IF estado_regla IN ('PUBLICADA', 'RETIRADA') THEN
        RAISE EXCEPTION 'El detalle de una regla de riesgo publicada es inmutable';
    END IF;

    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END;
$funcion$;

CREATE TRIGGER "TRG_FACTOR_RIESGO_PROTEGER_REGLA"
BEFORE INSERT OR UPDATE OR DELETE ON "SIGERSA"."FACTOR_RIESGO"
FOR EACH ROW EXECUTE FUNCTION "SIGERSA"."FN_PROTEGER_DETALLE_REGLA_RIESGO"();

CREATE TRIGGER "TRG_FACTOR_OPCION_PROTEGER_REGLA"
BEFORE INSERT OR UPDATE OR DELETE ON "SIGERSA"."FACTOR_RIESGO_OPCION"
FOR EACH ROW EXECUTE FUNCTION "SIGERSA"."FN_PROTEGER_DETALLE_REGLA_RIESGO"();

CREATE TRIGGER "TRG_RANGO_RIESGO_PROTEGER_REGLA"
BEFORE INSERT OR UPDATE OR DELETE ON "SIGERSA"."RANGO_RIESGO"
FOR EACH ROW EXECUTE FUNCTION "SIGERSA"."FN_PROTEGER_DETALLE_REGLA_RIESGO"();

CREATE TRIGGER "TRG_SUBCAT_RIESGO_PROTEGER_REGLA"
BEFORE INSERT OR UPDATE OR DELETE ON "SIGERSA"."SUBCATEGORIA_RIESGO_VERSION"
FOR EACH ROW EXECUTE FUNCTION "SIGERSA"."FN_PROTEGER_DETALLE_REGLA_RIESGO"();

CREATE FUNCTION "SIGERSA"."FN_VALIDAR_ASIGNACION"()
RETURNS trigger
LANGUAGE plpgsql
AS $funcion$
DECLARE
    estado_programacion varchar(30);
    estado_evaluador varchar(30);
BEGIN
    SELECT programacion.estado
      INTO estado_programacion
      FROM "SIGERSA"."PROGRAMACION" AS programacion
     WHERE programacion.id = NEW.programacion_id;

    SELECT usuario.estado
      INTO estado_evaluador
      FROM "SIGERSA"."USUARIO" AS usuario
     WHERE usuario.id = NEW.evaluador_id;

    IF NEW.estado = 'ACTIVA' AND estado_programacion = 'CANCELADA' THEN
        RAISE EXCEPTION 'Una programación cancelada no admite asignaciones activas';
    END IF;

    IF NEW.estado = 'ACTIVA' AND estado_evaluador <> 'ACTIVO' THEN
        RAISE EXCEPTION 'Solo un usuario activo puede recibir una asignación';
    END IF;

    RETURN NEW;
END;
$funcion$;

CREATE TRIGGER "TRG_ASIGNACION_VALIDAR"
BEFORE INSERT OR UPDATE OF programacion_id, evaluador_id, estado
ON "SIGERSA"."ASIGNACION"
FOR EACH ROW EXECUTE FUNCTION "SIGERSA"."FN_VALIDAR_ASIGNACION"();

CREATE FUNCTION "SIGERSA"."FN_VALIDAR_EVALUACION"()
RETURNS trigger
LANGUAGE plpgsql
AS $funcion$
DECLARE
    estado_ficha "SIGERSA"."ESTADO_FICHA";
    estado_regla varchar(20);
    estado_programacion varchar(30);
BEGIN
    SELECT ficha.estado
      INTO estado_ficha
      FROM "SIGERSA"."FICHA_INSPECCION" AS ficha
     WHERE ficha.id = NEW.ficha_inspeccion_id;

    SELECT regla.estado
      INTO estado_regla
      FROM "SIGERSA"."REGLA_RIESGO_VERSION" AS regla
     WHERE regla.id = NEW.version_regla_riesgo_id;

    IF estado_ficha <> 'PUBLICADA' THEN
        RAISE EXCEPTION 'Una evaluación debe utilizar una ficha PUBLICADA';
    END IF;

    IF estado_regla <> 'PUBLICADA' THEN
        RAISE EXCEPTION 'Una evaluación debe utilizar una regla de riesgo PUBLICADA';
    END IF;

    IF NEW.programacion_id IS NOT NULL THEN
        SELECT programacion.estado
          INTO estado_programacion
          FROM "SIGERSA"."PROGRAMACION" AS programacion
         WHERE programacion.id = NEW.programacion_id;

        IF estado_programacion = 'CANCELADA' THEN
            RAISE EXCEPTION 'Una programación cancelada no puede originar una evaluación';
        END IF;
    END IF;

    IF TG_OP = 'UPDATE' AND (
        NEW.ficha_inspeccion_id IS DISTINCT FROM OLD.ficha_inspeccion_id
        OR NEW.version_regla_riesgo_id IS DISTINCT FROM OLD.version_regla_riesgo_id
    ) THEN
        RAISE EXCEPTION 'La ficha y la regla de riesgo de una evaluación son inmutables';
    END IF;

    RETURN NEW;
END;
$funcion$;

CREATE TRIGGER "TRG_EVALUACION_VALIDAR_REFERENCIAS"
BEFORE INSERT OR UPDATE OF ficha_inspeccion_id, version_regla_riesgo_id, programacion_id
ON "SIGERSA"."EVALUACION"
FOR EACH ROW EXECUTE FUNCTION "SIGERSA"."FN_VALIDAR_EVALUACION"();

CREATE FUNCTION "SIGERSA"."FN_VALIDAR_EVALUACION_FACTOR"()
RETURNS trigger
LANGUAGE plpgsql
AS $funcion$
DECLARE
    regla_evaluacion uuid;
    regla_factor uuid;
BEGIN
    SELECT evaluacion.version_regla_riesgo_id
      INTO regla_evaluacion
      FROM "SIGERSA"."EVALUACION" AS evaluacion
     WHERE evaluacion.id = NEW.evaluacion_id;

    SELECT factor.regla_riesgo_version_id
      INTO regla_factor
      FROM "SIGERSA"."FACTOR_RIESGO" AS factor
     WHERE factor.id = NEW.factor_riesgo_id;

    IF regla_evaluacion IS DISTINCT FROM regla_factor THEN
        RAISE EXCEPTION 'El factor no pertenece a la regla de riesgo de la evaluación';
    END IF;

    RETURN NEW;
END;
$funcion$;

CREATE TRIGGER "TRG_EVALUACION_FACTOR_VALIDAR_REGLA"
BEFORE INSERT OR UPDATE OF evaluacion_id, factor_riesgo_id
ON "SIGERSA"."EVALUACION_FACTOR_RIESGO"
FOR EACH ROW EXECUTE FUNCTION "SIGERSA"."FN_VALIDAR_EVALUACION_FACTOR"();

CREATE FUNCTION "SIGERSA"."FN_VALIDAR_JERARQUIA_ITEM_FICHA"()
RETURNS trigger
LANGUAGE plpgsql
AS $funcion$
DECLARE
    nivel_padre smallint;
    genera_ciclo boolean;
BEGIN
    IF NEW.parent_item_ficha_id IS NULL THEN
        IF NEW.nivel <> 0 THEN
            RAISE EXCEPTION 'Un ítem raíz debe tener nivel 0';
        END IF;
        RETURN NEW;
    END IF;

    IF NEW.parent_item_ficha_id = NEW.id THEN
        RAISE EXCEPTION 'Un ítem no puede ser su propio padre';
    END IF;

    SELECT item.nivel
      INTO nivel_padre
      FROM "SIGERSA"."ITEM_FICHA" AS item
     WHERE item.id = NEW.parent_item_ficha_id
       AND item.ficha_inspeccion_id = NEW.ficha_inspeccion_id;

    IF nivel_padre IS NULL THEN
        RAISE EXCEPTION 'El padre debe existir y pertenecer a la misma ficha';
    END IF;

    IF NEW.nivel <> nivel_padre + 1 THEN
        RAISE EXCEPTION 'El nivel del ítem debe ser exactamente el nivel del padre más uno';
    END IF;

    WITH RECURSIVE ancestros AS (
        SELECT item.id, item.parent_item_ficha_id
          FROM "SIGERSA"."ITEM_FICHA" AS item
         WHERE item.id = NEW.parent_item_ficha_id
           AND item.ficha_inspeccion_id = NEW.ficha_inspeccion_id
        UNION ALL
        SELECT padre.id, padre.parent_item_ficha_id
          FROM "SIGERSA"."ITEM_FICHA" AS padre
          JOIN ancestros ON padre.id = ancestros.parent_item_ficha_id
         WHERE padre.ficha_inspeccion_id = NEW.ficha_inspeccion_id
    )
    SELECT EXISTS (SELECT 1 FROM ancestros WHERE id = NEW.id)
      INTO genera_ciclo;

    IF genera_ciclo THEN
        RAISE EXCEPTION 'La relación padre-hijo produciría un ciclo en ITEM_FICHA';
    END IF;

    RETURN NEW;
END;
$funcion$;

CREATE TRIGGER "TRG_ITEM_FICHA_VALIDAR_JERARQUIA"
BEFORE INSERT OR UPDATE OF parent_item_ficha_id, ficha_inspeccion_id, nivel
ON "SIGERSA"."ITEM_FICHA"
FOR EACH ROW
EXECUTE FUNCTION "SIGERSA"."FN_VALIDAR_JERARQUIA_ITEM_FICHA"();

CREATE FUNCTION "SIGERSA"."FN_VALIDAR_ITEM_OPCION"()
RETURNS trigger
LANGUAGE plpgsql
AS $funcion$
DECLARE
    item_evaluable boolean;
BEGIN
    SELECT item.es_evaluable
      INTO item_evaluable
      FROM "SIGERSA"."ITEM_FICHA" AS item
     WHERE item.id = NEW.item_ficha_id;

    IF item_evaluable IS DISTINCT FROM true THEN
        RAISE EXCEPTION 'Solo un ITEM_FICHA evaluable puede tener opciones de respuesta';
    END IF;

    RETURN NEW;
END;
$funcion$;

CREATE TRIGGER "TRG_ITEM_OPCION_VALIDAR_ITEM"
BEFORE INSERT OR UPDATE OF item_ficha_id
ON "SIGERSA"."ITEM_OPCION"
FOR EACH ROW
EXECUTE FUNCTION "SIGERSA"."FN_VALIDAR_ITEM_OPCION"();

CREATE FUNCTION "SIGERSA"."FN_VALIDAR_RESPUESTA_USUARIO"()
RETURNS trigger
LANGUAGE plpgsql
AS $funcion$
DECLARE
    ficha_evaluacion uuid;
    ficha_item uuid;
    item_evaluable boolean;
    estado_actual "SIGERSA"."ESTADO_EVALUACION";
BEGIN
    SELECT evaluacion.ficha_inspeccion_id, evaluacion.estado
      INTO ficha_evaluacion, estado_actual
      FROM "SIGERSA"."EVALUACION" AS evaluacion
     WHERE evaluacion.id = NEW.evaluacion_id;

    SELECT item.ficha_inspeccion_id, item.es_evaluable
      INTO ficha_item, item_evaluable
      FROM "SIGERSA"."ITEM_FICHA" AS item
     WHERE item.id = NEW.item_ficha_id;

    IF ficha_evaluacion IS DISTINCT FROM ficha_item THEN
        RAISE EXCEPTION 'El ítem respondido no pertenece a la ficha de la evaluación';
    END IF;

    IF item_evaluable IS DISTINCT FROM true THEN
        RAISE EXCEPTION 'El ítem seleccionado no admite respuestas';
    END IF;

    IF estado_actual NOT IN ('EN_EJECUCION', 'EN_CORRECCION') THEN
        RAISE EXCEPTION 'La evaluación no está habilitada para registrar respuestas';
    END IF;

    RETURN NEW;
END;
$funcion$;

CREATE TRIGGER "TRG_RESPUESTA_VALIDAR_CONTEXTO"
BEFORE INSERT OR UPDATE
ON "SIGERSA"."RESPUESTA_USUARIO"
FOR EACH ROW
EXECUTE FUNCTION "SIGERSA"."FN_VALIDAR_RESPUESTA_USUARIO"();

CREATE FUNCTION "SIGERSA"."FN_VALIDAR_RESPUESTA_OPCION"()
RETURNS trigger
LANGUAGE plpgsql
AS $funcion$
DECLARE
    item_respuesta uuid;
    item_opcion uuid;
    tipo_respuesta "SIGERSA"."TIPO_RESPUESTA";
BEGIN
    SELECT respuesta.item_ficha_id, item.tipo_respuesta
      INTO item_respuesta, tipo_respuesta
      FROM "SIGERSA"."RESPUESTA_USUARIO" AS respuesta
      JOIN "SIGERSA"."ITEM_FICHA" AS item ON item.id = respuesta.item_ficha_id
     WHERE respuesta.id = NEW.respuesta_usuario_id;

    SELECT opcion.item_ficha_id
      INTO item_opcion
      FROM "SIGERSA"."ITEM_OPCION" AS opcion
     WHERE opcion.id = NEW.item_opcion_id;

    IF item_respuesta IS DISTINCT FROM item_opcion THEN
        RAISE EXCEPTION 'La opción múltiple no pertenece al ítem respondido';
    END IF;

    IF tipo_respuesta <> 'OPCION_MULTIPLE' THEN
        RAISE EXCEPTION 'RESPUESTA_OPCION solo se utiliza para ítems de opción múltiple';
    END IF;

    RETURN NEW;
END;
$funcion$;

CREATE TRIGGER "TRG_RESPUESTA_OPCION_VALIDAR_ITEM"
BEFORE INSERT OR UPDATE OF respuesta_usuario_id, item_opcion_id
ON "SIGERSA"."RESPUESTA_OPCION"
FOR EACH ROW EXECUTE FUNCTION "SIGERSA"."FN_VALIDAR_RESPUESTA_OPCION"();

CREATE FUNCTION "SIGERSA"."FN_PROTEGER_FICHA_PUBLICADA"()
RETURNS trigger
LANGUAGE plpgsql
AS $funcion$
BEGIN
    IF TG_OP = 'DELETE' AND OLD.estado IN ('PUBLICADA', 'RETIRADA', 'ARCHIVADA') THEN
        RAISE EXCEPTION 'Una ficha publicada o histórica no se puede eliminar';
    END IF;

    IF TG_OP = 'UPDATE' AND OLD.estado IN ('PUBLICADA', 'RETIRADA', 'ARCHIVADA') THEN
        IF NEW.ficha_raiz_id IS DISTINCT FROM OLD.ficha_raiz_id
            OR NEW.version_anterior_id IS DISTINCT FROM OLD.version_anterior_id
            OR NEW.codigo IS DISTINCT FROM OLD.codigo
            OR NEW.nombre IS DISTINCT FROM OLD.nombre
            OR NEW.descripcion IS DISTINCT FROM OLD.descripcion
            OR NEW.version IS DISTINCT FROM OLD.version
            OR NEW.vigente_desde IS DISTINCT FROM OLD.vigente_desde
            OR NEW.puntaje_maximo_referencia IS DISTINCT FROM OLD.puntaje_maximo_referencia
            OR NEW.regla_calificacion_id IS DISTINCT FROM OLD.regla_calificacion_id
            OR NEW.hash_definicion IS DISTINCT FROM OLD.hash_definicion
            OR NEW.definicion_snapshot IS DISTINCT FROM OLD.definicion_snapshot
            OR NEW.publicado_en IS DISTINCT FROM OLD.publicado_en
            OR NEW.publicado_por IS DISTINCT FROM OLD.publicado_por
        THEN
            RAISE EXCEPTION 'La definición de una ficha publicada es inmutable; cree una nueva versión';
        END IF;
    END IF;

    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END;
$funcion$;

CREATE TRIGGER "TRG_FICHA_PROTEGER_PUBLICADA"
BEFORE UPDATE OR DELETE ON "SIGERSA"."FICHA_INSPECCION"
FOR EACH ROW
EXECUTE FUNCTION "SIGERSA"."FN_PROTEGER_FICHA_PUBLICADA"();

CREATE FUNCTION "SIGERSA"."FN_PROTEGER_DETALLE_FICHA"()
RETURNS trigger
LANGUAGE plpgsql
AS $funcion$
DECLARE
    ficha_id uuid;
    estado_ficha "SIGERSA"."ESTADO_FICHA";
BEGIN
    IF TG_TABLE_NAME = 'ITEM_FICHA' THEN
        ficha_id := CASE WHEN TG_OP = 'DELETE' THEN OLD.ficha_inspeccion_id ELSE NEW.ficha_inspeccion_id END;
    ELSE
        SELECT item.ficha_inspeccion_id
          INTO ficha_id
          FROM "SIGERSA"."ITEM_FICHA" AS item
         WHERE item.id = CASE WHEN TG_OP = 'DELETE' THEN OLD.item_ficha_id ELSE NEW.item_ficha_id END;
    END IF;

    SELECT ficha.estado
      INTO estado_ficha
      FROM "SIGERSA"."FICHA_INSPECCION" AS ficha
     WHERE ficha.id = ficha_id;

    IF estado_ficha IN ('PUBLICADA', 'RETIRADA', 'ARCHIVADA') THEN
        RAISE EXCEPTION 'Los ítems y opciones de una ficha publicada son inmutables';
    END IF;

    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;
    RETURN NEW;
END;
$funcion$;

CREATE TRIGGER "TRG_ITEM_FICHA_PROTEGER_PUBLICADA"
BEFORE INSERT OR UPDATE OR DELETE ON "SIGERSA"."ITEM_FICHA"
FOR EACH ROW
EXECUTE FUNCTION "SIGERSA"."FN_PROTEGER_DETALLE_FICHA"();

CREATE TRIGGER "TRG_ITEM_OPCION_PROTEGER_PUBLICADA"
BEFORE INSERT OR UPDATE OR DELETE ON "SIGERSA"."ITEM_OPCION"
FOR EACH ROW
EXECUTE FUNCTION "SIGERSA"."FN_PROTEGER_DETALLE_FICHA"();

CREATE FUNCTION "SIGERSA"."FN_BLOQUEAR_CAMBIOS_AUDITORIA"()
RETURNS trigger
LANGUAGE plpgsql
AS $funcion$
BEGIN
    RAISE EXCEPTION 'AUDITORIA_EVENTO es inmutable';
END;
$funcion$;

CREATE TRIGGER "TRG_AUDITORIA_EVENTO_INMUTABLE"
BEFORE UPDATE OR DELETE ON "SIGERSA"."AUDITORIA_EVENTO"
FOR EACH ROW
EXECUTE FUNCTION "SIGERSA"."FN_BLOQUEAR_CAMBIOS_AUDITORIA"();

-- =============================================================================
-- Índices estratégicos
-- Los índices pertenecen al esquema SIGERSA porque se crean sobre sus tablas.
-- =============================================================================

CREATE INDEX "IX_EMPRESA_RAZON_SOCIAL"
    ON "SIGERSA"."EMPRESA" (razon_social);

CREATE INDEX "IX_ESTABLECIMIENTO_EMPRESA_NOMBRE"
    ON "SIGERSA"."ESTABLECIMIENTO" (empresa_id, nombre);

CREATE INDEX "IX_SUBCATEGORIA_RIESGO_ACTIVA"
    ON "SIGERSA"."SUBCATEGORIA_ALIMENTO" (nivel_riesgo)
    WHERE activo = true;

CREATE INDEX "IX_USUARIO_EMPRESA_ESTADO"
    ON "SIGERSA"."USUARIO" (empresa_id, estado);

CREATE INDEX "IX_OTP_USUARIO_ESTADO_EXPIRA"
    ON "SIGERSA"."OTP_RECUPERACION" (usuario_id, estado, expira_en);

CREATE INDEX "IX_REFRESH_TOKEN_USUARIO_EXPIRA"
    ON "SIGERSA"."REFRESH_TOKEN" (usuario_id, expira_en)
    WHERE revocado_en IS NULL;

CREATE INDEX "IX_USUARIO_ROL_USUARIO_ACTIVO"
    ON "SIGERSA"."USUARIO_ROL" (usuario_id, vigente_hasta)
    WHERE activo = true;

CREATE INDEX "IX_MUNICIPIO_PROVINCIA_NOMBRE"
    ON "SIGERSA"."MUNICIPIO" (provincia_id, nombre);

CREATE INDEX "IX_CONTACTO_EMPRESA_ESTABLECIMIENTO"
    ON "SIGERSA"."CONTACTO" (empresa_id, establecimiento_id)
    WHERE activo = true;

CREATE INDEX "IX_ESTABLECIMIENTO_PRODUCTO_SUBCATEGORIA"
    ON "SIGERSA"."ESTABLECIMIENTO_PRODUCTO" (subcategoria_alimento_id, establecimiento_id)
    WHERE activo = true;

CREATE INDEX "IX_SUBCAT_RIESGO_REGLA_NIVEL"
    ON "SIGERSA"."SUBCATEGORIA_RIESGO_VERSION" (regla_riesgo_version_id, nivel_riesgo);

CREATE INDEX "IX_ITEM_FICHA_ARBOL"
    ON "SIGERSA"."ITEM_FICHA" (ficha_inspeccion_id, parent_item_ficha_id, nivel, orden);

CREATE INDEX "IX_SOLICITUD_ESTABLECIMIENTO_ESTADO_FECHA"
    ON "SIGERSA"."SOLICITUD" (establecimiento_id, estado, creado_en DESC);

CREATE INDEX "IX_ALERTA_LAPCH_ESTABLECIMIENTO_FECHA"
    ON "SIGERSA"."ALERTA_LAPCH" (establecimiento_id, fecha_alerta DESC);

CREATE INDEX "IX_DENUNCIA_ESTABLECIMIENTO_FECHA"
    ON "SIGERSA"."DENUNCIA" (establecimiento_id, fecha_recepcion DESC);

CREATE INDEX "IX_CASO_ESTABLECIMIENTO_ESTADO_FECHA"
    ON "SIGERSA"."CASO" (establecimiento_id, estado, creado_en DESC);

CREATE INDEX "IX_CASO_RESPONSABLE_ESTADO"
    ON "SIGERSA"."CASO" (responsable_actual_id, estado)
    WHERE responsable_actual_id IS NOT NULL;

CREATE INDEX "IX_CASO_TRANSICION_CASO_FECHA"
    ON "SIGERSA"."CASO_TRANSICION" (caso_id, ejecutado_en DESC);

CREATE INDEX "IX_PROGRAMACION_CASO_FECHA"
    ON "SIGERSA"."PROGRAMACION" (caso_id, inicio_programado);

CREATE INDEX "IX_ASIGNACION_EVALUADOR_ESTADO"
    ON "SIGERSA"."ASIGNACION" (evaluador_id, estado, asignado_en DESC);

CREATE INDEX "IX_FACTOR_RIESGO_REGLA_ACTIVO"
    ON "SIGERSA"."FACTOR_RIESGO" (regla_riesgo_version_id, orden)
    WHERE activo = true;

CREATE INDEX "IX_RANGO_RIESGO_REGLA_LIMITES"
    ON "SIGERSA"."RANGO_RIESGO" (regla_riesgo_version_id, limite_inferior, limite_superior);

CREATE INDEX "IX_EVALUACION_ESTABLECIMIENTO_FECHA"
    ON "SIGERSA"."EVALUACION" (establecimiento_id, iniciada_en DESC);

CREATE INDEX "IX_EVALUACION_EVALUADOR_ESTADO_FECHA"
    ON "SIGERSA"."EVALUACION" (evaluador_principal_id, estado, programada_inicio_en);

CREATE INDEX "IX_EVALUACION_RIESGO"
    ON "SIGERSA"."EVALUACION" (nivel_riesgo, riesgo_total DESC)
    WHERE nivel_riesgo IS NOT NULL;

CREATE INDEX "IX_EVALUACION_FACTOR_EVALUACION"
    ON "SIGERSA"."EVALUACION_FACTOR_RIESGO" (evaluacion_id, calculado_en);

CREATE INDEX "IX_RESPUESTA_ITEM"
    ON "SIGERSA"."RESPUESTA_USUARIO" (item_ficha_id);

CREATE INDEX "IX_NC_EVALUACION_ESTADO"
    ON "SIGERSA"."NO_CONFORMIDAD" (evaluacion_id, estado);

CREATE INDEX "IX_CORRECCION_EVALUACION_ESTADO"
    ON "SIGERSA"."CORRECCION" (evaluacion_id, estado, fecha_limite);

CREATE INDEX "IX_EVIDENCIA_EVALUACION_SINCRONIZACION"
    ON "SIGERSA"."EVIDENCIA" (evaluacion_id, estado_sincronizacion)
    WHERE eliminada = false;

CREATE INDEX "IX_EVIDENCIA_RESPUESTA_RESPUESTA"
    ON "SIGERSA"."EVIDENCIA_RESPUESTA" (respuesta_usuario_id);

CREATE INDEX "IX_EVIDENCIA_NC_NO_CONFORMIDAD"
    ON "SIGERSA"."EVIDENCIA_NO_CONFORMIDAD" (no_conformidad_id);

CREATE INDEX "IX_EVIDENCIA_CORRECCION_CORRECCION"
    ON "SIGERSA"."EVIDENCIA_CORRECCION" (correccion_id);

CREATE INDEX "IX_MEDIDA_CORRECTIVA_ESTADO_FECHA"
    ON "SIGERSA"."MEDIDA_CORRECTIVA" (estado, fecha_compromiso);

CREATE INDEX "IX_INFORME_EVALUACION_ESTADO"
    ON "SIGERSA"."INFORME" (evaluacion_id, estado);

CREATE INDEX "IX_NOTIFICACION_USUARIO_ESTADO_FECHA"
    ON "SIGERSA"."NOTIFICACION" (usuario_id, estado, creado_en DESC);

CREATE INDEX "IX_OPERACION_SYNC_USUARIO_FECHA"
    ON "SIGERSA"."OPERACION_SINCRONIZACION" (usuario_id, recibida_en DESC);

CREATE INDEX "IX_OPERACION_SYNC_EVALUACION_ESTADO"
    ON "SIGERSA"."OPERACION_SINCRONIZACION" (evaluacion_id, estado)
    WHERE evaluacion_id IS NOT NULL;

CREATE INDEX "IX_OUTBOX_PENDIENTE"
    ON "SIGERSA"."OUTBOX" (disponible_en, intentos)
    WHERE estado IN ('PENDIENTE', 'FALLIDO');

CREATE INDEX "IX_AUDITORIA_RECURSO_FECHA"
    ON "SIGERSA"."AUDITORIA_EVENTO" (recurso_tipo, recurso_id, ocurrido_en DESC);

CREATE INDEX "IX_AUDITORIA_ACTOR_FECHA"
    ON "SIGERSA"."AUDITORIA_EVENTO" (actor_id, ocurrido_en DESC)
    WHERE actor_id IS NOT NULL;

COMMIT;
