import { useCallback, useEffect, useState } from 'react'
import { disableWebPush, enableWebPush, getWebPushStatus, type WebPushStatus } from '../../lib/webPush'

const initialStatus: WebPushStatus = { state: 'loading', message: 'Comprobando este dispositivo…' }

export function WebPushSettings() {
  const [status, setStatus] = useState(initialStatus)
  const [busy, setBusy] = useState(false)

  const refresh = useCallback(async () => {
    try {
      setStatus(await getWebPushStatus())
    } catch (error) {
      setStatus({
        state: 'error',
        message: error instanceof Error ? error.message : 'No se pudo comprobar la suscripción push.',
      })
    }
  }, [])

  useEffect(() => {
    queueMicrotask(() => void refresh())
    const onMessage = (event: MessageEvent) => {
      if (event.data?.type === 'PUSH_SUBSCRIPTION_CHANGED') void refresh()
    }
    navigator.serviceWorker?.addEventListener('message', onMessage)
    return () => navigator.serviceWorker?.removeEventListener('message', onMessage)
  }, [refresh])

  async function run(action: () => Promise<WebPushStatus>) {
    setBusy(true)
    try {
      setStatus(await action())
    } catch (error) {
      setStatus({
        state: 'error',
        message: error instanceof Error ? error.message : 'No se pudo actualizar la suscripción push.',
      })
    } finally {
      setBusy(false)
    }
  }

  const canEnable = ['prompt', 'unsubscribed', 'server-disabled', 'error'].includes(status.state)
  const subscribed = status.state === 'subscribed'

  return (
    <article className="mt-6 overflow-hidden rounded-card bg-white shadow-card">
      <div className="border-b border-slate-100 px-5 py-4">
        <h2 className="font-extrabold text-ink-strong">Alertas críticas en este dispositivo</h2>
        <p className="mt-1 text-sm text-ink-muted">
          El permiso se solicita únicamente al pulsar Activar alertas.
        </p>
      </div>
      <div className="flex flex-wrap items-center gap-4 px-5 py-4">
        <span
          className={`h-2.5 w-2.5 rounded-full ${subscribed ? 'bg-emerald-500' : status.state === 'error' || status.state === 'denied' ? 'bg-red-500' : 'bg-amber-500'}`}
          aria-hidden="true"
        />
        <p className="min-w-0 flex-1 text-sm text-ink-body" aria-live="polite">
          {status.message}
        </p>
        {canEnable && (
          <button
            type="button"
            disabled={busy}
            onClick={() => void run(enableWebPush)}
            className="rounded-xl bg-brand-700 px-4 py-2.5 text-sm font-bold text-white disabled:opacity-50"
          >
            {busy ? 'Activando…' : 'Activar alertas'}
          </button>
        )}
        {subscribed && (
          <button
            type="button"
            disabled={busy}
            onClick={() => void run(disableWebPush)}
            className="text-brand-800 rounded-xl border border-brand-700 px-4 py-2.5 text-sm font-bold disabled:opacity-50"
          >
            {busy ? 'Desactivando…' : 'Desactivar en este dispositivo'}
          </button>
        )}
      </div>
    </article>
  )
}
