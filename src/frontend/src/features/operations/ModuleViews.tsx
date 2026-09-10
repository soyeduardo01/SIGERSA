import { useCallback, useEffect, useState } from 'react'
import { CardGridSkeleton } from '../../components/feedback/Skeletons'
import { useAuth } from '../../contexts/useAuth'
import { useSyncStatus } from '../../hooks/useSyncStatus'
import { alerts } from '../../lib/alerts'
import { formatStatusLabel } from '../../lib/formatters'
import { offlineDb, type SyncQueueItem } from '../../offline/database'
import { flushSyncQueue, subscribeToSyncQueue } from '../../offline/syncQueue'
import { AllItemsAdmin } from '../admin/AllItemsAdmin'
import { CasesManagement } from '../cases/CasesManagement'
import { CorrectionsManagement } from '../corrections/CorrectionsManagement'
import { EvaluationsManagement } from '../evaluations/EvaluationsManagement'
import { EvidenceManagement } from '../evidences/EvidenceManagement'
import { RequestsManagement } from '../requests/RequestsManagement'
import { SchedulingManagement } from '../scheduling/SchedulingManagement'
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
  const { isSyncing } = useSyncStatus()
  const cards = [
    ['Casos activos', 'Consulte los casos autorizados para su ámbito.'],
    ['Evaluaciones', 'Revise asignaciones y trabajo pendiente de sincronizar.'],
    ['Alertas', 'Priorice denuncias y eventos que requieren atención.'],
    ['Riesgo BPM', 'Acceda a indicadores calculados y trazables.'],
  ]
  return (
    <section aria-labelledby="overview-title">
      <p className="text-xs font-bold tracking-[0.14em] text-brand-700 uppercase">
        Panel operativo
      </p>
      <h1 id="overview-title" className="mt-2 text-2xl font-extrabold text-ink-strong">
        Resumen
      </h1>
      <p className="mt-2 text-sm text-ink-muted">
        Punto de entrada al ciclo EBR/BPM según los permisos y el ámbito de la sesión.
      </p>
      <div className="mt-6">
        {isSyncing ? (
          <CardGridSkeleton cards={4} />
        ) : (
          <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
            {cards.map(([title, text]) => (
              <article key={title} className="rounded-card bg-white p-5 shadow-card">
                <p className="text-sm font-extrabold text-ink-strong">{title}</p>
                <p className="mt-2 text-sm leading-6 text-ink-muted">{text}</p>
                <span className="text-brand-800 mt-5 inline-flex rounded-full bg-brand-50 px-2.5 py-1 text-xs font-bold">
                  Ámbito autorizado
                </span>
              </article>
            ))}
          </div>
        )}
      </div>
    </section>
  )
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
      statuses={['ACTIVA', 'INACTIVA', 'PENDIENTE']}
      editors={['ADMINISTRADOR', 'ADMINISTRADOR_EMPRESA', 'COORDINADOR']}
    />
  )
}
export function EstablishmentsPage() {
  return (
    <Workspace
      title="Establecimientos"
      eyebrow="Registro sanitario"
      description="Consulte y mantenga establecimientos de acuerdo con la empresa, territorio y asignación autorizada."
      actionLabel="Nuevo establecimiento"
      referenceLabel="Código"
      detailLabel="Nombre, dirección y actividad"
      statuses={['ACTIVO', 'INACTIVO', 'PENDIENTE']}
      editors={['ADMINISTRADOR', 'ADMINISTRADOR_EMPRESA', 'USUARIO_DELEGADO', 'COORDINADOR']}
    />
  )
}
export function ParametersPage() {
  return (
    <Workspace
      title="Parámetros y catálogos"
      eyebrow="Configuración"
      description="Consulte los valores activos de ParametersControl. Solo el Administrador puede crear, modificar o desactivar valores."
      actionLabel="Nuevo parámetro"
      referenceLabel="KeyWord / Código"
      detailLabel="Valor y ámbito"
      statuses={['ACTIVO', 'INACTIVO']}
      editors={['ADMINISTRADOR']}
    />
  )
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
      statuses={['RECIBIDA', 'EN_ANALISIS', 'VINCULADA', 'CERRADA']}
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
      statuses={['ABIERTO', 'EN_CORRECCION', 'VALIDADO', 'CERRADO']}
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
      statuses={['BORRADOR', 'GENERADO', 'PUBLICADO']}
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
      statuses={['EXITOSO', 'DENEGADO', 'ERROR']}
      editors={[]}
    />
  )
}

export function EvaluationsPage() {
  return <EvaluationsManagement />
}

export function FichasPage() {
  const canEdit = useRole('ADMINISTRADOR')
  return <AllItemsAdmin readOnly={!canEdit} />
}

export function ProfilePage() {
  const { identity, roleLabel, roles } = useAuth()
  return (
    <section aria-labelledby="profile-title">
      <p className="text-xs font-bold tracking-[0.14em] text-brand-700 uppercase">Cuenta</p>
      <h1 id="profile-title" className="mt-2 text-2xl font-extrabold text-ink-strong">
        Mi perfil
      </h1>
      <div className="mt-6 grid gap-5 lg:grid-cols-2">
        <article className="rounded-card bg-white p-6 shadow-card">
          <h2 className="font-extrabold text-ink-strong">Datos de la sesión</h2>
          <dl className="mt-5 grid gap-4 text-sm">
            <div>
              <dt className="font-bold text-ink-muted">Nombre</dt>
              <dd className="mt-1 text-ink-strong">{identity?.name ?? 'Usuario SIGERSA'}</dd>
            </div>
            <div>
              <dt className="font-bold text-ink-muted">Correo</dt>
              <dd className="mt-1 text-ink-strong">{identity?.email || 'No disponible'}</dd>
            </div>
            <div>
              <dt className="font-bold text-ink-muted">Rol principal</dt>
              <dd className="mt-1 text-ink-strong">{roleLabel}</dd>
            </div>
          </dl>
        </article>
        <article className="rounded-card bg-white p-6 shadow-card">
          <h2 className="font-extrabold text-ink-strong">Permisos activos</h2>
          <p className="mt-2 text-sm text-ink-muted">
            Los accesos dependen además del ámbito y de la propiedad de cada recurso.
          </p>
          <div className="mt-5 flex flex-wrap gap-2">
            {roles.map((role) => (
              <span
                key={role}
                className="text-brand-800 rounded-full bg-brand-50 px-3 py-1.5 text-xs font-bold"
              >
                {role}
              </span>
            ))}
          </div>
        </article>
      </div>
    </section>
  )
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
