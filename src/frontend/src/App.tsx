import { AppLayout } from './components/layout/AppLayout'
import { RiskBadge, type RiskLevel } from './components/ui/RiskBadge'
import { useSyncStatus } from './hooks/useSyncStatus'
import { isSupabaseConfigured } from './lib/supabase'

const inspections: Array<{
  id: string
  establishment: string
  location: string
  date: string
  progress: number
  risk: RiskLevel
}> = [
  {
    id: 'EV-2026-0184',
    establishment: 'Procesadora del Caribe',
    location: 'Santo Domingo Este',
    date: '11 sep, 9:30 a. m.',
    progress: 72,
    risk: 'Alto',
  },
  {
    id: 'EV-2026-0181',
    establishment: 'Alimentos Quisqueya',
    location: 'Distrito Nacional',
    date: '12 sep, 11:00 a. m.',
    progress: 46,
    risk: 'Medio',
  },
  {
    id: 'EV-2026-0178',
    establishment: 'Lácteos del Cibao',
    location: 'Santiago',
    date: '13 sep, 8:00 a. m.',
    progress: 18,
    risk: 'Bajo',
  },
]

const summary = [
  { label: 'Evaluaciones activas', value: '18', detail: '5 programadas hoy', tone: 'brand' },
  { label: 'Riesgo alto', value: '4', detail: 'Requieren prioridad', tone: 'high' },
  { label: 'Pendientes de revisión', value: '7', detail: '2 vencen hoy', tone: 'medium' },
  { label: 'Cumplimiento promedio', value: '86%', detail: '+3.2% este mes', tone: 'low' },
] as const

const toneStyles = {
  brand: 'bg-brand-100 text-brand-700',
  high: 'bg-risk-high/10 text-risk-high',
  medium: 'bg-risk-medium/10 text-risk-medium',
  low: 'bg-risk-low/10 text-risk-low',
}

