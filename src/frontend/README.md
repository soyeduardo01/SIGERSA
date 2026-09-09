# SIGERSA Frontend PWA

Aplicación móvil primero para inspecciones EBR/BPM. Utiliza React, TypeScript, Vite, Tailwind CSS, Dexie/IndexedDB y Supabase Storage.

## Configuración local

1. Copie `.env.example` como `.env.local`.
2. Complete `VITE_SUPABASE_URL` y `VITE_SUPABASE_PUBLISHABLE_KEY` con las credenciales públicas del proyecto.
3. Instale e inicie:

```powershell
pnpm install
pnpm dev
```

## Calidad

```powershell
pnpm format:check
pnpm lint
pnpm typecheck
pnpm test
pnpm build
```

`pnpm build` produce una aplicación instalable con manifiesto y Service Worker. La cola IndexedDB reintenta respuestas y evidencias pendientes usando su `idempotency_key` al recuperar la conexión.

## Seguridad de Supabase

El navegador solo puede recibir la clave pública (`anon` o publishable). El bucket de evidencias debe ser privado y aplicar políticas RLS por usuario/rol; las claves de servicio pertenecen exclusivamente al backend.
