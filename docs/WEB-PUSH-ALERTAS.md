# Web Push: condiciones de envío y guía de prueba

## Qué quedó implementado

- El Service Worker generado por Workbox importa `public/push-sw.js`. Este escucha `push`, muestra la notificación aun con la PWA en segundo plano, identifica el aviso como **SIGERSA**, usa el icono oficial de la PWA y abre la ruta interna indicada al pulsarla.
- En **Notificaciones y sincronización** aparece el control **Activar alertas**. El navegador solo solicita permiso después de ese gesto explícito.
- La suscripción de cada navegador se guarda en `SIGERSA.SUSCRIPCION_PUSH` y queda asociada al usuario obtenido del JWT. Un usuario puede registrar varios dispositivos.
- La API envía el mensaje cifrado con VAPID a todos los dispositivos activos del usuario objetivo.
- Si el proveedor responde `404 Not Found` o `410 Gone`, el endpoint se desactiva automáticamente. Los fallos transitorios se contabilizan sin eliminar la suscripción.

## Condiciones exactas para enviar una alerta

Una alerta Web Push se intenta enviar automáticamente cuando se registra una denuncia pública y el sistema la asigna a un coordinador. El mensaje se dirige a todos los dispositivos activos de ese coordinador y no incluye el contenido confidencial de la denuncia. La persona denunciante, el técnico que tenga abierta la aplicación y otros usuarios no reciben esa alerta, salvo que alguno de ellos sea precisamente el coordinador asignado.

También puede enviarse manualmente mediante:

```http
POST /api/v1/push/users/{userId}/alerts
```

En ambos casos deben cumplirse estas condiciones:

1. `WebPush:Enabled` está en `true` y las claves VAPID son válidas.
2. Las migraciones `028_web_push_subscriptions.sql` y `029_inspection_reminder_push_dispatch.sql` fueron aplicadas.
3. El usuario objetivo activó las alertas en al menos un dispositivo y la suscripción sigue vigente.
4. El proveedor push puede alcanzarse por red. El navegador puede estar abierto, en segundo plano o cerrado; la entrega final depende del navegador y del sistema operativo.

Para los recordatorios de inspección, además debe mantenerse ejecutándose `SIGERSA.Worker`; la API por sí sola no procesa el calendario de envíos.

Para usar el endpoint manual, además, quien hace la solicitud debe estar autenticado con rol `ADMINISTRADOR` o `COORDINADOR` y no superar el límite de 10 disparos por minuto por usuario autenticado. El envío automático de una denuncia no depende de ese endpoint ni de esos dos requisitos manuales.

Los recordatorios de inspección a 30, 14 y 7 días, y el día de la inspección, disparan Web Push automáticamente cuando alcanza su fecha programada. Se envían a los mismos destinatarios del aviso interno: evaluador principal y usuarios activos con rol `ADMINISTRADOR` o `COORDINADOR`. Los avisos de 7 días y del día de la inspección permanecen visibles hasta que el usuario interactúe con ellos; los de 30 y 14 días pueden cerrarse automáticamente según el navegador.

El Worker consulta recordatorios vencidos cada 30 segundos. Cada aviso se reclama de forma atómica para evitar envíos simultáneos duplicados. Si no existe una suscripción activa o el proveedor falla, se reintenta cada 15 minutos hasta 8 veces. La etiqueta estable de la notificación permite que el navegador reemplace un reenvío accidental en lugar de mostrar dos avisos iguales.

## Configuración VAPID

Genere el par una sola vez; no rote la clave privada sin volver a suscribir los dispositivos:

```powershell
npx web-push generate-vapid-keys
```

Configure secretos locales o variables del entorno, sin guardar la clave privada en Git:

```text
WebPush__Enabled=true
WebPush__Subject=mailto:soporte@su-dominio.do
WebPush__PublicKey=CLAVE_PUBLICA_GENERADA
WebPush__PrivateKey=CLAVE_PRIVADA_GENERADA
WebPush__TimeToLiveSeconds=3600
```

La clave pública no es secreta y la API la entrega al frontend. La privada debe existir únicamente en el backend/gestor de secretos.

## Prueba manual

1. Aplique todas las migraciones, incluidas la `028` y la `029`.
2. Inicie API y frontend. En producción se requiere HTTPS; `localhost`/`127.0.0.1` se acepta para desarrollo.
3. Inicie sesión con el usuario receptor.
4. Abra **Notificaciones y sincronización**, pulse **Activar alertas** y conceda permiso.
5. Copie el UUID del usuario receptor. Autentíquese como `ADMINISTRADOR` o `COORDINADOR` y obtenga su access token.
6. Envíe la prueba:

```powershell
$token = 'ACCESS_TOKEN_ADMIN_O_COORDINADOR'
$userId = 'UUID_DEL_USUARIO_RECEPTOR'
$body = @{
  title = 'Alerta crítica de prueba'
  body = 'Se detectó una condición que requiere revisión inmediata.'
  url = '/modulo.html?module=notificaciones'
  tag = 'prueba-alerta-critica'
  requireInteraction = $true
} | ConvertTo-Json

Invoke-RestMethod `
  -Method Post `
  -Uri "http://127.0.0.1:5000/api/v1/push/users/$userId/alerts" `
  -Headers @{ Authorization = "Bearer $token" } `
  -ContentType 'application/json' `
  -Body $body
```

La respuesta informa:

- `subscriptions`: dispositivos activos encontrados.
- `sent`: entregas aceptadas por proveedores push.
- `expired`: suscripciones 404/410 desactivadas.
- `failed`: fallos transitorios o rechazos que requieren reintento/diagnóstico.

`sent` significa que el proveedor aceptó el mensaje; no garantiza que el sistema operativo lo haya mostrado. Para probar segundo plano, cierre la pestaña (sin desinstalar la PWA ni bloquear notificaciones) y vuelva a invocar el endpoint.

## Estados y errores esperados

- **Servidor sin configurar**: el botón informa que Web Push está deshabilitado.
- **Permiso denegado**: el sitio no vuelve a mostrar el prompt; el usuario debe habilitarlo en la configuración del navegador.
- **Navegador incompatible**: se conserva el centro de notificaciones interno.
- **Sin suscripciones**: el endpoint responde correctamente con contadores en cero.
- **Endpoint expirado**: se desactiva y deja de intentarse en envíos posteriores.
- **Sin conexión al activar/desactivar**: se muestra el error y el usuario puede reintentar. Si la baja local ya ocurrió, el próximo envío desactivará el endpoint remoto al recibir 404/410.

## Restricciones de seguridad aplicadas

- La API deriva el dueño de la suscripción del claim `sub` del JWT.
- Solo se aceptan endpoints HTTPS de proveedores incluidos en `AllowedEndpointHosts`, reduciendo riesgo SSRF.
- La URL que abre la notificación debe ser una ruta interna que comience con `/` y no con `//`.
- Título, cuerpo, etiqueta, claves y endpoint tienen límites y validación.
- El endpoint de envío está autorizado y limitado por tasa.
- No se registran en logs las claves `auth`, `p256dh` ni la clave VAPID privada.