function App() {
  const syncStatus = useSyncStatus()

  return (
    <AppLayout {...syncStatus}>
      <section aria-labelledby="dashboard-title">
        <div className="flex flex-col gap-5 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <p className="text-sm font-semibold text-brand-700">Jueves, 10 de septiembre</p>
            <h1
              id="dashboard-title"
              className="mt-1 text-2xl font-extrabold tracking-tight text-ink-strong sm:text-3xl"
            >
              Buenas noches, Elena
            </h1>
            <p className="mt-2 max-w-2xl text-sm leading-6 text-ink-muted sm:text-base">
              Aquí tienes el estado de las evaluaciones basadas en riesgo bajo tu supervisión.
            </p>
          </div>
          <button
            type="button"
            className="inline-flex min-h-11 items-center justify-center gap-2 rounded-xl bg-brand-700 px-4 py-2.5 text-sm font-bold text-white shadow-sm hover:bg-brand-900"
          >
            <svg
              viewBox="0 0 24 24"
              className="size-5"
              fill="none"
              stroke="currentColor"
              aria-hidden="true"
            >
              <path strokeLinecap="round" strokeWidth="2" d="M12 5v14M5 12h14" />
            </svg>
            Nueva evaluación
          </button>
        </div>

        <div
          className={`mt-6 flex items-start gap-3 rounded-xl border px-4 py-3 text-sm ${
            syncStatus.isOnline
              ? 'border-emerald-200 bg-emerald-50 text-emerald-900'
              : 'border-amber-200 bg-amber-50 text-amber-950'
          }`}
          role="status"
        >
          <span
            className={`mt-1 size-2.5 shrink-0 rounded-full ${syncStatus.isOnline ? 'bg-risk-low' : 'bg-risk-medium'}`}
            aria-hidden="true"
          />
          <div>
            <p className="font-bold">
              {syncStatus.isOnline ? 'Conexión disponible' : 'Trabajando sin conexión'}
            </p>
            <p className="mt-0.5 text-xs leading-5 opacity-80">
              {syncStatus.pendingCount > 0
                ? `${syncStatus.pendingCount} cambios están guardados de forma segura y pendientes de sincronización.`
                : 'La aplicación está lista para conservar respuestas y evidencias en este dispositivo.'}
            </p>
          </div>
        </div>

        <div className="mt-6 grid grid-cols-2 gap-3 lg:grid-cols-4 lg:gap-5">
          {summary.map((item) => (
            <article
              key={item.label}
              className="rounded-card bg-surface-card p-4 shadow-card sm:p-5"
            >
              <div
                className={`mb-4 grid size-9 place-items-center rounded-lg ${toneStyles[item.tone]}`}
                aria-hidden="true"
              >
                <svg viewBox="0 0 24 24" className="size-4.5" fill="none" stroke="currentColor">
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth="2"
                    d="M5 12h3l2-5 4 10 2-5h3"
                  />
                </svg>
              </div>
              <p className="text-2xl font-extrabold tracking-tight text-ink-strong sm:text-3xl">
                {item.value}
              </p>
              <h2 className="mt-1 text-xs font-semibold text-ink-body sm:text-sm">{item.label}</h2>
              <p className="mt-2 text-[0.68rem] font-medium text-ink-muted sm:text-xs">
                {item.detail}
              </p>
            </article>
          ))}
        </div>
      </section>

      <div className="mt-6 grid gap-6 xl:grid-cols-[minmax(0,1fr)_20rem]">
        <section
          className="overflow-hidden rounded-card bg-surface-card shadow-card"
          aria-labelledby="inspections-title"
        >
          <div className="flex items-center justify-between border-b border-slate-100 px-4 py-4 sm:px-6">
            <div>
              <h2 id="inspections-title" className="text-base font-bold text-ink-strong">
                Próximas inspecciones
              </h2>
              <p className="mt-1 text-xs text-ink-muted">
                Actividades asignadas para los próximos días
              </p>
            </div>
            <a
              href="#evaluaciones"
              className="text-sm font-bold text-brand-700 hover:text-brand-900"
            >
              Ver todas
            </a>
          </div>

          <div className="divide-y divide-slate-100">
            {inspections.map((inspection) => (
              <article key={inspection.id} className="p-4 sm:p-5">
                <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                  <div className="min-w-0">
                    <div className="flex flex-wrap items-center gap-2">
                      <p className="text-xs font-bold tracking-wide text-brand-700">
                        {inspection.id}
                      </p>
                      <RiskBadge level={inspection.risk} />
                    </div>
                    <h3 className="mt-2 truncate text-sm font-bold text-ink-strong sm:text-base">
                      {inspection.establishment}
                    </h3>
                    <p className="mt-1 flex flex-wrap gap-x-3 gap-y-1 text-xs text-ink-muted">
                      <span>{inspection.location}</span>
                      <span aria-hidden="true">·</span>
                      <span>{inspection.date}</span>
                    </p>
                  </div>
                  <div className="w-full sm:w-36">
                    <div className="mb-1.5 flex items-center justify-between text-xs">
                      <span className="font-medium text-ink-muted">Progreso</span>
                      <span className="font-bold text-ink-strong">{inspection.progress}%</span>
                    </div>
                    <div
                      className="h-2 overflow-hidden rounded-full bg-surface-muted"
                      role="progressbar"
                      aria-label={`Progreso de ${inspection.establishment}`}
                      aria-valuemin={0}
                      aria-valuemax={100}
                      aria-valuenow={inspection.progress}
                    >
                      <div
                        className="h-full rounded-full bg-brand-500"
                        style={{ width: `${inspection.progress}%` }}
                      />
                    </div>
                  </div>
                </div>
              </article>
            ))}
          </div>
        </section>

        <aside className="space-y-6" aria-label="Indicadores complementarios">
          <section
            className="rounded-card bg-surface-card p-5 shadow-card"
            aria-labelledby="bpm-title"
          >
            <h2 id="bpm-title" className="text-base font-bold text-ink-strong">
              Resultado BPM
            </h2>
            <p className="mt-1 text-xs leading-5 text-ink-muted">
              Tokens semánticos de cumplimiento
            </p>
            <dl className="mt-5 space-y-3">
              <div className="flex items-center justify-between rounded-xl bg-emerald-50 p-3">
                <dt className="text-sm font-semibold text-ink-body">
                  <span className="mr-2 font-extrabold text-bpm-c">C</span>Cumple
                </dt>
                <dd className="text-sm font-extrabold text-bpm-c">84%</dd>
              </div>
              <div className="flex items-center justify-between rounded-xl bg-amber-50 p-3">
                <dt className="text-sm font-semibold text-ink-body">
                  <span className="mr-2 font-extrabold text-bpm-cp">CP</span>Cumple parcial
                </dt>
                <dd className="text-sm font-extrabold text-bpm-cp">11%</dd>
              </div>
              <div className="flex items-center justify-between rounded-xl bg-red-50 p-3">
                <dt className="text-sm font-semibold text-ink-body">
                  <span className="mr-2 font-extrabold text-bpm-it">IT</span>Incumplimiento
                </dt>
                <dd className="text-sm font-extrabold text-bpm-it">5%</dd>
              </div>
            </dl>
          </section>

          <section
            className="rounded-card bg-surface-inverse p-5 text-white shadow-card"
            aria-labelledby="offline-title"
          >
            <div className="flex items-center justify-between">
              <h2 id="offline-title" className="text-sm font-bold">
                Preparación offline
              </h2>
              <span className="rounded-full bg-white/10 px-2 py-1 text-[0.65rem] font-bold text-emerald-100">
                PWA
              </span>
            </div>
            <p className="mt-3 text-xs leading-5 text-slate-300">
              Evaluaciones, fichas y cola de sincronización disponibles en IndexedDB.
            </p>
            <div className="mt-4 flex items-center gap-2 text-xs font-semibold text-emerald-100">
              <span
                className={`size-2 rounded-full ${isSupabaseConfigured ? 'bg-emerald-400' : 'bg-amber-400'}`}
                aria-hidden="true"
              />
              {isSupabaseConfigured ? 'Supabase configurado' : 'Supabase pendiente de configurar'}
            </div>
          </section>
        </aside>
      </div>
    </AppLayout>
  )
}

export default App
