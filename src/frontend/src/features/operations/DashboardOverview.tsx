import { useState, type ReactNode } from 'react'
import { EstablishmentFormModal } from './EstablishmentFormModal'

interface KpiCardProps {
  icon: ReactNode
  iconClass: string
  title: string
  value: string
  trend: string
  trendClass?: string
  description: string
  badge: string
  badgeClass: string
}

function KpiCard({
  icon,
  iconClass,
  title,
  value,
  trend,
  trendClass = 'text-emerald-600',
  description,
  badge,
  badgeClass,
}: KpiCardProps) {
  return (
    <article className="rounded-2xl border border-slate-200/80 bg-white p-5 shadow-card">
      <div className="flex items-start justify-between">
        <span className={`grid size-12 place-items-center rounded-full text-xl ${iconClass}`}>{icon}</span>
        <span className={`text-xs font-bold ${trendClass}`}>↗ {trend}</span>
      </div>
      <p className="mt-3 text-sm font-bold text-ink-strong">{title}</p>
      <div className="mt-1 flex items-end gap-3">
        <strong className="text-3xl font-extrabold tracking-tight text-slate-950">{value}</strong>
        <span className="pb-1 text-[0.65rem] text-ink-muted">vs. mes anterior</span>
      </div>
      <p className="mt-2 text-xs text-ink-muted">{description}</p>
      <span className={`mt-3 inline-flex items-center gap-2 rounded-full px-3 py-1 text-[0.68rem] font-bold ${badgeClass}`}>
        <span className="size-2 rounded-full bg-current opacity-80" /> {badge}
      </span>
    </article>
  )
}

const evaluations = [
  ['10 Dic 2024', 'EST-4582', 'Restaurant El Buen Sabor', 'Inspección BPM', 'Completada', 'Bajo'],
  ['09 Dic 2024', 'EST-3176', 'Comercial Andina S.A.', 'Verificación', 'En proceso', 'Medio'],
  ['08 Dic 2024', 'EST-2291', 'Panadería La Esperanza', 'Inspección BPM', 'Pendiente', 'Alto'],
  ['07 Dic 2024', 'EST-1120', 'Supermercado Plaza Vea', 'Seguimiento', 'Completada', 'Bajo'],
]

const findings = [
  ['💧', 'Higiene y saneamiento', 48, 'bg-cyan-50'],
  ['🍴', 'Manipulación de alimentos', 32, 'bg-purple-50'],
  ['▥', 'Infraestructura', 18, 'bg-red-50'],
  ['▤', 'Documentación', 15, 'bg-blue-50'],
  ['✣', 'Control de plagas', 12, 'bg-lime-50'],
]

const actions = [
  ['10', 'DIC', 'Revisión de casos críticos', '3 establecimientos', 'border-red-500'],
  ['12', 'DIC', 'Evaluaciones programadas', '5 establecimientos', 'border-emerald-500'],
  ['15', 'DIC', 'Seguimiento de correcciones', '8 establecimientos', 'border-amber-400'],
  ['18', 'DIC', 'Generar reporte mensual', 'Dirección BPM', 'border-slate-400'],
]

