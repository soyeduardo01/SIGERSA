import { useEffect, useState } from 'react'
import { useAuth } from '../../contexts/useAuth'
import { getDashboard, type DashboardSnapshot } from '../../lib/api'
import { formatStatusLabel } from '../../lib/formatters'
import { canAccessModule } from '../../lib/rbac'

const emptyDashboard: DashboardSnapshot = {
  activeCases: 0,
  pendingEvaluations: 0,
  criticalAlerts: 0,
  averageCompliance: null,
  myRequests: 0,
  unreadNotifications: 0,
  scheduledEvaluations: 0,
  openComplaints: 0,
  pendingAssignments: 0,
  pendingReports: 0,
  recentEvaluations: [],
  riskDistribution: [],
  upcomingSchedules: [],
}

function Metric({
  title,
  value,
  detail,
  href,
  trend,
  trendKind = 'positive',
}: {
  title: string
  value: string
  detail: string
  href?: string
  trend?: string
  trendKind?: 'positive' | 'negative'
}) {
  const tone = metricTone(title)
  const content = (
    <div className="flex h-full gap-3">
      <span className={`grid size-12 shrink-0 place-items-center rounded-2xl ${tone.icon}`}>
        <MetricGlyph title={title} />
      </span>
      <span className="min-w-0 flex-1">
        <span className="block text-xs leading-4 font-extrabold text-ink-strong sm:text-[13px]">
          {title}
        </span>
        <span className="mt-1 flex flex-wrap items-center gap-1.5">
          <strong className="text-[1.75rem] font-black tracking-tight text-slate-950 sm:text-3xl">
            {value}
          </strong>
          {trend && (
            <span
              className={`shrink-0 rounded-full px-2 py-0.5 text-[11px] font-extrabold ${trendKind === 'negative' ? 'bg-red-100 text-red-600' : 'bg-emerald-100 text-emerald-700'}`}
            >
              {trend}
            </span>
          )}
        </span>
        <span className="mt-1.5 block text-[11px] leading-4 text-ink-muted">{detail}</span>
      </span>
    </div>
  )
  return (
    <article className="overflow-hidden rounded-[1.35rem] border border-white/90 bg-white/95 p-4 shadow-[0_12px_32px_rgba(18,86,64,0.08)] transition hover:-translate-y-0.5 hover:shadow-[0_18px_38px_rgba(18,86,64,0.12)]">
      {href ? (
        <a
          href={href}
          className="block rounded-lg focus:ring-2 focus:ring-brand-500 focus:outline-none"
        >
          {content}
        </a>
      ) : (
        content
      )}
    </article>
  )
}

