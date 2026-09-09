# Especificación de persistencia y esquema de datos — Fase 1

## 1. Línea base

PostgreSQL 15 o superior es la fuente transaccional de verdad. Todos los objetos de negocio se crean en el esquema citado `"SIGERSA"`; `public` solo puede contener extensiones del motor. Las consultas Dapper califican expresamente el esquema y la conexión fija `search_path` a `"SIGERSA"` como defensa adicional.

Las migraciones se encuentran en `src/backend/Database/Migrations` y deben aplicarse por nombre ascendente:

1. `001_initial_schema_sigersa.sql`: esquema inicial, parámetros, ficha base y modelo transaccional.
2. `002_repair_allitems_hierarchy.sql`: reparación repetible de referencias jerárquicas heredadas.
3. `003_link_versioned_items_to_allitems.sql`: trazabilidad entre la instantánea publicada y su fila fuente.
4. `004_expand_system_roles.sql`: cinco roles canónicos, permisos y validación de ámbito empresarial.
5. `005_harden_authentication_flows.sql`: comprobantes de recuperación de contraseña de un solo uso.

## 2. Objetos normativos de Fase 1

### 2.1 `ParametersControl`

`SIGERSA.ParametersControl` sustituye todos los ENUM de PostgreSQL. Su PK es `ParametersId BIGSERIAL`; incluye `KeyWord`, `CompanyCode`, `OCode`, `CCode`, `NumericData`, `DoubleData`, `StringData`, `BooleanData`, `DateData`, `Status` y las marcas `CUser/CDate`, `MUser/MDate`, `DUser/DDate`.

Las funciones operativas son:

- `FN_ParametersControl_Create`: crea un registro activo y devuelve su identificador.
- `FN_ParametersControl_Update`: modifica únicamente registros activos y completa auditoría.
- `FN_ParametersControl_SoftDelete`: establece `Status=false` y completa `DUser/DDate`.
- `FN_ParametersControl_GetActive`: devuelve exclusivamente registros con `Status=true`.

Se precargan 53 valores distribuidos en siete catálogos: `ESTADO_CASO`, `ESTADO_FICHA`, `ESTADO_EVALUACION`, `TIPO_ITEM_FICHA`, `TIPO_RESPUESTA`, `NIVEL_RIESGO` y `ESTADO_SINCRONIZACION`.

### 2.2 `AllItems` y versiones inmutables

`SIGERSA.AllItems` contiene 90 filas base y permite alta, edición y eliminación de la plantilla mutable. `Items` es PK serial y orden de origen; `ItemsId`, `Description`, `SectionType` y `Parents` modelan texto y jerarquía. La aplicación rechaza referencias inexistentes y ciclos.

Publicar una ficha copia la plantilla a `FICHA_INSPECCION` e `ITEM_FICHA`, conserva `source_allitems_item`, definición e integridad criptográfica. Cada `EVALUACION` referencia una versión publicada; cambios posteriores de `AllItems` no afectan evaluaciones existentes.

### 2.3 Autenticación, sesión y OTP

`USUARIO`, `USUARIO_ROL`, `ROL` y `ROL_PERMISO` implementan RBAC. `REFRESH_TOKEN` conserva únicamente hashes y permite rotación, revocación y detección de reutilización por familia. `OTP_RECUPERACION` conserva el hash del OTP, expiración, intentos y estado. `COMPROBANTE_RECUPERACION` vincula el `jti` del JWT de recuperación con el usuario, expiración y marca de uso; su consumo y el cambio de contraseña ocurren en una sola transacción.

Los roles canónicos son `ADMINISTRADOR`, `ADMINISTRADOR_EMPRESA`, `USUARIO_DELEGADO`, `COORDINADOR` y `TECNICO_EVALUADOR`. Un trigger impide asignar roles empresariales sin ámbito de empresa o con un establecimiento de otra empresa.

### 2.4 Operación, concurrencia y evidencias

Las tablas transaccionales usan UUID, instantes `timestamptz`, auditoría y `version_fila`. Las respuestas offline se deduplican mediante `OPERACION_SINCRONIZACION.idempotency_key`; una versión desactualizada provoca conflicto de concurrencia.

PostgreSQL no almacena binarios. `EVIDENCIA` conserva bucket privado, ruta lógica, nombres, tamaño, MIME y SHA-256. El adaptador de infraestructura valida ubicación, límite, MIME y firma binaria antes de cargar el contenido exclusivamente en Supabase Storage.

## 3. Verificación reproducible

Una instalación limpia debe producir cero tipos `ENUM`, 90 filas en `AllItems`, siete `KeyWord` activos y cinco roles activos. La prueba de baja lógica crea un parámetro temporal, lo actualiza, ejecuta `FN_ParametersControl_SoftDelete` y comprueba que `FN_ParametersControl_GetActive` ya no lo devuelve; la transacción de prueba se revierte.
