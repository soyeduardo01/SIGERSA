# Contratos REST API v1

## 1. Propósito

Este documento será la fuente de especificación para los contratos HTTP públicos de SIGERSA. Los recursos, operaciones, esquemas, reglas de validación y ejemplos se completarán antes de implementar cada caso de uso.

## 2. Lineamientos arquitectónicos

- La API seguirá Clean Architecture, separando presentación, aplicación, dominio e infraestructura.
- Los controladores adaptarán HTTP a los casos de uso y no contendrán lógica de negocio ni acceso directo a datos.
- La versión inicial se publicará bajo la ruta base `/api/v1`.
- Los recursos intercambiarán JSON con codificación UTF-8.
- Los errores HTTP usarán obligatoriamente el formato `application/problem+json` conforme a Problem Details.
- La persistencia de los casos de uso se resolverá en PostgreSQL y **todas las tablas deberán pertenecer obligatoriamente al esquema `SIGERSA`**.

## 3. Convenciones de respuesta

### 3.1 Respuesta satisfactoria

Cada operación documentará:

- código de estado HTTP;
- cuerpo de respuesta, cuando corresponda;
- encabezados relevantes;
- esquema y ejemplo JSON;
- reglas de paginación, filtrado y ordenamiento, si aplican.

### 3.2 Respuesta de error

Los errores se devolverán con `Content-Type: application/problem+json` y documentarán, como mínimo, los campos estándar `type`, `title`, `status`, `detail` e `instance`. Los errores de validación podrán agregar una extensión `errors` con los campos y mensajes correspondientes.

Ejemplo base:

```json
{
  "type": "https://sigersa.local/problems/validation-error",
  "title": "La solicitud contiene datos inválidos.",
  "status": 400,
  "detail": "Revise los campos indicados e intente nuevamente.",
  "instance": "/api/v1/casos"
}
```

## 4. Plantilla para futuros endpoints

### `[MÉTODO] /api/v1/[recurso]`

- **Caso de uso:** Pendiente de especificación.
- **Autorización y alcance:** Pendiente de especificación.
- **Parámetros:** Pendiente de especificación.
- **Cuerpo de solicitud:** Pendiente de especificación.
- **Respuesta satisfactoria:** Pendiente de especificación.
- **Respuestas `application/problem+json`:** Pendiente de especificación.
- **Idempotencia/concurrencia:** Pendiente de especificación.
- **Criterios de aceptación:** Pendiente de especificación.

## 5. Dominios por documentar

- Autenticación, sesión y OTP.
- Empresas y establecimientos.
- Casos, programación y asignaciones.
- Fichas versionadas y evaluaciones.
- Evidencias, informes y correcciones.
- Auditoría, catálogos y notificaciones.
