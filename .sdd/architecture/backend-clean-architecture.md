# Arquitectura backend SIGERSA

## Plataforma

El backend usa .NET 10 y una solución modular basada en Clean Architecture. La dependencia de código apunta hacia el dominio:

- `SIGERSA.Domain`: entidades, excepciones y contratos de repositorios y almacenamiento.
- `SIGERSA.Application`: casos de uso, DTOs y validadores FluentValidation.
- `SIGERSA.Infrastructure`: Dapper/Npgsql, repositorios y adaptador de Supabase Storage.
- `SIGERSA.Api`: API REST v1, Problem Details, OpenAPI y composición de dependencias.
- `SIGERSA.Worker`: procesamiento asíncrono futuro de la bandeja `OUTBOX`.
- `SIGERSA.Tests`: pruebas automatizadas del dominio, aplicación e infraestructura.

## Persistencia

PostgreSQL es la fuente transaccional. Toda sentencia Dapper debe calificar explícitamente tablas con el esquema `"SIGERSA"`; además, la fábrica de conexiones configura `search_path` como defensa adicional. Las actualizaciones incorporan `version_fila` en el predicado y producen un conflicto cuando ninguna fila coincide.

Las cinco columnas comunes de auditoría son `creado_en`, `creado_por`, `modificado_en`, `modificado_por` y `version_fila`. La trazabilidad crítica se concentra en `"SIGERSA"."AUDITORIA_EVENTO"`.

## Evidencias

Los binarios se almacenan exclusivamente en Supabase Storage. PostgreSQL conserva sus metadatos. Antes de subir un archivo, el adaptador valida bucket, ruta, límite de tamaño, MIME permitido y firma binaria; luego calcula SHA-256. Las claves de Supabase se suministran mediante variables de entorno o un proveedor seguro y nunca se versionan.

## API y observabilidad

Los errores se serializan como `application/problem+json` e incluyen un `traceId`. Los conflictos de concurrencia retornan HTTP 409 y los errores de validación HTTP 400. Serilog registra método, ruta, estado, duración y contexto técnico, sin registrar cuerpos ni secretos. Swagger publica el contrato REST v1 en desarrollo.

## Decisiones de alcance

- No se usa Redis.
- No se usa Entity Framework para transacciones.
- La migración SQL no se ejecuta automáticamente durante el arranque.
- Supabase Storage sustituye cualquier almacenamiento físico local de evidencias.
