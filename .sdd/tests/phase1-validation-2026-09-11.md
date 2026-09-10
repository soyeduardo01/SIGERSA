# Evidencia de validación — Fase 1, pasos 1 y 2

Fecha: 2026-09-11  
Rama: `dev-eduardo`

## Resultado ejecutivo

| Alcance | Resultado | Evidencia |
| --- | --- | --- |
| Paso 1 — Base de datos | Aprobado | Migraciones `001` a `006` aplicadas sobre una base vacía y las incrementales aplicadas sobre la base existente. |
| Paso 2 — Código backend | Aprobado | Compilación .NET 10 sin advertencias; 55 pruebas unitarias aprobadas; API inició y respondió estado `200`. |
| Paso 3 — PWA | Aprobado localmente | TypeScript, formato, lint, 5 pruebas y build PWA aprobados; ficha por evaluación, CRUD/reordenamiento, recuperación OTP y cola offline implementados. El flujo login/recuperación también fue inspeccionado en navegador real. |
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
- migración `006` reaplicada sobre la base existente: aprobada de forma idempotente;
- índices finales de evidencia: hash no único `IX_EVIDENCIA_HASH` y operación única `UQ_EVIDENCIA_IDEMPOTENCY_KEY`, ambos presentes.

En la base local se ejecutó, dentro de una transacción revertida, el ciclo completo `FN_ParametersControl_Create` → `FN_ParametersControl_Update` → `FN_ParametersControl_SoftDelete` → `FN_ParametersControl_GetActive`. La consulta activa devolvió cero filas después de la baja lógica y la prueba terminó con `OK`.

## Paso 2 — Comprobaciones ejecutadas

- Solución `SIGERSA.sln`: compilación aprobada en .NET 10, cero errores y cero advertencias.
- Pruebas backend: `55/55` aprobadas.
- Formato backend: `dotnet format --verify-no-changes`, aprobado.
- Arranque real de la API con configuración local ignorada: aprobado.
- `GET /api/v1/system/status`: HTTP `200`, servicio `ok`, .NET `10.0`.
- Swagger generado desde el ensamblado: HTTP `200`, `22` rutas; contrato guardado en `.sdd/specs/openapi-v1.json`.
- Auth: hash PBKDF2 con sal, bloqueo, refresh rotativo, detección de reutilización, logout con revocación, OTP consumible y comprobante de recuperación de un solo uso cubiertos por pruebas.
- Dapper: repositorios de parámetros, `AllItems`, autenticación, evaluación y evidencias compilados; guardia de esquema exige nombres calificados con `"SIGERSA"`.
- Riesgo: cálculo BPM, exclusión de no aplicables, agregación recursiva, límites de nivel y detección de ciclos cubiertos por pruebas.
- Concurrencia/idempotencia: conflicto optimista y reutilización segura de operación cubiertos por pruebas y flujo de persistencia.
- SMTP: configuración solo local; el flujo OTP usa el adaptador SMTP y no registra el código.
- Supabase Storage: autorización firmada posterior a validar asignación, ruta determinista, verificación remota y persistencia idempotente de ruta, tamaño, MIME y SHA-256 comprobadas con dobles de prueba.
- Ficha mutable: la eliminación de padres exige elegir entre subárbol y reubicación; el orden conserva padres antes que hijos y las combinaciones `C/S/SS/A/I` se validan. Las operaciones se probaron contra API y PostgreSQL reales: ausencia de estrategia devolvió `409`, `REPARENT` conservó y reasignó el hijo, `SUBTREE` dejó cero nodos temporales, el reordenamiento completo conservó 90 nodos y el cambio de código del padre actualizó atómicamente la referencia del hijo.
- Navegador: se verificaron la pantalla de inicio de sesión, el enlace de recuperación, el formulario de solicitud de OTP, la validación obligatoria del correo y el retorno al login. Playwright CLI no pudo usar su distribución predeterminada porque Google Chrome no está instalado; la comprobación se completó con el navegador integrado de Codex.

## Flujo E2E ejecutado

Sobre una base temporal creada desde las seis migraciones se ejecutó la API real y se comprobó: login de Administrador; lectura y reordenamiento reversible de los 90 nodos; publicación de una ficha de 90 nodos; creación y asignación de una evaluación; login del Técnico Evaluador; descarga de la instantánea con 90 nodos y 45 preguntas; guardado de dos respuestas; repetición de una respuesta con la misma clave idempotente; y cálculo persistido. La repetición devolvió el mismo identificador lógico y el cálculo produjo 50 % de cumplimiento, riesgo total 4, nivel `MEDIO` y frecuencia `SEMESTRAL`. La base temporal fue eliminada al terminar.

## Seguridad de configuración

`.env.local` está ignorado por `.gitignore` y no está rastreado. El escaneo final de todos los archivos versionables no encontró coincidencias exactas con la cadena de conexión, clave JWT, cuenta/contraseña SMTP ni claves Supabase locales; tampoco encontró patrones de clave secreta Supabase.

La clave publicable local fue aceptada por Supabase (`200`). La clave configurada para el backend fue rechazada (`401`) y tiene formato incompleto. Para probar una carga real a un bucket privado hace falta proporcionar localmente el valor completo `sb_secret_...` como `Supabase__Key`; ese valor no debe enviarse a Git ni incorporarse en documentación, ejemplos o logs.
