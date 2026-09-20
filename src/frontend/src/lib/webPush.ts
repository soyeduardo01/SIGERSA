import {
  getWebPushConfiguration,
  registerWebPushSubscription,
  removeWebPushSubscription,
} from './api'

export type WebPushState =
  | 'loading'
  | 'unsupported'
  | 'server-disabled'
  | 'prompt'
  | 'denied'
  | 'subscribed'
  | 'unsubscribed'
  | 'error'

export interface WebPushStatus {
  state: WebPushState
  message: string
}

const unsupported: WebPushStatus = {
  state: 'unsupported',
  message: 'Este navegador no admite notificaciones push.',
}

export async function getWebPushStatus(): Promise<WebPushStatus> {
  if (!supportsWebPush()) return unsupported
  if (Notification.permission === 'denied') {
    return { state: 'denied', message: 'El navegador bloqueó las notificaciones para este sitio.' }
  }

  const configuration = await getWebPushConfiguration()
  if (!configuration.enabled || !configuration.publicKey) {
    return { state: 'server-disabled', message: 'El servidor todavía no tiene Web Push configurado.' }
  }

  const registration = await navigator.serviceWorker.ready
  const subscription = await registration.pushManager.getSubscription()
  if (subscription) {
    await saveSubscription(subscription)
    return { state: 'subscribed', message: 'Este dispositivo recibe alertas críticas.' }
  }

  return Notification.permission === 'granted'
    ? { state: 'unsubscribed', message: 'Las alertas están permitidas, pero este dispositivo no está suscrito.' }
    : { state: 'prompt', message: 'Active las alertas para recibir avisos aunque SIGERSA esté cerrado.' }
}

export async function enableWebPush(): Promise<WebPushStatus> {
  if (!supportsWebPush()) return unsupported
  const configuration = await getWebPushConfiguration()
  if (!configuration.enabled || !configuration.publicKey) {
    return { state: 'server-disabled', message: 'El servidor todavía no tiene Web Push configurado.' }
  }

  const permission = await Notification.requestPermission()
  if (permission !== 'granted') {
    return {
      state: permission === 'denied' ? 'denied' : 'prompt',
      message: permission === 'denied'
        ? 'El navegador bloqueó las notificaciones. Habilítelas desde la configuración del sitio.'
        : 'No se concedió permiso para mostrar notificaciones.',
    }
  }

  const registration = await navigator.serviceWorker.ready
  let subscription = await registration.pushManager.getSubscription()
  subscription ??= await registration.pushManager.subscribe({
    userVisibleOnly: true,
    applicationServerKey: base64UrlToBytes(configuration.publicKey),
  })
  await saveSubscription(subscription)
  return { state: 'subscribed', message: 'Este dispositivo recibe alertas críticas.' }
}

export async function disableWebPush(): Promise<WebPushStatus> {
  if (!supportsWebPush()) return unsupported
  const registration = await navigator.serviceWorker.ready
  const subscription = await registration.pushManager.getSubscription()
  if (subscription) {
    const endpoint = subscription.endpoint
    await subscription.unsubscribe()
    await removeWebPushSubscription(endpoint)
  }
  return { state: 'unsubscribed', message: 'Las alertas están desactivadas en este dispositivo.' }
}

async function saveSubscription(subscription: PushSubscription) {
  const json = subscription.toJSON()
  if (!json.endpoint || !json.keys?.p256dh || !json.keys.auth) {
    throw new Error('El navegador generó una suscripción push incompleta.')
  }
  await registerWebPushSubscription({
    endpoint: json.endpoint,
    expirationTime: json.expirationTime ? new Date(json.expirationTime).toISOString() : null,
    keys: { p256dh: json.keys.p256dh, auth: json.keys.auth },
    userAgent: navigator.userAgent,
  })
}

function supportsWebPush() {
  return 'serviceWorker' in navigator && 'PushManager' in window && 'Notification' in window
}

function base64UrlToBytes(value: string) {
  const padding = '='.repeat((4 - (value.length % 4)) % 4)
  const raw = atob((value + padding).replace(/-/g, '+').replace(/_/g, '/'))
  return Uint8Array.from(raw, (character) => character.charCodeAt(0))
}
