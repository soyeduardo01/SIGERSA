import { useCallback, useEffect, useState } from 'react'
import { useAuth } from '../../contexts/useAuth'
import { alerts } from '../../lib/alerts'
import { formatStatusLabel } from '../../lib/formatters'
import { getNotifications, markNotificationRead, type SystemNotification } from '../../lib/api'
import { offlineDb, type SyncQueueItem } from '../../offline/database'
import {
  discardSyncMutation,
  flushSyncQueue,
  retrySyncMutation,
  subscribeToSyncQueue,
} from '../../offline/syncQueue'
import { AllItemsAdmin } from '../admin/AllItemsAdmin'
import { ParametersManagement } from '../admin/ParametersManagement'
import { CasesManagement } from '../cases/CasesManagement'
import { CorrectionsManagement } from '../corrections/CorrectionsManagement'
import { CompaniesManagement } from '../companies/CompaniesManagement'
import { EvaluationsManagement } from '../evaluations/EvaluationsManagement'
import { EvidenceManagement } from '../evidences/EvidenceManagement'
import { EstablishmentsManagement } from '../establishments/EstablishmentsManagement'
import { RequestsManagement } from '../requests/RequestsManagement'
import { SchedulingManagement } from '../scheduling/SchedulingManagement'
import { ProfileManagement } from '../profile/ProfileManagement'
import { DashboardOverview } from './DashboardOverview'
import {
  AuditManagement,
  FindingsManagement,
  HistoryManagement,
  SurveillanceManagement,
} from './OperationalDataPages'

function useRole(...allowed: string[]) {
  const { roles } = useAuth()
  return roles.some((role) => allowed.includes(role))
}

export function OverviewPage() {
  return <DashboardOverview />
}

export function CompaniesPage() {
  return <CompaniesManagement />
}
export function EstablishmentsPage() {
  return <EstablishmentsManagement />
}
export function ParametersPage() {
  return <ParametersManagement />
}
export function RequestsPage() {
  return <RequestsManagement />
}
export function AlertsPage() {
  return <SurveillanceManagement />
}
export function CasesPage() {
  return <CasesManagement />
}
export function SchedulingPage() {
  return <SchedulingManagement />
}
export function FindingsPage() {
  return <FindingsManagement />
}
export function EvidencePage() {
  return <EvidenceManagement />
}
export function CorrectionsPage() {
  return <CorrectionsManagement />
}
export function ReportsPage() {
  return <HistoryManagement />
}
export function AuditPage() {
  return <AuditManagement />
}

export function EvaluationsPage() {
  return <EvaluationsManagement />
}

export function FichasPage() {
  const canEdit = useRole('ADMINISTRADOR')
  return <AllItemsAdmin readOnly={!canEdit} />
}

export function ProfilePage() {
  return <ProfileManagement />
}

