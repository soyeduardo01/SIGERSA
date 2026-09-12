import { useCallback, useEffect, useState } from 'react'
import { useAuth } from '../../contexts/useAuth'
import { alerts } from '../../lib/alerts'
import { formatStatusLabel } from '../../lib/formatters'
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
  const [syncing, setSyncing] = useState(false)

  const loadQueue = useCallback(async () => {
    setItems(await offlineDb.syncQueue.orderBy('createdAt').reverse().toArray())
  }, [])

  useEffect(() => {
    queueMicrotask(() => void loadQueue())
    return subscribeToSyncQueue(() => void loadQueue())
  }, [loadQueue])

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
            Revise los cambios guardados sin conexión y envíelos cuando la red esté disponible.
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
                      className="rounded-lg border border-brand-700 px-3 py-2 font-bold text-brand-800 disabled:opacity-50"
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
