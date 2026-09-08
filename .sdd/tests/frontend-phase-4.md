# Validación SDD — Frontend Fase 4

## Criterios automatizados

- El formateo debe pasar con `pnpm format:check`.
- ESLint debe finalizar sin advertencias con `pnpm lint`.
- TypeScript debe compilar sin emitir archivos con `pnpm typecheck`.
- Las pruebas unitarias deben pasar con `pnpm test`.
- La compilación `pnpm build` debe generar `manifest.webmanifest`, `registerSW.js`, `sw.js` y los iconos PWA.

## Escenarios funcionales requeridos

1. Una respuesta creada sin red queda en `syncQueue` con `idempotency_key` única.
2. Una evidencia sin red conserva metadata y `Blob` sin intentar cargarlo.
3. Al volver la conectividad, la cola envía respuestas, carga evidencias en Supabase y registra su metadata.
4. Repetir una mutación con la misma `idempotency_key` no crea otra entrada local.
5. Los fallos de red incrementan los intentos y programan un nuevo reintento.
6. La interfaz distingue por texto y color los estados BPM y niveles de riesgo.
7. El menú adaptable puede abrirse con teclado y cerrarse con `Escape`.

## Verificaciones manuales pendientes de integración

- Confirmar instalación de la PWA y navegación sin red en un navegador compatible.
- Validar las políticas RLS y el bucket privado contra un proyecto real de Supabase.
- Verificar sincronización extremo a extremo cuando la API v1 esté disponible.
