import { useEffect, useState, type FormEvent } from 'react'
import { TableSkeleton } from '../../components/feedback/Skeletons'
import { Pagination } from '../../components/ui/Pagination'
import { useParameterOptions } from '../../hooks/useParameterOptions'
import {
  cancelSchedule,
  createSchedule,
  getScheduleOptions,
  getSchedules,
  updateSchedule,
  type Schedule,
  type ScheduleDraft,
  type ScheduleOptions,
  type SchedulesPage,
} from '../../lib/api'
import { alerts } from '../../lib/alerts'
import { formatStatusLabel } from '../../lib/formatters'
import {
  futureLocalDateTime,
  toLocalDateTimeInput,
  toUtcIsoFromLocalInput,
} from '../../lib/dateTime'
import { queueSchedule } from '../../offline/syncQueue'

const emptyPage: SchedulesPage = { items: [], page: 1, pageSize: 10, total: 0 }
type CalendarView = 'day' | 'week' | 'month'

export function SchedulingManagement() {
  const scheduleStates = useParameterOptions('ESTADO_PROGRAMACION')
  const [result, setResult] = useState(emptyPage)
  const [options, setOptions] = useState<ScheduleOptions | null>(null)
  const [status, setStatus] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [editing, setEditing] = useState<Schedule | null>(null)
  const [open, setOpen] = useState(false)
  const [calendarView, setCalendarView] = useState<CalendarView>('month')
  const [anchorDate, setAnchorDate] = useState(() => new Date().toISOString().slice(0, 10))
  const [pageNumber, setPageNumber] = useState(1)

  async function load() {
    setLoading(true)
    try {
      setResult(await getSchedules({ status, page: pageNumber, pageSize: 10 }))
      setError('')
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'No se pudo cargar la programación.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    let active = true
    void Promise.all([
      getSchedules({ status, page: pageNumber, pageSize: 10 }),
      getScheduleOptions(),
    ])
      .then(([page, choices]) => {
        if (active) {
          setResult(page)
          setOptions(choices)
          setError('')
        }
      })
      .catch(
        (caught) =>
          active &&
          setError(caught instanceof Error ? caught.message : 'No se pudo cargar la programación.'),
      )
      .finally(() => active && setLoading(false))
    return () => {
      active = false
    }
  }, [pageNumber, status])

  async function save(draft: ScheduleDraft) {
    try {
      if (editing) await updateSchedule(editing.id, draft)
      else if (navigator.onLine) await createSchedule(draft)
      else await queueSchedule({ ...draft, rowVersion: null })
      setOpen(false)
      if (navigator.onLine) await load()
      await alerts.success(
        editing
          ? 'Programación actualizada'
          : navigator.onLine
            ? 'Visita programada'
            : 'Programación guardada sin conexión',
      )
    } catch (caught) {
      await alerts.error(caught, 'No se pudo guardar la programación')
      throw caught
    }
  }

  async function cancel(item: Schedule) {
    const reason = await alerts.textInput({
      title: 'Cancelar programación',
      label: 'Indique el motivo de la cancelación.',
      confirmText: 'Cancelar visita',
    })
    if (!reason) return
    try {
      await cancelSchedule(item.id, item.rowVersion, reason)
      await load()
      await alerts.success('Programación cancelada')
    } catch (caught) {
      await alerts.error(caught, 'No se pudo cancelar la programación')
    }
  }

  const bounds = calendarBounds(anchorDate, calendarView)
  const visibleItems = result.items
    .filter((item) => {
      const startsAt = new Date(item.startsAt)
      return startsAt >= bounds.start && startsAt < bounds.end
    })
    .sort((left, right) => Date.parse(left.startsAt) - Date.parse(right.startsAt))
  const periodLabel = formatCalendarPeriod(bounds.start, bounds.end, calendarView)

  function moveCalendar(direction: -1 | 1) {
    const next = new Date(`${anchorDate}T12:00:00`)
    if (calendarView === 'day') next.setDate(next.getDate() + direction)
    else if (calendarView === 'week') next.setDate(next.getDate() + direction * 7)
    else next.setMonth(next.getMonth() + direction)
    setAnchorDate(toDateInput(next))
  }

  return (
    <section aria-labelledby="scheduling-title">
      <div className="flex flex-col justify-between gap-4 md:flex-row md:items-end">
        <div>
          <p className="text-xs font-bold tracking-[0.14em] text-brand-700 uppercase">
            Planificación institucional
          </p>
          <h1 id="scheduling-title" className="mt-2 text-2xl font-extrabold">
            Programación
          </h1>
          <p className="mt-2 text-sm text-ink-muted">
            Consulte su agenda y atienda cada inspección dentro del intervalo asignado.
          </p>
        </div>
        {options?.canManage && (
          <button
            type="button"
            onClick={() => {
              setEditing(null)
              setOpen(true)
            }}
            className="min-h-11 rounded-xl bg-brand-700 px-5 font-bold text-white"
          >
            + Programar visita
          </button>
        )}
      </div>
      <div className="mt-6 rounded-card bg-white p-5 shadow-card">
        <div className="flex flex-wrap items-end justify-between gap-4">
          <label className="block min-w-56 text-sm font-bold">
            Estado
            <select
              value={status}
              onChange={(event) => {
                setStatus(event.target.value)
                setPageNumber(1)
              }}
              className="mt-1.5 min-h-11 w-full rounded-xl border px-3 font-normal"
            >
              <option value="">Todos</option>
              {scheduleStates.options.map((value) => (
                <option key={value.parametersId} value={value.stringData ?? ''}>
                  {formatStatusLabel(value.stringData ?? '')}
                </option>
              ))}
            </select>
          </label>
          <label className="block text-sm font-bold">
            Fecha de referencia
            <input
              type="date"
              value={anchorDate}
              onChange={(event) => setAnchorDate(event.target.value)}
              className="mt-1.5 min-h-11 rounded-xl border px-3 font-normal"
            />
          </label>
          <div
            className="flex rounded-xl border border-slate-300 p-1"
            aria-label="Vista del calendario"
          >
            {(['day', 'week', 'month'] as const).map((view) => (
              <button
                key={view}
                type="button"
                onClick={() => setCalendarView(view)}
                className={`min-h-9 rounded-lg px-3 text-sm font-bold ${calendarView === view ? 'bg-brand-700 text-white' : 'text-ink-body'}`}
              >
                {view === 'day' ? 'Día' : view === 'week' ? 'Semana' : 'Mes'}
              </button>
            ))}
          </div>
        </div>
        <div className="mt-4 flex flex-wrap items-center justify-between gap-3 rounded-xl bg-slate-50 p-3">
          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={() => moveCalendar(-1)}
              className="rounded-lg border border-slate-300 bg-white px-3 py-2 font-bold"
              aria-label="Período anterior"
            >
              ←
            </button>
            <button
              type="button"
              onClick={() => setAnchorDate(toDateInput(new Date()))}
              className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm font-bold"
            >
              Hoy
            </button>
            <button
              type="button"
              onClick={() => moveCalendar(1)}
              className="rounded-lg border border-slate-300 bg-white px-3 py-2 font-bold"
              aria-label="Período siguiente"
            >
              →
            </button>
          </div>
          <strong className="text-sm text-ink-strong">{periodLabel}</strong>
          <span className="text-xs font-semibold text-ink-muted">
            {visibleItems.length} inspección(es) en esta vista
          </span>
        </div>
        {!scheduleStates.loading && scheduleStates.options.length === 0 && (
          <span className="mt-2 block text-xs font-normal text-amber-700">
            El catálogo ESTADO_PROGRAMACION no tiene valores activos.
          </span>
        )}
        {error && (
          <div
            role="alert"
            className="mt-5 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800"
          >
            {error}
          </div>
        )}
        <div className="mt-5 overflow-x-auto">
          <table className="w-full min-w-[900px] text-left text-sm">
            <thead>
              <tr className="border-b text-xs text-ink-muted uppercase">
                <th className="px-3 py-3">Caso</th>
                <th className="px-3 py-3">Fecha</th>
                <th className="px-3 py-3">Técnicos</th>
                <th className="px-3 py-3">Prioridad</th>
                <th className="px-3 py-3">Estado</th>
                <th className="px-3 py-3 text-right">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {visibleItems.map((item) => {
                const timing = scheduleTiming(item)
                return (
                  <tr key={item.id} className={`border-b border-slate-100 ${timing.rowClass}`}>
                    <td className="px-3 py-4 font-bold">
                      {item.caseNumber}
                      <span className="block text-xs font-normal text-ink-muted">
                        {item.companyName} · {item.establishmentName}
                      </span>
                      <span className="mt-1 block text-xs font-normal text-ink-muted">
                        {formatStatusLabel(item.caseOrigin)}
                        {item.requestNumber ? ` · Solicitud ${item.requestNumber}` : ''}
                      </span>
                      {item.location && (
                        <span className="mt-1 block text-xs font-normal text-ink-muted">
                          {item.location}
                        </span>
                      )}
                    </td>
                    <td className="px-3 py-4">
                      {new Date(item.startsAt).toLocaleString()}
                      <span className="block text-xs text-ink-muted">
                        hasta {new Date(item.endsAt).toLocaleString()}
                      </span>
                      {timing.label && (
                        <span
                          className={`mt-1 inline-flex rounded-full px-2 py-1 text-xs font-bold ${timing.badgeClass}`}
                        >
                          {timing.label}
                        </span>
                      )}
                    </td>
                    <td className="px-3 py-4">{item.evaluatorNames.join(', ')}</td>
                    <td className="px-3 py-4">{item.priority}</td>
                    <td className="px-3 py-4">{formatStatusLabel(item.status)}</td>
                    <td className="px-3 py-4 text-right">
                      {options?.canManage && !['CANCELADA', 'COMPLETADA'].includes(item.status) && (
                        <div className="flex justify-end gap-2">
                          <button
                            type="button"
                            onClick={() => {
                              setEditing(item)
                              setOpen(true)
                            }}
                            className="rounded-lg border px-3 py-2 font-bold"
                          >
                            Reprogramar
                          </button>
                          <button
                            type="button"
                            onClick={() => void cancel(item)}
                            className="rounded-lg border border-red-300 px-3 py-2 font-bold text-red-700"
                          >
                            Cancelar
                          </button>
                        </div>
                      )}
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
          {loading && <TableSkeleton rows={5} columns={6} />}
          {!loading && !error && visibleItems.length === 0 && (
            <p className="p-10 text-center text-sm text-ink-muted">
              No hay visitas programadas para esta vista del calendario.
            </p>
          )}
        </div>
        <Pagination
          page={pageNumber}
          pageSize={result.pageSize}
          total={result.total}
          disabled={loading}
          label="programaciones"
          onChange={setPageNumber}
        />
      </div>
      {open && options && (
        <ScheduleForm
          item={editing}
          options={options}
          onClose={() => setOpen(false)}
          onSave={save}
        />
      )}
    </section>
  )
}

function calendarBounds(anchor: string, view: CalendarView) {
  const start = new Date(`${anchor}T00:00:00`)
  if (view === 'week') {
    const mondayOffset = (start.getDay() + 6) % 7
    start.setDate(start.getDate() - mondayOffset)
  } else if (view === 'month') {
    start.setDate(1)
  }
  const end = new Date(start)
  if (view === 'day') end.setDate(end.getDate() + 1)
  else if (view === 'week') end.setDate(end.getDate() + 7)
  else end.setMonth(end.getMonth() + 1)
  return { start, end }
}

function toDateInput(value: Date) {
  const year = value.getFullYear()
  const month = String(value.getMonth() + 1).padStart(2, '0')
  const day = String(value.getDate()).padStart(2, '0')
  return `${year}-${month}-${day}`
}

function formatCalendarPeriod(start: Date, end: Date, view: CalendarView) {
  const formatter = new Intl.DateTimeFormat('es-DO', {
    day: view === 'month' ? undefined : 'numeric',
    month: 'long',
    year: 'numeric',
  })
  if (view === 'day' || view === 'month') return formatter.format(start)
  const inclusiveEnd = new Date(end)
  inclusiveEnd.setDate(inclusiveEnd.getDate() - 1)
  return `${formatter.format(start)} – ${formatter.format(inclusiveEnd)}`
}

export function scheduleTiming(
  item: Pick<Schedule, 'startsAt' | 'endsAt' | 'status'>,
  now = Date.now(),
) {
  if (['CANCELADA', 'COMPLETADA'].includes(item.status))
    return { label: '', badgeClass: '', rowClass: '' }
  const startsAt = Date.parse(item.startsAt)
  const endsAt = Date.parse(item.endsAt)
  if (endsAt < now)
    return {
      label: 'Plazo vencido',
      badgeClass: 'bg-red-100 text-red-800',
      rowClass: 'bg-red-50/60',
    }
  if (startsAt <= now)
    return {
      label: 'Atender ahora',
      badgeClass: 'bg-amber-100 text-amber-900',
      rowClass: 'bg-amber-50/60',
    }
  if (startsAt - now <= 24 * 60 * 60 * 1000)
    return {
      label: 'Próxima en menos de 24 h',
      badgeClass: 'bg-blue-100 text-blue-800',
      rowClass: '',
    }
  return { label: '', badgeClass: '', rowClass: '' }
}

function ScheduleForm({
  item,
  options,
  onClose,
  onSave,
}: {
  item: Schedule | null
  options: ScheduleOptions
  onClose: () => void
  onSave: (draft: ScheduleDraft) => Promise<void>
}) {
  const [caseId, setCaseId] = useState(item?.caseId ?? '')
  const [startsAt, setStartsAt] = useState(
    item ? toLocalDateTimeInput(item.startsAt) : futureLocalDateTime(60),
  )
  const [endsAt, setEndsAt] = useState(
    item ? toLocalDateTimeInput(item.endsAt) : futureLocalDateTime(120),
  )
  const [priority, setPriority] = useState(item?.priority ?? 3)
  const [evaluatorIds, setEvaluatorIds] = useState(item?.evaluatorIds ?? [])
  const [observations, setObservations] = useState(item?.observations ?? '')
  const [changeReason, setChangeReason] = useState(item?.changeReason ?? '')
  const [saving, setSaving] = useState(false)
  async function submit(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    try {
      await onSave({
        caseId,
        startsAt: toUtcIsoFromLocalInput(startsAt),
        endsAt: toUtcIsoFromLocalInput(endsAt),
        priority,
        evaluatorIds,
        observations,
        changeReason,
        idempotencyKey: crypto.randomUUID(),
        rowVersion: item?.rowVersion ?? null,
      })
    } finally {
      setSaving(false)
    }
  }
  return (
    <div
      className="sigersa-modal-overlay fixed inset-0 z-50 grid place-items-center p-4"
      role="presentation"
    >
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby="schedule-form-title"
        className="sigersa-modal-panel w-full max-w-2xl p-6"
      >
        <div className="flex justify-between">
          <h2 id="schedule-form-title" className="text-xl font-extrabold">
            {item ? 'Reprogramar visita' : 'Programar visita'}
          </h2>
          <button type="button" onClick={onClose} aria-label="Cerrar">
            ✕
          </button>
        </div>
        <form onSubmit={(event) => void submit(event)} className="mt-6 grid gap-4 md:grid-cols-2">
          <label className="text-sm font-bold md:col-span-2">
            Caso
            <select
              required
              disabled={Boolean(item)}
              value={caseId}
              onChange={(e) => setCaseId(e.target.value)}
              className="mt-1.5 min-h-11 w-full rounded-xl border px-3 font-normal disabled:bg-slate-100"
            >
              <option value="">Seleccione</option>
              {item && <option value={item.caseId}>{item.caseNumber}</option>}
              {options.cases.map((option) => (
                <option key={option.id} value={option.id}>
                  {option.name}
                </option>
              ))}
            </select>
          </label>
          <label className="text-sm font-bold">
            Inicio
            <input
              required
              type="datetime-local"
              value={startsAt}
              min={item ? undefined : futureLocalDateTime(0)}
              onChange={(e) => {
                const nextStart = e.target.value
                setStartsAt(nextStart)
                if (endsAt && new Date(endsAt) <= new Date(nextStart)) {
                  setEndsAt(toLocalDateTimeInput(new Date(nextStart).getTime() + 60 * 60_000))
                }
              }}
              className="mt-1.5 min-h-11 w-full rounded-xl border px-3 font-normal"
            />
          </label>
          <label className="text-sm font-bold">
            Fin
            <input
              required
              type="datetime-local"
              min={startsAt}
              value={endsAt}
              onChange={(e) => setEndsAt(e.target.value)}
              className="mt-1.5 min-h-11 w-full rounded-xl border px-3 font-normal"
            />
          </label>
          <label className="text-sm font-bold">
            Prioridad
            <input
              required
              type="number"
              min="1"
              max="5"
              value={priority}
              onChange={(e) => setPriority(Number(e.target.value))}
              className="mt-1.5 min-h-11 w-full rounded-xl border px-3 font-normal"
            />
          </label>
          <label className="text-sm font-bold">
            {item ? 'Motivo del cambio' : 'Observaciones'}
            <input
              required={Boolean(item)}
              value={item ? changeReason : observations}
              onChange={(e) =>
                item ? setChangeReason(e.target.value) : setObservations(e.target.value)
              }
              className="mt-1.5 min-h-11 w-full rounded-xl border px-3 font-normal"
            />
          </label>
          <fieldset className="md:col-span-2">
            <legend className="text-sm font-bold">Técnicos evaluadores</legend>
            <div className="mt-2 grid gap-2 sm:grid-cols-2">
              {options.evaluators.map((option) => (
                <label
                  key={option.id}
                  className="flex items-center gap-2 rounded-xl border p-3 text-sm"
                >
                  <input
                    type="checkbox"
                    checked={evaluatorIds.includes(option.id)}
                    onChange={(e) =>
                      setEvaluatorIds((ids) =>
                        e.target.checked
                          ? [...ids, option.id]
                          : ids.filter((id) => id !== option.id),
                      )
                    }
                  />
                  {option.name}
                </label>
              ))}
            </div>
          </fieldset>
          <div className="flex justify-end gap-3 border-t pt-5 md:col-span-2">
            <button
              type="button"
              onClick={onClose}
              className="rounded-xl border px-4 py-2.5 font-bold"
            >
              Volver
            </button>
            <button
              disabled={saving || evaluatorIds.length === 0}
              className="rounded-xl bg-brand-700 px-5 py-2.5 font-bold text-white disabled:opacity-50"
            >
              {saving ? 'Guardando…' : 'Guardar'}
            </button>
          </div>
        </form>
      </section>
    </div>
  )
}