export function NotificationsPage() {
  const [items, setItems] = useState<SyncQueueItem[]>([])
  const [notifications, setNotifications] = useState<SystemNotification[]>([])
  const [notificationError, setNotificationError] = useState('')
  const [syncing, setSyncing] = useState(false)
  const { identity } = useAuth()

  const loadQueue = useCallback(async () => {
    const allItems = await offlineDb.syncQueue.orderBy('createdAt').reverse().toArray()
    setItems(allItems.filter((item) => item.ownerUserId === identity?.id))
  }, [identity?.id])

  const loadNotifications = useCallback(async () => {
    try {
      setNotifications(await getNotifications())
      setNotificationError('')
    } catch (error) {
      setNotificationError(
        error instanceof Error ? error.message : 'No se pudieron cargar las notificaciones.',
      )
    }
  }, [])

  useEffect(() => {
    queueMicrotask(() => void loadQueue())
    return subscribeToSyncQueue(() => void loadQueue())
  }, [loadQueue])

  useEffect(() => {
    queueMicrotask(() => void loadNotifications())
  }, [loadNotifications])

  async function readNotification(id: string) {
    try {
      await markNotificationRead(id)
      await loadNotifications()
    } catch (error) {
      await alerts.error(error, 'No se pudo marcar la notificación como leída')
    }
  }

  async function synchronize() {
    setSyncing(true)
    try {
      await flushSyncQueue()
      await loadQueue()
      if ((await offlineDb.syncQueue.count()) === 0) {
        await alerts.success('Sincronización completada', 'No quedan cambios pendientes.')
      }
    } catch (error) {
      await alerts.error(error, 'No se pudo sincronizar')
    } finally {
      setSyncing(false)
    }
  }

  async function discard(item: SyncQueueItem) {
    const confirmed = await alerts.confirm({
      title: 'Descartar cambio local',
      text: 'Este cambio no se enviará al servidor. Esta acción no se puede deshacer.',
      confirmText: 'Descartar',
    })
    if (!confirmed) return
    await discardSyncMutation(item.idempotencyKey)
    await loadQueue()
  }

  async function retry(item: SyncQueueItem) {
    setSyncing(true)
    try {
      await retrySyncMutation(item.idempotencyKey)
      await loadQueue()
    } finally {
      setSyncing(false)
    }
  }

  return (
    <section aria-labelledby="notifications-title">
      <p className="text-xs font-bold tracking-[0.14em] text-brand-700 uppercase">Cuenta</p>
      <div className="mt-2 flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 id="notifications-title" className="text-2xl font-extrabold text-ink-strong">
            Notificaciones y sincronización
          </h1>
          <p className="mt-2 text-sm text-ink-muted">
            Revise los recordatorios de inspección y los cambios guardados sin conexión.
          </p>
        </div>
        <button
          type="button"
          onClick={() => void synchronize()}
          disabled={syncing || !navigator.onLine}
          className="hover:bg-brand-800 rounded-xl bg-brand-700 px-4 py-2.5 text-sm font-bold text-white transition disabled:cursor-not-allowed disabled:opacity-50"
        >
          {syncing ? 'Sincronizando…' : 'Sincronizar ahora'}
        </button>
      </div>

      <article className="mt-6 overflow-hidden rounded-card bg-white shadow-card">
        <div className="border-b border-slate-100 px-5 py-4">
          <h2 className="font-extrabold text-ink-strong">Avisos del sistema</h2>
          <p className="mt-1 text-sm text-ink-muted">
            Los recordatorios aparecen 30, 14 y 7 días antes, y el día de la próxima inspección.
          </p>
        </div>
        {notificationError && <p className="p-5 text-sm text-red-700">{notificationError}</p>}
        {!notificationError && notifications.length === 0 && (
          <p className="p-5 text-sm text-ink-muted">No hay avisos disponibles en este momento.</p>
        )}
        {notifications.length > 0 && (
          <ul className="divide-y divide-slate-100">
            {notifications.map((notification) => (
              <li
                key={notification.id}
                className={`flex flex-wrap items-start gap-3 px-5 py-4 text-sm ${notification.readAt ? 'bg-white' : 'bg-brand-50'}`}
              >
                <div className="min-w-0 flex-1">
                  <p className="font-extrabold text-ink-strong">{notification.title}</p>
                  <p className="mt-1 text-ink-body">{notification.message}</p>
                  <p className="mt-2 text-xs text-ink-muted">
                    {new Intl.DateTimeFormat('es-DO', {
                      dateStyle: 'medium',
                      timeStyle: 'short',
                    }).format(new Date(notification.scheduledFor))}
                  </p>
                </div>
                {!notification.readAt && (
                  <button
                    type="button"
                    onClick={() => void readNotification(notification.id)}
                    className="text-brand-800 rounded-lg border border-brand-700 px-3 py-2 font-bold"
                  >
                    Marcar como leída
                  </button>
                )}
              </li>
            ))}
          </ul>
        )}
      </article>

      <article className="mt-6 overflow-hidden rounded-card bg-white shadow-card">
        <div className="border-b border-slate-100 px-5 py-4">
          <h2 className="font-extrabold text-ink-strong">Cambios pendientes</h2>
          <p className="mt-1 text-sm text-ink-muted">
            {items.length === 0
              ? 'Todos los cambios están sincronizados.'
              : `${items.length} cambio${items.length === 1 ? '' : 's'} en la cola.`}
          </p>
        </div>
        {items.length > 0 && (
          <ul className="divide-y divide-slate-100">
            {items.map((item) => (
              <li key={item.idempotencyKey} className="flex flex-wrap gap-3 px-5 py-4 text-sm">
                <span className="font-bold text-ink-strong">
                  {formatStatusLabel(item.kind === 'answer' ? 'respuesta' : item.kind)}
                </span>
                <span className="rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-bold text-ink-muted">
                  {formatStatusLabel(item.status)}
                </span>
                <span className="ml-auto text-ink-muted">
                  {item.attempts} intento{item.attempts === 1 ? '' : 's'}
                </span>
                {item.lastError && <p className="w-full text-red-700">{item.lastError}</p>}
                {item.status === 'failed' && (
                  <div className="flex w-full justify-end gap-2">
                    <button
                      type="button"
                      disabled={syncing || !navigator.onLine}
                      onClick={() => void retry(item)}
                      className="text-brand-800 rounded-lg border border-brand-700 px-3 py-2 font-bold disabled:opacity-50"
                    >
                      Reintentar
                    </button>
                    <button
                      type="button"
                      onClick={() => void discard(item)}
                      className="rounded-lg border border-red-300 px-3 py-2 font-bold text-red-700"
                    >
                      Descartar
                    </button>
                  </div>
                )}
              </li>
            ))}
          </ul>
        )}
      </article>
    </section>
  )
}
