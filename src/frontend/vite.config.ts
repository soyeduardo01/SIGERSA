import react from '@vitejs/plugin-react'
import { fileURLToPath } from 'node:url'
import { defineConfig } from 'vite'
import { VitePWA } from 'vite-plugin-pwa'

export default defineConfig({
  envDir: '../../',
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: ['favicon.ico', 'apple-touch-icon-180x180.png'],
      manifest: {
        name: 'SIGERSA - Sistema Integral de Gestión de Riesgo y Seguridad Alimentaria',
        short_name: 'SIGERSA',
        description: 'Sistema Integral de Gestión de Riesgo y Seguridad Alimentaria.',
        theme_color: '#0d563f',
        background_color: '#f5f7f6',
        display: 'standalone',
        orientation: 'portrait-primary',
        scope: '/',
        start_url: '/',
        lang: 'es-DO',
        categories: ['business', 'productivity'],
        icons: [
          { src: 'pwa-192x192.png', sizes: '192x192', type: 'image/png' },
          { src: 'pwa-512x512.png', sizes: '512x512', type: 'image/png' },
          {
            src: 'maskable-icon-512x512.png',
            sizes: '512x512',
            type: 'image/png',
            purpose: 'maskable',
          },
        ],
      },
      workbox: {
        cleanupOutdatedCaches: true,
        navigateFallback: null,
        ignoreURLParametersMatching: [/^utm_/, /^module$/],
        globPatterns: ['**/*.{js,css,html,ico,png,svg,woff2}'],
        runtimeCaching: [
          {
            urlPattern: /\.(?:js|css|woff2?|png|svg|webp|ico)$/i,
            handler: 'CacheFirst',
            options: {
              cacheName: 'sigersa-static-v2',
              expiration: { maxEntries: 80, maxAgeSeconds: 60 * 60 * 24 * 30 },
              cacheableResponse: { statuses: [0, 200] },
            },
          },
          {
            urlPattern: /\/api\/v1\//i,
            handler: 'NetworkFirst',
            method: 'GET',
            options: {
              cacheName: 'sigersa-api-v1',
              networkTimeoutSeconds: 5,
              expiration: { maxEntries: 100, maxAgeSeconds: 60 * 60 * 24 },
              cacheableResponse: { statuses: [0, 200] },
            },
          },
          {
            urlPattern: /^https:\/\/[^/]+\.supabase\.co\/(?:rest|storage)\/v1\//i,
            handler: 'NetworkFirst',
            method: 'GET',
            options: {
              cacheName: 'sigersa-supabase-data-v1',
              networkTimeoutSeconds: 5,
              expiration: { maxEntries: 100, maxAgeSeconds: 60 * 60 * 24 },
              cacheableResponse: { statuses: [0, 200] },
            },
          },
        ],
      },
      devOptions: { enabled: true, type: 'module', disableRuntimeConfig: true },
    }),
  ],
  build: {
    rollupOptions: {
      input: {
        login: fileURLToPath(new URL('./index.html', import.meta.url)),
        recovery: fileURLToPath(new URL('./recuperar-clave.html', import.meta.url)),
        module: fileURLToPath(new URL('./modulo.html', import.meta.url)),
      },
    },
  },
})
