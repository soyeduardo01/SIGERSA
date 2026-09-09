# Evidencia de validación — Fase 1, pasos 1 y 2

Fecha: 2026-09-11  
Rama: `dev-eduardo`

## Resultado ejecutivo

| Alcance | Resultado | Evidencia |
| --- | --- | --- |
| Paso 1 — Base de datos | Aprobado | Migraciones `001` a `005` aplicadas sobre una base vacía y `005` aplicada sobre la base existente. |
| Paso 2 — Código backend | Aprobado | Compilación .NET 10 sin advertencias; 47 pruebas unitarias aprobadas; API inició y respondió estado `200`. |
| Paso 2 — Supabase remoto | Pendiente de credencial | El adaptador y sus pruebas están aprobados, pero la clave local de servidor fue rechazada con `401`; la clave secreta recibida estaba enmascarada y no puede reconstruirse. |

## Paso 1 — Comprobaciones ejecutadas

Se creó una base temporal, se aplicaron secuencialmente todas las migraciones y luego se eliminó esa base temporal. Resultado observado:

- tipos PostgreSQL `ENUM` dentro de `SIGERSA`: `0`;
- filas de `SIGERSA.AllItems`: `90`;
- catálogos activos distintos en `ParametersControl`: `7`;
- valores de catálogo precargados: `53`;
- roles activos: `5`;
- referencias padre huérfanas de `AllItems`: `0`;
- migraciones limpias: aprobadas.

En la base local se ejecutó, dentro de una transacción revertida, el ciclo completo `FN_ParametersControl_Create` → `FN_ParametersControl_Update` → `FN_ParametersControl_SoftDelete` → `FN_ParametersControl_GetActive`. La consulta activa devolvió cero filas después de la baja lógica y la prueba terminó con `OK`.

## Paso 2 — Comprobaciones ejecutadas

- Solución `SIGERSA.sln`: compilación aprobada en .NET 10, cero errores y cero advertencias.
- Pruebas backend: `47/47` aprobadas.
- Formato backend: `dotnet format --verify-no-changes`, aprobado.
- Arranque real de la API con configuración local ignorada: aprobado.
- `GET /api/v1/system/status`: HTTP `200`, servicio `ok`, .NET `10.0`.
- Swagger generado desde el ensamblado: HTTP `200`, `19` rutas; contrato guardado en `.sdd/specs/openapi-v1.json`.
- Auth: hash PBKDF2 con sal, bloqueo, refresh rotativo, detección de reutilización, logout con revocación, OTP consumible y comprobante de recuperación de un solo uso cubiertos por pruebas.
- Dapper: repositorios de parámetros, `AllItems`, autenticación, evaluación y evidencias compilados; guardia de esquema exige nombres calificados con `"SIGERSA"`.
- Riesgo: cálculo BPM, exclusión de no aplicables, agregación recursiva, límites de nivel y detección de ciclos cubiertos por pruebas.
- Concurrencia/idempotencia: conflicto optimista y reutilización segura de operación cubiertos por pruebas y flujo de persistencia.
- SMTP: configuración solo local; el flujo OTP usa el adaptador SMTP y no registra el código.
- Supabase Storage: autorización por asignación comprobada antes de transferir; persistencia de ruta, tamaño, MIME y SHA-256 comprobada con dobles de prueba.

## Seguridad de configuración

`.env.local` está ignorado por `.gitignore` y no está rastreado. El escaneo de los 161 archivos versionables no encontró coincidencias exactas con la cadena de conexión, clave JWT, cuenta/contraseña SMTP ni claves Supabase locales; tampoco encontró patrones de clave secreta Supabase.

La clave publicable local fue aceptada por Supabase (`200`). La clave configurada para el backend fue rechazada (`401`) y tiene formato incompleto. Para probar una carga real a un bucket privado hace falta proporcionar localmente el valor completo `sb_secret_...` como `Supabase__Key`; ese valor no debe enviarse a Git ni incorporarse en documentación, ejemplos o logs.
