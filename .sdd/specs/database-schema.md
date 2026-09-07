# Especificación de persistencia y esquema de datos

## 1. Propósito

Este documento define las directrices arquitectónicas iniciales para la persistencia transaccional de SIGERSA. El DDL detallado, las relaciones, los índices y las migraciones se especificarán en fases posteriores.

## 2. Directrices obligatorias

### 2.1 Motor y esquema

- El motor transaccional será PostgreSQL 15 o superior.
- **Todas las tablas de SIGERSA deberán crearse y consultarse de forma estricta en el esquema `SIGERSA`.**
- No se crearán tablas de negocio en `public` ni se dependerá del `search_path` implícito.
- El SQL deberá calificar explícitamente los objetos, por ejemplo: `SIGERSA.evaluacion`.
- Las migraciones deberán crear o verificar el esquema `SIGERSA` antes de crear cualquier tabla.

### 2.2 Acceso a datos

- Dapper será el micro-ORM obligatorio para consultas y transacciones.
- **Entity Framework está prohibido como ORM para las transacciones y la persistencia del sistema.**
- Las consultas deberán parametrizar sus valores; no se admitirá concatenación de entradas en SQL.
- Las transacciones deberán delimitarse explícitamente en la capa de infraestructura y respetar los casos de uso de la aplicación.

### 2.3 Tipos de datos principales

| Necesidad | Tipo PostgreSQL | Directriz |
| --- | --- | --- |
| Identificadores | `uuid` | Usar claves UUID para entidades y agregados. |
| Fechas y horas | `timestamptz` | Persistir instantes normalizados en UTC. |
| Datos flexibles | `jsonb` | Reservar para estructuras variables con una justificación explícita; indexar según los patrones de consulta. |

## 3. Convenciones iniciales

- Los nombres físicos de tablas, columnas, restricciones e índices usarán una convención consistente en `snake_case`.
- Cada tabla transaccional deberá definir su clave primaria y las restricciones de integridad aplicables.
- Los cambios de esquema deberán ser reproducibles, versionados y ejecutados mediante migraciones SQL.
- Los campos de auditoría temporal usarán `timestamptz` en UTC.
- La información histórica de una evaluación deberá conservar la versión inmutable de la ficha y las reglas utilizadas.

## 4. Fuente de verdad

PostgreSQL será la única fuente de verdad para la persistencia del sistema. Cualquier mecanismo futuro de optimización deberá documentarse y aprobarse mediante una decisión arquitectónica antes de incorporarse.
