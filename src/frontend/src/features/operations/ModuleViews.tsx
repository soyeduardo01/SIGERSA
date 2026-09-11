import { useCallback, useEffect, useState } from 'react'
import { useAuth } from '../../contexts/useAuth'
import { alerts } from '../../lib/alerts'
import { formatStatusLabel } from '../../lib/formatters'
import { offlineDb, type SyncQueueItem } from '../../offline/database'
import { flushSyncQueue, subscribeToSyncQueue } from '../../offline/syncQueue'
import { AllItemsAdmin } from '../admin/AllItemsAdmin'
import { ParametersManagement } from '../admin/ParametersManagement'
import { CasesManagement } from '../cases/CasesManagement'
import { CorrectionsManagement } from '../corrections/CorrectionsManagement'
import { EvaluationsManagement } from '../evaluations/EvaluationsManagement'
import { EvidenceManagement } from '../evidences/EvidenceManagement'
import { EstablishmentsManagement } from '../establishments/EstablishmentsManagement'
import { RequestsManagement } from '../requests/RequestsManagement'
import { SchedulingManagement } from '../scheduling/SchedulingManagement'
import { ProfileManagement } from '../profile/ProfileManagement'
import { DashboardOverview } from './DashboardOverview'
import { OperationalModulePage, type OperationalModuleConfig } from './OperationalModulePage'

function useRole(...allowed: string[]) {
  const { roles } = useAuth()
  return roles.some((role) => allowed.includes(role))
}

function Workspace(props: Omit<OperationalModuleConfig, 'canCreate'> & { editors: string[] }) {
  const canCreate = useRole(...props.editors)
  const { editors: _editors, ...config } = props
  void _editors
  return <OperationalModulePage config={{ ...config, canCreate }} />
}

export function OverviewPage() {
  return <DashboardOverview />
}

export function CompaniesPage() {
  return (
    <Workspace
      title="Empresas"
      eyebrow="Administración empresarial"
      description="Administre el perfil de las organizaciones sujetas al proceso EBR/BPM y sus ámbitos autorizados."
      actionLabel="Nueva empresa"
      referenceLabel="RNC o código"
      detailLabel="Razón social y datos de contacto"
      statusKeyWord="ESTADO_EMPRESA"
      editors={['ADMINISTRADOR', 'ADMINISTRADOR_EMPRESA', 'COORDINADOR']}
    />
  )
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
  return (
    <Workspace
      title="Alertas y denuncias"
      eyebrow="Vigilancia sanitaria"
      description="Centralice alertas relacionadas, denuncias y su evaluación inicial para priorización."
      actionLabel="Nueva alerta"
      referenceLabel="Código de alerta"
      detailLabel="Origen, categoría y descripción"
      statusKeyWord="ESTADO_ALERTA"
      editors={['ADMINISTRADOR', 'COORDINADOR']}
    />
  )
}
export function CasesPage() {
  return <CasesManagement />
}
export function SchedulingPage() {
  return <SchedulingManagement />
}
export function FindingsPage() {
  return (
    <Workspace
      title="Hallazgos y no conformidades"
      eyebrow="Ejecución de campo"
      description="Documente incumplimientos, severidad y relación con los ítems de la ficha versionada."
      actionLabel="Nuevo hallazgo"
      referenceLabel="Ítem / Hallazgo"
      detailLabel="Descripción y severidad"
      statusKeyWord="ESTADO_HALLAZGO"
      editors={['ADMINISTRADOR', 'TECNICO_EVALUADOR']}
    />
  )
}
export function EvidencePage() {
  return <EvidenceManagement />
}
export function CorrectionsPage() {
  return <CorrectionsManagement />
}
export function ReportsPage() {
  return (
    <Workspace
      title="Informes e indicadores"
      eyebrow="Análisis y decisión"
      description="Prepare consultas de cumplimiento, riesgo, frecuencia e histórico dentro del ámbito permitido."
      actionLabel="Nuevo informe"
      referenceLabel="Informe"
      detailLabel="Período, indicador y alcance"
      statusKeyWord="ESTADO_INFORME"
      editors={['ADMINISTRADOR', 'COORDINADOR']}
    />
  )
}
export function AuditPage() {
  return (
    <Workspace
      title="Auditoría"
      eyebrow="Trazabilidad"
      description="Consulte eventos inmutables de seguridad, cambios sensibles, versiones y decisiones autorizadas."
      actionLabel="Exportar consulta"
      referenceLabel="Correlación / Actor"
      detailLabel="Evento, recurso y resultado"
      statusKeyWord="RESULTADO_AUDITORIA"
      editors={[]}
    />
  )
}

export function EvaluationsPage() {
  return <EvaluationsManagement />
}

export function InspectionsPage() {
  return <EvaluationsManagement mode="inspection" />
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
                <span className="font-bold text-ink-strong">{item.kind}</span>
                <span className="rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-bold text-ink-muted">
                  {formatStatusLabel(item.status)}
                </span>
                <span className="ml-auto text-ink-muted">
                  {item.attempts} intento{item.attempts === 1 ? '' : 's'}
                </span>
                {item.lastError && <p className="w-full text-red-700">{item.lastError}</p>}
              </li>
            ))}
          </ul>
        )}
      </article>
    </section>
  )
}
