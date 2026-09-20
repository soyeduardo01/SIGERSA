/* Web Push events imported by the Workbox-generated service worker. */
self.SIGERSA_PUSH_BRAND = Object.freeze({
  name: 'SIGERSA',
  icon: '/pwa-192x192.png',
  badge: '/pwa-64x64.png',
})

self.addEventListener('push', (event) => {
  let payload = {}
  try {
    payload = event.data ? event.data.json() : {}
  } catch {
    payload = { body: event.data ? event.data.text() : '' }
  }

  const receivedTitle = typeof payload.title === 'string' && payload.title.trim()
    ? payload.title.trim()
    : 'Alerta crítica'
  const title = /^SIGERSA\b/i.test(receivedTitle)
    ? receivedTitle
    : `${self.SIGERSA_PUSH_BRAND.name} · ${receivedTitle}`
  const body = typeof payload.body === 'string' ? payload.body : 'Tiene una nueva alerta crítica.'
  const requestedUrl = typeof payload.url === 'string' ? payload.url : '/modulo.html?module=notificaciones'
  const url = requestedUrl.startsWith('/') && !requestedUrl.startsWith('//')
    ? requestedUrl
    : '/modulo.html?module=notificaciones'

  event.waitUntil(self.registration.showNotification(title, {
    body,
    icon: self.SIGERSA_PUSH_BRAND.icon,
    badge: self.SIGERSA_PUSH_BRAND.badge,
    tag: typeof payload.tag === 'string' ? payload.tag : 'sigersa-critical-alert',
    renotify: true,
    requireInteraction: payload.requireInteraction !== false,
    data: { url },
  }))
})

self.addEventListener('notificationclick', (event) => {
  event.notification.close()
  const url = event.notification.data?.url || '/modulo.html?module=notificaciones'
  event.waitUntil((async () => {
    const target = new URL(url, self.location.origin)
    const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true })
    for (const client of windows) {
      if (new URL(client.url).origin === target.origin) {
        await client.navigate(target.href)
        return client.focus()
      }
    }
    return self.clients.openWindow(target.href)
  })())
})

self.addEventListener('pushsubscriptionchange', (event) => {
  event.waitUntil((async () => {
    const windows = await self.clients.matchAll({ type: 'window', includeUncontrolled: true })
    windows.forEach((client) => client.postMessage({ type: 'PUSH_SUBSCRIPTION_CHANGED' }))
  })())
})
