# Contratos REST API v1 — Fase 1

## 1. Contrato ejecutable

El contrato normativo y legible por herramientas es `.sdd/specs/openapi-v1.json`, generado desde el ensamblado de la API. Todas las rutas usan `/api/v1`, JSON UTF-8 salvo la carga multipart de evidencia y errores `application/problem+json`. Los endpoints requieren JWT de propósito `access` salvo los marcados como públicos.

## 2. Autenticación

| Método y ruta | Acceso | Resultado principal |
| --- | --- | --- |
| `POST /auth/login` | Público, limitado | JWT de acceso y refresh token rotativo. |
| `POST /auth/refresh` | Público, limitado | Rota el refresh token; reutilizar uno revocado invalida su familia. |
| `POST /auth/logout` | Autenticado | Revoca el refresh token presentado; respuesta `204`. |
| `POST /auth/password-recovery/request` | Público, limitado | Respuesta neutral `202`; envía OTP si la cuenta es elegible. |
| `POST /auth/password-recovery/verify` | Público, limitado | Consume el OTP y emite JWT de recuperación de un solo uso. |
| `POST /auth/password-recovery/reset` | JWT `password_reset`, limitado | Consume atómicamente el comprobante, cambia la contraseña y revoca sesiones. |

## 3. Parámetros y ficha mutable

| Método y ruta | Acceso | Uso |
| --- | --- | --- |
| `GET /parameters/{keyWord}` | Autenticado | Consulta solo parámetros activos. |
| `POST /parameters` | Administrador | Alta auditada. |
| `PUT /parameters/{id}` | Administrador | Edición auditada. |
| `DELETE /parameters/{id}` | Administrador | Baja lógica. |
| `GET /all-items` | Autenticado | Lee la plantilla mutable. |
| `POST /all-items` | Administrador | Agrega un nodo. |
| `PUT /all-items/{id}` | Administrador | Edita texto o jerarquía, actualiza las referencias de hijos y rechaza ciclos o tipos incompatibles. |
| `DELETE /all-items/{id}` | Administrador | Elimina una hoja; si tiene hijos exige `childStrategy=SUBTREE` o `REPARENT`, con `reparentToItems` opcional. |
| `PUT /all-items/order` | Administrador | Reordena todos los nodos en una transacción, sin permitir hijos antes de sus padres. |
| `POST /inspection-templates/publish-all-items` | Administrador | Publica una versión inmutable de la ficha y reglas. |

## 4. Gestión de usuarios

| Método y ruta | Acceso | Uso |
| --- | --- | --- |
| `GET /users` | Administrador/Administrador Empresa/Coordinador | Lista paginada con búsqueda y filtros; el ámbito de empresa se aplica en servidor. |
| `GET /users/options` | Administrador/Administrador Empresa/Coordinador | Devuelve roles y empresas asignables, además de la facultad de edición del actor. |
| `POST /users` | Administrador/Administrador Empresa | Crea una cuenta y asigna un rol permitido; los roles empresariales requieren empresa. |
| `PUT /users/{id}` | Administrador/Administrador Empresa | Actualiza datos, rol y estado con control de versión y ámbito. |
| `POST /users/{id}/suspension` | Administrador/Administrador Empresa | Bloquea o activa una cuenta con confirmación de versión; impide el autobloqueo. |

## 5. Evaluación, riesgo y evidencias

| Método y ruta | Acceso | Uso |
| --- | --- | --- |
| `GET /evaluations` | Según rol y ámbito | Lista evaluaciones globales, propias de empresa o asignadas al técnico. |
| `GET /evaluations/options` | Según rol; edición Administrador/Coordinador | Devuelve casos, establecimientos, fichas, reglas y técnicos válidos para crear. |
| `POST /evaluations` | Administrador/Coordinador | Crea y asigna una evaluación contra versiones publicadas. |
| `GET /evaluations/{id}/form` | Según rol y ámbito | Devuelve la instantánea inmutable autorizada. |
| `POST /respuestas` | Administrador/Técnico asignado | Guarda una respuesta con `Idempotency-Key` y versión base opcional. |
| `POST /evaluations/{id}/calculate` | Administrador/Coordinador/Técnico asignado | Calcula y persiste cumplimiento y riesgo en servidor. |
| `POST /risk/all-items/calculate` | Autenticado | Calcula recursivamente una vista previa sobre `AllItems`. |
| `POST /evidences/upload-authorization` | Administrador/Técnico asignado | Emite una autorización firmada para una ruta privada determinista. |
| `POST /evidences/confirm` | Administrador/Técnico asignado | Verifica el objeto remoto y registra sus metadatos de forma idempotente. |
| `POST /evidences` | Administrador/Técnico asignado | Valida y carga multipart exclusivamente a Supabase Storage. |
| `GET /evidences` | Según rol y ámbito | Lista únicamente metadatos de evidencias autorizadas. |
| `GET /evidences/{id}/content` | Según rol y ámbito | Descarga desde el bucket privado después de validar la relación con la evaluación. |

Una evaluación o ítem inexistente dentro del ámbito del actor responde `404`; credenciales inválidas responden `401`; rol insuficiente responde `403`; y una versión obsoleta o clave idempotente reutilizada con otro contenido responde `409`.

## 6. Flujo operativo

| Método y ruta | Acceso | Uso |
| --- | --- | --- |
| `GET /requests` y `GET /requests/options` | Según rol y ámbito | Lista solicitudes y opciones limitadas al ámbito global, empresarial o asignado. |
| `POST /requests` | Administrador/Empresa/Delegado/Coordinador | Crea idempotentemente una solicitud propia o autorizada. |
| `PUT /requests/{id}` | Mismos operadores, dentro del ámbito | Actualiza con versión optimista. |
| `POST /requests/{id}/submit` o `/cancel` | Mismos operadores, dentro del ámbito | Ejecuta una transición válida con control de versión. |
| `GET /cases` y `GET /cases/options` | Según rol y ámbito | Lista casos globales, empresariales o asignados. |
| `POST /cases`, `PUT /cases/{id}`, `POST /cases/{id}/close` | Administrador/Coordinador | Crea, analiza, prioriza y cierra con idempotencia/concurrencia. |
| `GET /schedules` y `GET /schedules/options` | Administrador/Coordinador | Lista programación y catálogos de casos/técnicos. |
| `POST /schedules`, `PUT /schedules/{id}`, `POST /schedules/{id}/cancel` | Administrador/Coordinador | Programa, reasigna o cancela visitas en transacciones auditadas. |
| `GET /corrections` y `GET /corrections/options` | Según rol y ámbito | Lista correcciones globales, empresariales habilitadas o asignadas. |
| `POST /corrections` | Administrador/Coordinador | Solicita una corrección idempotente y marca la evaluación en corrección. |
| `POST /corrections/{id}/submit` | Responsable autorizado | Envía la corrección sin permitir actuación fuera de empresa/asignación. |
| `POST /corrections/{id}/accept` o `/reject` | Administrador/Coordinador | Resuelve con separación de funciones y control de versión. |

## 7. Operación técnica

`GET /system/status` comprueba el proceso sin revelar configuración. Swagger UI y `/swagger/v1/swagger.json` se exponen únicamente en desarrollo. Ninguna respuesta, log o documento OpenAPI contiene contraseñas, OTP, tokens ni claves de proveedores.