export function DashboardOverview() {
  const { roles } = useAuth()
  const [data, setData] = useState(emptyDashboard)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    let active = true
    void getDashboard()
      .then((snapshot) => {
        if (active) {
          setData(snapshot)
          setError('')
        }
      })
      .catch((caught) => {
        if (active)
          setError(caught instanceof Error ? caught.message : 'No se pudo cargar el resumen.')
      })
      .finally(() => active && setLoading(false))
    return () => {
      active = false
    }
  }, [])

  const isCompanyAdministrator = roles.includes('ADMINISTRADOR_EMPRESA')
  const isDelegate = roles.includes('USUARIO_DELEGADO')
  const isCoordinator = roles.includes('COORDINADOR')
  const isTechnician = roles.includes('TECNICO_EVALUADOR')
  const canSeeEvaluations = canAccessModule(roles, 'evaluaciones')
  const canSeeSchedules = canAccessModule(roles, 'programacion')
  const viewData = data
  const viewRiskTotal = viewData.riskDistribution.reduce((sum, item) => sum + item.total, 0)
  const now = new Date()
  const displayDate = new Intl.DateTimeFormat('es-DO', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  }).format(now)
  const displayTime = new Intl.DateTimeFormat('es-DO', {
    hour: 'numeric',
    minute: '2-digit',
  }).format(now)

  return (
    <section
      aria-labelledby="overview-title"
      className="-m-4 min-h-full bg-[#f4f9f6] p-4 sm:-m-6 sm:p-6"
    >
      <header className="relative overflow-hidden rounded-[1.6rem] border border-white bg-[linear-gradient(90deg,rgba(247,252,249,0.98)_0%,rgba(247,252,249,0.88)_48%,rgba(247,252,249,0.12)_100%),url('/assets/dashboard-food-banner.jpg')] bg-cover bg-center px-5 py-5 shadow-[0_12px_34px_rgba(18,86,64,0.07)] sm:px-7">
        <div
          className="absolute inset-y-0 right-0 w-1/2 bg-[linear-gradient(90deg,transparent,rgba(8,116,82,0.12))]"
          aria-hidden="true"
        />
        <div className="relative grid items-center gap-5 lg:grid-cols-[1fr_auto]">
          <div>
            <h1
              id="overview-title"
              className="text-3xl font-black tracking-tight text-slate-950 sm:text-4xl"
            >
              Resumen operativo
            </h1>
            <p className="mt-1 text-sm text-ink-muted">
              Indicadores actualizados de los casos, evaluaciones y alertas registradas en SIGERSA.
            </p>
          </div>
          <div className="rounded-2xl border border-white/90 bg-white/90 px-4 py-3 text-xs shadow-lg backdrop-blur-sm">
            <strong className="block text-ink-strong capitalize">▣ {displayDate}</strong>
            <span className="mt-1 block text-ink-muted">Última actualización: {displayTime}</span>
          </div>
        </div>
      </header>

      {error && (
        <div
          role="alert"
          className="mt-5 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800"
        >
          {error}
        </div>
      )}

      <div
        className="mt-4 grid gap-4 sm:grid-cols-2 xl:grid-cols-4 2xl:grid-cols-5"
        aria-busy={loading}
      >
        {isCompanyAdministrator ? (
          <>
            <Metric
              title="Usuarios de empresa"
              value="→"
              detail="Administrar los usuarios vinculados a su empresa."
              href="/modulo.html?module=usuarios"
            />
            <Metric
              title="Notificaciones"
              value={String(viewData.unreadNotifications)}
              detail="Notificaciones internas sin leer."
              href="/modulo.html?module=notificaciones"
            />
          </>
        ) : isDelegate ? (
          <>
            <Metric
              title="Nueva Solicitud BPM"
              value="+"
              detail="Registrar una solicitud de inspección."
              href="/modulo.html?module=solicitudes&new=1"
            />
            <Metric
              title="Mis Solicitudes"
              value={String(viewData.myRequests)}
              detail="Solicitudes registradas por su empresa."
              href="/modulo.html?module=solicitudes"
            />
            <Metric
              title="Notificaciones"
              value={String(viewData.unreadNotifications)}
              detail="Notificaciones internas sin leer."
              href="/modulo.html?module=notificaciones"
            />
          </>
        ) : isCoordinator ? (
          <>
            <Metric
              title="Casos pendientes"
              value={String(viewData.activeCases)}
              detail="Casos que aún no han sido cerrados."
              href="/modulo.html?module=casos"
            />
            <Metric
              title="Evaluaciones programadas"
              value={String(viewData.scheduledEvaluations)}
              detail="Programaciones vigentes."
              href="/modulo.html?module=programacion"
            />
            <Metric
              title="Alertas LAPCH"
              value={String(viewData.criticalAlerts)}
              detail="Alertas registradas."
              href="/modulo.html?module=alertas-denuncias"
            />
            <Metric
              title="Denuncias"
              value={String(viewData.openComplaints)}
              detail="Denuncias registradas."
              href="/modulo.html?module=alertas-denuncias"
            />
            <Metric
              title="Asignaciones pendientes"
              value={String(viewData.pendingAssignments)}
              detail="Programaciones que requieren evaluador."
              href="/modulo.html?module=programacion"
            />
          </>
        ) : isTechnician ? (
          <>
            <Metric
              title="Evaluaciones asignadas"
              value={String(viewData.pendingEvaluations)}
              detail="Evaluaciones activas bajo su responsabilidad."
              href="/modulo.html?module=evaluaciones"
            />
            <Metric
              title="Calendario"
              value={String(viewData.scheduledEvaluations)}
              detail="Programaciones vigentes en su agenda."
              href="/modulo.html?module=programacion"
            />
            <Metric
              title="Pendientes de informe"
              value={String(viewData.pendingReports)}
              detail="Evaluaciones sin informe emitido."
              href="/modulo.html?module=reportes"
            />
          </>
        ) : (
          <>
            <Metric
              title="Casos activos"
              value={String(viewData.activeCases)}
              detail="Casos que aún no han sido cerrados."
            />
            <Metric
              title="Evaluaciones pendientes"
              value={String(viewData.pendingEvaluations)}
              detail="Evaluaciones asignadas, en ejecución o revisión."
              href="/modulo.html?module=evaluaciones"
            />
            <Metric
              title="Alertas registradas"
              value={String(viewData.criticalAlerts)}
              detail="Alertas LAPCH registradas."
              href="/modulo.html?module=alertas-denuncias"
            />
            <Metric
              title="Denuncias registradas"
              value={String(viewData.openComplaints)}
              detail="Denuncias ciudadanas registradas."
              href="/modulo.html?module=alertas-denuncias"
            />
            <Metric
              title="Cumplimiento BPM"
              value={
                viewData.averageCompliance === null
                  ? '—'
                  : `${viewData.averageCompliance.toFixed(1)}%`
              }
              detail="Promedio general de evaluaciones BPM."
            />
          </>
        )}
      </div>

      {(canSeeSchedules || canSeeEvaluations) && (
        <div className="mt-4 grid items-start gap-4 xl:grid-cols-12">
          {canSeeSchedules && <UpcomingSchedules data={viewData.upcomingSchedules} />}
          {canSeeEvaluations && (
            <RiskDonut
              data={viewData.riskDistribution}
              total={viewRiskTotal}
              compliance={viewData.averageCompliance ?? 0}
            />
          )}
        </div>
      )}

      {canSeeEvaluations && (
        <div className="mt-4 grid gap-4 xl:grid-cols-12">
          <article className="overflow-hidden rounded-[1.35rem] border border-white/90 bg-white shadow-[0_12px_32px_rgba(18,86,64,0.08)] xl:col-span-7">
            <div className="flex items-center justify-between px-5 py-4">
              <h2 className="text-sm font-extrabold text-ink-strong">Últimas evaluaciones</h2>
              <a
                href="/modulo.html?module=evaluaciones"
                className="text-xs font-bold text-brand-700"
              >
                Ver todas →
              </a>
            </div>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[680px] text-left text-xs">
                <thead className="border-y border-slate-100 bg-slate-50 text-ink-muted">
                  <tr>
                    <th className="px-5 py-3">Evaluación</th>
                    <th className="px-5 py-3">Establecimiento</th>
                    <th className="px-5 py-3">Estado</th>
                    <th className="px-5 py-3">Cumplimiento</th>
                    <th className="px-5 py-3">Riesgo</th>
                  </tr>
                </thead>
                <tbody>
                  {viewData.recentEvaluations.map((item) => (
                    <tr key={item.id} className="border-b border-slate-100">
                      <td className="px-5 py-3 font-bold">{item.number}</td>
                      <td className="px-5 py-3">{item.establishmentName}</td>
                      <td className="px-5 py-3">
                        <StatusBadge value={item.status} />
                      </td>
                      <td className="px-5 py-3">
                        {item.compliancePercentage === null
                          ? '—'
                          : `${item.compliancePercentage.toFixed(1)}%`}
                      </td>
                      <td className="px-5 py-3">
                        {item.riskLevel ? <RiskBadge value={item.riskLevel} /> : 'Sin calcular'}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {!loading && viewData.recentEvaluations.length === 0 && (
                <p className="p-8 text-center text-sm text-ink-muted">
                  No hay evaluaciones registradas.
                </p>
              )}
            </div>
          </article>

          <article className="rounded-[1.35rem] border border-white/90 bg-white p-5 shadow-[0_12px_32px_rgba(18,86,64,0.08)] xl:col-span-5">
            <div className="flex items-center justify-between gap-3">
              <h2 className="text-sm font-extrabold text-ink-strong">
                Detalle de niveles de riesgo
              </h2>
              <span className="text-xs font-bold text-brand-700">{viewRiskTotal} evaluaciones</span>
            </div>
            <div className="mt-4 space-y-3">
              {viewData.riskDistribution.map((item) => {
                const percentage =
                  viewRiskTotal === 0 ? 0 : Math.round((item.total / viewRiskTotal) * 100)
                return (
                  <div key={item.level}>
                    <div className="flex justify-between text-xs">
                      <span className="font-bold">{formatStatusLabel(item.level)}</span>
                      <span>
                        {item.total} · {percentage}%
                      </span>
                    </div>
                    <div className="mt-1.5 h-3 overflow-hidden rounded-full bg-slate-100">
                      <div
                        className={`h-full rounded-full ${riskBarColor(item.level)}`}
                        style={{ width: `${percentage}%` }}
                      />
                    </div>
                  </div>
                )
              })}
              {!loading && viewRiskTotal === 0 && (
                <p className="text-sm text-ink-muted">Aún no hay riesgos calculados.</p>
              )}
            </div>
            <p className="mt-6 rounded-xl bg-emerald-50 px-4 py-3 text-xs leading-relaxed text-emerald-900">
              🌱 Nuestro compromiso es fortalecer una cadena alimentaria más segura en la República
              Dominicana.
            </p>
          </article>
        </div>
      )}
    </section>
  )
}

function UpcomingSchedules({ data }: { data: DashboardSnapshot['upcomingSchedules'] }) {
  return (
    <article className="rounded-[1.35rem] border border-white/90 bg-white p-5 shadow-[0_12px_32px_rgba(18,86,64,0.08)] xl:col-span-7">
      <div className="flex items-center justify-between">
        <div>
          <p className="text-xs font-bold tracking-wide text-brand-700 uppercase">Agenda</p>
          <h2 className="mt-1 text-sm font-extrabold text-ink-strong">Próximas programaciones</h2>
        </div>
        <a
          href="/modulo.html?module=programacion"
          className="rounded-lg border border-slate-200 px-3 py-2 text-xs font-bold text-brand-700"
        >
          Ver agenda →
        </a>
      </div>
      <div className="mt-5 divide-y divide-slate-100" aria-label="Próximas programaciones">
        {data.map((item) => (
          <div key={item.id} className="grid gap-1 py-3 text-xs sm:grid-cols-[1fr_auto]">
            <span>
              <strong className="block text-ink-strong">{item.caseNumber}</strong>
              <span className="text-ink-muted">{item.establishmentName}</span>
            </span>
            <time className="font-bold text-brand-700" dateTime={item.startsAt}>
              {new Intl.DateTimeFormat('es-DO', { dateStyle: 'medium', timeStyle: 'short' }).format(
                new Date(item.startsAt),
              )}
            </time>
          </div>
        ))}
        {!data.length && (
          <p className="py-8 text-center text-sm text-ink-muted">No hay programaciones próximas.</p>
        )}
      </div>
    </article>
  )
}

function RiskDonut({
  data,
  total,
  compliance,
}: {
  data: DashboardSnapshot['riskDistribution']
  total: number
  compliance: number
}) {
  const percentages = data.map((item) => (total === 0 ? 0 : Math.round((item.total / total) * 100)))
  const first = percentages[0] ?? 0
  const second = first + (percentages[1] ?? 0)
  const third = second + (percentages[2] ?? 0)
  const background = `conic-gradient(#087452 0 ${first}%, #65bd75 ${first}% ${second}%, #f5b522 ${second}% ${third}%, #e9484f ${third}% 100%)`
  const colors = ['bg-[#087452]', 'bg-[#65bd75]', 'bg-[#f5b522]', 'bg-[#e9484f]']
  return (
    <article className="rounded-[1.35rem] border border-white/90 bg-white p-5 shadow-[0_12px_32px_rgba(18,86,64,0.08)] xl:col-span-5">
      <div className="flex items-center justify-between gap-3">
        <h2 className="flex items-center gap-2 text-sm font-extrabold text-ink-strong">
          <span
            className="grid size-8 place-items-center rounded-lg bg-emerald-50 text-emerald-700"
            aria-hidden="true"
          >
            <svg
              className="size-5"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
            >
              <path d="M5 20V9M10 20V4M15 20v-7M20 20V7M3 20h19" />
            </svg>
          </span>
          Distribución general de riesgo
        </h2>
        <span className="shrink-0 rounded-lg border border-slate-200 px-2.5 py-1.5 text-[0.65rem] font-bold text-ink-muted">
          Total: {total} casos
        </span>
      </div>
      <div className="mt-4 grid items-center gap-5 sm:grid-cols-[9.5rem_1fr] xl:grid-cols-[8.5rem_1fr] 2xl:grid-cols-[10rem_1fr]">
        <div
          className="relative mx-auto grid size-36 place-items-center rounded-full 2xl:size-40"
          style={{ background }}
          role="img"
          aria-label={`Gráfico circular de riesgo, ${compliance.toFixed(0)} por ciento de cumplimiento`}
        >
          <div className="grid size-[5.4rem] place-items-center rounded-full bg-white text-center shadow-inner 2xl:size-24">
            <span>
              <strong className="block text-2xl text-slate-950">{compliance.toFixed(0)}%</strong>
              <small className="text-[0.65rem] text-ink-muted">Cumplimiento</small>
            </span>
          </div>
        </div>
        <ul>
          {data.map((item, index) => (
            <li
              key={item.level}
              className="grid grid-cols-[1fr_auto_auto] items-center gap-3 border-b border-slate-100 py-2 text-xs last:border-0"
            >
              <span className="flex items-center gap-2">
                <i className={`size-3 rounded-full ${colors[index] ?? 'bg-slate-400'}`} />
                {formatStatusLabel(item.level)}
              </span>
              <span className="text-ink-muted tabular-nums">{item.total}</span>
              <strong className="min-w-9 text-right tabular-nums">
                {percentages[index] ?? 0}%
              </strong>
            </li>
          ))}
        </ul>
      </div>
    </article>
  )
}

function metricTone(title: string) {
  if (/alert|denuncia|crític/i.test(title)) return { icon: 'bg-red-100 text-red-600' }
  if (/evalu|calendario|informe/i.test(title)) return { icon: 'bg-sky-100 text-sky-600' }
  if (/cumplimiento|notific/i.test(title)) return { icon: 'bg-violet-100 text-violet-700' }
  return { icon: 'bg-emerald-100 text-emerald-700' }
}

function MetricGlyph({ title }: { title: string }) {
  const isAlert = /alert|denuncia|crític/i.test(title)
  const isEvaluation = /evalu|calendario|informe/i.test(title)
  return (
    <svg
      className="size-7"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.9"
      aria-hidden="true"
    >
      {isAlert ? (
        <>
          <path d="M12 3 2.8 19h18.4L12 3Z" />
          <path d="M12 9v4M12 16.5h.01" />
        </>
      ) : isEvaluation ? (
        <>
          <rect x="5" y="4" width="14" height="17" rx="2" />
          <path d="M9 4.5V3h6v1.5M8.5 11l2 2 5-5M9 17h6" />
        </>
      ) : (
        <>
          <path d="M4 21V9l5-4v16M9 21V3h7v18M16 21v-9l4 2v7" />
          <path d="M12 7h1M12 11h1M12 15h1" />
        </>
      )}
    </svg>
  )
}

function StatusBadge({ value }: { value: string }) {
  const className = /COMPLET|APROBAD/i.test(value)
    ? 'bg-emerald-100 text-emerald-800'
    : /EJECUC/i.test(value)
      ? 'bg-sky-100 text-sky-700'
      : /REVIS/i.test(value)
        ? 'bg-violet-100 text-violet-700'
        : 'bg-slate-100 text-slate-700'
  return (
    <span
      className={`inline-flex rounded-full px-2.5 py-1 text-[0.65rem] font-extrabold ${className}`}
    >
      {formatStatusLabel(value)}
    </span>
  )
}

function RiskBadge({ value }: { value: string }) {
  const className =
    value === 'BAJO'
      ? 'bg-emerald-100 text-emerald-800'
      : value === 'MEDIO'
        ? 'bg-amber-100 text-amber-700'
        : 'bg-red-100 text-red-700'
  return (
    <span
      className={`inline-flex rounded-full px-2.5 py-1 text-[0.65rem] font-extrabold ${className}`}
    >
      {formatStatusLabel(value)}
    </span>
  )
}

function riskBarColor(value: string) {
  if (value === 'BAJO') return 'bg-emerald-700'
  if (value === 'MEDIO') return 'bg-amber-400'
  if (value === 'ALTO') return 'bg-red-500'
  return 'bg-red-700'
}