export function DashboardOverview() {
  const [modalOpen, setModalOpen] = useState(false)

  return (
    <>
      <section aria-labelledby="overview-title">
        <div className="flex flex-col justify-between gap-4 md:flex-row md:items-end">
          <div>
            <p className="text-xs font-bold tracking-[0.2em] text-brand-700 uppercase">Panel operativo</p>
            <h1 id="overview-title" className="mt-1 text-3xl font-extrabold tracking-tight text-slate-950">Resumen</h1>
            <p className="mt-1 text-sm text-ink-muted">Visión general de las actividades de inspección, evaluaciones y riesgo sanitario.</p>
          </div>
          <div className="flex flex-wrap items-center gap-4">
            <p className="hidden text-right text-xs leading-5 text-brand-900 sm:block">Un entorno más seguro<br />para una mejor salud</p>
            <span className="hidden text-3xl text-emerald-500 sm:block" aria-hidden="true">❧</span>
            <button
              type="button"
              onClick={() => setModalOpen(true)}
              className="inline-flex min-h-11 items-center gap-2 rounded-xl bg-brand-700 px-4 text-sm font-bold text-white shadow-lg shadow-emerald-900/15 transition hover:-translate-y-0.5 hover:bg-brand-800"
            >
              <span className="text-lg">＋</span> Registrar establecimiento
            </button>
          </div>
        </div>

        <div className="mt-5 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <KpiCard icon="▣" iconClass="bg-emerald-100 text-brand-700" title="Casos activos" value="128" trend="+12%" description="Inspecciones y casos en seguimiento." badge="En seguimiento" badgeClass="bg-emerald-50 text-brand-700" />
          <KpiCard icon="▤" iconClass="bg-emerald-50 text-emerald-600" title="Evaluaciones pendientes" value="36" trend="+8%" trendClass="text-red-500" description="Asignaciones por completar." badge="Requiere atención" badgeClass="bg-amber-50 text-amber-700" />
          <KpiCard icon="!" iconClass="bg-red-100 font-extrabold text-red-500" title="Alertas críticas" value="8" trend="-20%" description="Hallazgos de alto riesgo sanitario." badge="Prioridad alta" badgeClass="bg-red-50 text-red-600" />
          <KpiCard icon="▥" iconClass="bg-green-100 text-green-700" title="Riesgo BPM" value="72%" trend="+6%" description="Cumplimiento promedio general." badge="Tendencia positiva" badgeClass="bg-emerald-50 text-brand-700" />
        </div>

        <div className="mt-4 grid gap-4 xl:grid-cols-12">
          <article className="rounded-2xl border border-slate-200/80 bg-white p-5 shadow-card xl:col-span-6">
            <div className="flex items-center justify-between gap-3">
              <h2 className="text-sm font-extrabold text-ink-strong">Actividad de inspecciones y evaluaciones</h2>
              <select aria-label="Período del gráfico" className="rounded-xl border border-slate-200 bg-white px-3 py-2 text-xs text-ink-body outline-none focus:border-brand-500">
                <option>Últimos 12 meses</option><option>Últimos 6 meses</option>
              </select>
            </div>
            <div className="relative mt-5 h-52 overflow-hidden rounded-xl bg-[linear-gradient(to_right,rgba(148,163,184,.15)_1px,transparent_1px),linear-gradient(to_bottom,rgba(148,163,184,.15)_1px,transparent_1px)] bg-[size:48px_34px]">
              <div className="absolute inset-x-5 bottom-7 flex h-36 items-end justify-between gap-2" aria-label="Gráfico ilustrativo de actividad">
                {[42, 55, 38, 64, 48, 68, 76, 88, 52, 61, 72, 82].map((height, index) => (
                  <div key={index} className="flex h-full flex-1 items-end">
                    <span className="w-full rounded-t bg-gradient-to-t from-brand-700 to-emerald-400" style={{ height: `${height}%` }} />
                  </div>
                ))}
              </div>
              <p className="absolute inset-x-0 bottom-1 text-center text-[0.62rem] text-ink-muted">Actividad mensual consolidada</p>
            </div>
          </article>

          <article className="rounded-2xl border border-slate-200/80 bg-white p-5 shadow-card xl:col-span-3">
            <h2 className="text-sm font-extrabold text-ink-strong">Distribución de riesgo BPM</h2>
            <div className="mt-6 flex items-center justify-center gap-5">
              <div className="grid size-36 shrink-0 place-items-center rounded-full bg-[conic-gradient(#11754c_0_55%,#65c77b_55%_83%,#f5b422_83%_95%,#e83a4f_95%)]">
                <div className="grid size-24 place-items-center rounded-full bg-white text-center"><div><strong className="text-2xl">72%</strong><p className="text-[0.6rem] text-ink-muted">Cumplimiento</p></div></div>
              </div>
              <div className="space-y-2 text-[0.68rem]">
                <p><span className="mr-2 inline-block size-2.5 rounded-full bg-brand-700" />Bajo 55%</p>
                <p><span className="mr-2 inline-block size-2.5 rounded-full bg-green-400" />Medio 28%</p>
                <p><span className="mr-2 inline-block size-2.5 rounded-full bg-amber-400" />Alto 12%</p>
                <p><span className="mr-2 inline-block size-2.5 rounded-full bg-red-500" />Crítico 5%</p>
              </div>
            </div>
          </article>

          <article className="rounded-2xl border border-slate-200/80 bg-white p-5 shadow-card xl:col-span-3">
            <h2 className="text-sm font-extrabold text-ink-strong">Establecimientos por región</h2>
            <div className="mt-4 flex h-44 items-center justify-center rounded-xl bg-gradient-to-br from-emerald-50 to-white">
              <div className="text-center text-brand-600"><span className="block text-7xl opacity-35">⌖</span><p className="mt-2 text-[0.65rem] text-ink-muted">Mapa de cobertura territorial</p></div>
            </div>
          </article>
        </div>

        <div className="mt-4 grid gap-4 pb-4 xl:grid-cols-12">
          <article className="overflow-hidden rounded-2xl border border-slate-200/80 bg-white shadow-card xl:col-span-7">
            <div className="flex items-center justify-between px-5 py-4"><h2 className="text-sm font-extrabold text-ink-strong">Últimas evaluaciones</h2><a href="/modulo.html?module=evaluaciones" className="text-xs font-bold text-brand-700">Ver todas →</a></div>
            <div className="overflow-x-auto">
              <table className="w-full min-w-[720px] text-left text-xs">
                <thead className="border-y border-slate-100 bg-slate-50 text-[0.62rem] tracking-wide text-ink-muted"><tr>{['FECHA', 'CÓDIGO', 'ESTABLECIMIENTO', 'TIPO', 'ESTADO', 'RIESGO', 'ACCIONES'].map((heading) => <th key={heading} className="px-3 py-2.5 first:pl-5">{heading}</th>)}</tr></thead>
                <tbody className="divide-y divide-slate-100 text-ink-body">
                  {evaluations.map(([date, code, establishment, type, status, risk]) => (
                    <tr key={code} className="hover:bg-slate-50">
                      <td className="py-3 pr-3 pl-5">{date}</td><td className="px-3 py-3 font-bold">{code}</td><td className="px-3 py-3 text-ink-strong">{establishment}</td><td className="px-3 py-3">{type}</td>
                      <td className="px-3 py-3"><span className={`rounded-full px-2.5 py-1 font-bold ${status === 'Completada' ? 'bg-emerald-50 text-emerald-700' : status === 'En proceso' ? 'bg-amber-50 text-amber-700' : 'bg-red-50 text-red-600'}`}>{status}</span></td>
                      <td className={`px-3 py-3 font-bold ${risk === 'Bajo' ? 'text-emerald-600' : risk === 'Medio' ? 'text-amber-600' : 'text-red-600'}`}>{risk}</td><td className="px-3 py-3 text-center"><button type="button" aria-label={`Acciones para ${code}`} className="text-lg">•••</button></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </article>

          <article className="rounded-2xl border border-slate-200/80 bg-white p-5 shadow-card xl:col-span-2">
            <div className="flex items-center justify-between"><h2 className="text-sm font-extrabold">Hallazgos</h2><a href="/modulo.html?module=hallazgos" className="text-[0.65rem] font-bold text-brand-700">Ver todos</a></div>
            <ul className="mt-3 divide-y divide-slate-100 text-xs">
              {findings.map(([icon, label, count, color]) => <li key={String(label)} className="flex items-center gap-2 py-2.5"><span className={`grid size-7 place-items-center rounded-lg ${color}`}>{icon}</span><span className="min-w-0 flex-1 truncate">{label}</span><strong>{count}</strong></li>)}
            </ul>
          </article>

          <article className="rounded-2xl border border-slate-200/80 bg-white p-5 shadow-card xl:col-span-3">
            <div className="flex items-center justify-between"><h2 className="text-sm font-extrabold">Próximas acciones</h2><button type="button" className="text-[0.65rem] font-bold text-brand-700">Ver todas</button></div>
            <div className="mt-3 space-y-2 text-xs">
              {actions.map(([day, month, title, subtitle, border]) => <div key={String(title)} className="flex gap-3 rounded-xl bg-slate-50 p-2.5"><div className={`w-10 shrink-0 border-r-2 text-center ${border}`}><strong className="block text-base">{day}</strong><span className="text-[0.55rem] text-ink-muted">{month}</span></div><div><p className="font-bold">{title}</p><p className="mt-1 text-ink-muted">{subtitle}</p></div></div>)}
            </div>
          </article>
        </div>
      </section>

      <EstablishmentFormModal open={modalOpen} onClose={() => setModalOpen(false)} />
    </>
  )
}
