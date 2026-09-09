# Arquitectura Frontend PWA

## Propósito

El frontend de SIGERSA es una PWA móvil primero orientada al trabajo de campo. Debe permitir consultar fichas y registrar respuestas o evidencias aun cuando la conectividad sea intermitente.

## Stack y límites

- React con TypeScript y Vite para interfaz y empaquetado.
- Tailwind CSS con tokens semánticos para cumplimiento BPM (`C`, `CP`, `IT`) y riesgo (`BAJO`, `MEDIO`, `ALTO`).
- `vite-plugin-pwa` y Workbox para manifiesto, Service Worker y estrategias de caché.
- Dexie sobre IndexedDB para evaluaciones, catálogo de fichas y cola de sincronización.
- Cliente oficial de Supabase para enviar binarios a Storage.
- API REST v1 para persistir respuestas y metadatos de evidencia.

## Flujo offline

1. Cada mutación recibe una `idempotency_key` antes de almacenarse localmente.
2. IndexedDB conserva la carga útil y, para evidencias, el `Blob` hasta que exista conexión.
3. Al recuperar la red, las respuestas se envían a `/api/v1/respuestas` con el encabezado `Idempotency-Key`.
4. La PWA solicita a la API una autorización firmada para una evaluación asignada, carga el binario directamente al bucket privado mediante `supabase-js` y confirma la operación en `/api/v1/evidences/confirm`. La API vuelve a descargar y valida tamaño, firma y SHA-256 antes de registrar metadatos.
5. Los fallos se reintentan con espera incremental y un máximo controlado de intentos. El backend debe conservar la idempotencia como autoridad final.

## Estrategias de caché

- Assets versionados: `CacheFirst`.
- Lecturas REST API y lecturas de Supabase: `NetworkFirst` con tiempo máximo y copia local.
- Mutaciones: nunca se resuelven desde caché; pasan por la cola IndexedDB.

## Seguridad

Solo se exponen en Vite la URL y la clave pública de Supabase. El bucket de evidencias debe permanecer privado y protegido con RLS. Las claves de servicio, secretos de base de datos y credenciales administrativas no se incorporan al bundle.

## Accesibilidad

La navegación base incluye enlace para saltar al contenido, regiones semánticas, etiquetas accesibles, foco visible, control por teclado y respeto a la preferencia de movimiento reducido. Los colores semánticos se acompañan siempre de texto.
