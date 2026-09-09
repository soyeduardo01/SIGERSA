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
| `PUT /all-items/{id}` | Administrador | Edita texto o jerarquía, rechazando ciclos. |
| `DELETE /all-items/{id}` | Administrador | Elimina un nodo sin descendientes. |
| `POST /inspection-templates/publish-all-items` | Administrador | Publica una versión inmutable de la ficha y reglas. |

## 4. Evaluación, riesgo y evidencias

| Método y ruta | Acceso | Uso |
| --- | --- | --- |
| `POST /evaluations` | Administrador/Coordinador | Crea y asigna una evaluación contra versiones publicadas. |
| `GET /evaluations/{id}/form` | Según rol y ámbito | Devuelve la instantánea inmutable autorizada. |
| `POST /respuestas` | Administrador/Técnico asignado | Guarda una respuesta con `Idempotency-Key` y versión base opcional. |
| `POST /evaluations/{id}/calculate` | Administrador/Coordinador/Técnico asignado | Calcula y persiste cumplimiento y riesgo en servidor. |
| `POST /risk/all-items/calculate` | Autenticado | Calcula recursivamente una vista previa sobre `AllItems`. |
| `POST /evidences` | Administrador/Técnico asignado | Valida y carga multipart exclusivamente a Supabase Storage. |

Una evaluación o ítem inexistente dentro del ámbito del actor responde `404`; credenciales inválidas responden `401`; rol insuficiente responde `403`; y una versión obsoleta o clave idempotente reutilizada con otro contenido responde `409`.

## 5. Operación

`GET /system/status` comprueba el proceso sin revelar configuración. Swagger UI y `/swagger/v1/swagger.json` se exponen únicamente en desarrollo. Ninguna respuesta, log o documento OpenAPI contiene contraseñas, OTP, tokens ni claves de proveedores.
