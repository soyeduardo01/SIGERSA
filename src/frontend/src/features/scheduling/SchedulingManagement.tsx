import { useEffect, useState, type FormEvent } from 'react'
import { TableSkeleton } from '../../components/feedback/Skeletons'
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
import { queueSchedule } from '../../offline/syncQueue'

const emptyPage: SchedulesPage = { items: [], page: 1, pageSize: 50, total: 0 }

export function SchedulingManagement() {
  const scheduleStates = useParameterOptions('ESTADO_PROGRAMACION')
  const [result, setResult] = useState(emptyPage)
  const [options, setOptions] = useState<ScheduleOptions | null>(null)
  const [status, setStatus] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [editing, setEditing] = useState<Schedule | null>(null)
  const [open, setOpen] = useState(false)

  async function load() {
    setLoading(true)
    try {
      setResult(await getSchedules({ status }))
      setError('')
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'No se pudo cargar la programación.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    let active = true
    void Promise.all([getSchedules({ status }), getScheduleOptions()])
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
  }, [status])

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
            Asigne fechas y técnicos a los casos abiertos.
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
        <label className="block max-w-xs text-sm font-bold">
          Estado
          <select
            value={status}
            onChange={(event) => setStatus(event.target.value)}
            className="mt-1.5 min-h-11 w-full rounded-xl border px-3 font-normal"
          >
            <option value="">Todos</option>
            {scheduleStates.options.map((value) => (
              <option key={value.parametersId} value={value.stringData ?? ''}>
                {formatStatusLabel(value.stringData ?? '')}
              </option>
            ))}
          </select>
          {!scheduleStates.loading && scheduleStates.options.length === 0 && (
            <span className="mt-1 block text-xs font-normal text-amber-700">
              El catálogo ESTADO_PROGRAMACION no tiene valores activos.
            </span>
          )}
        </label>
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
              {result.items.map((item) => (
                <tr key={item.id} className="border-b border-slate-100">
                  <td className="px-3 py-4 font-bold">
                    {item.caseNumber}
                    <span className="block text-xs font-normal text-ink-muted">
                      {item.establishmentName}
                    </span>
                  </td>
                  <td className="px-3 py-4">
                    {new Date(item.startsAt).toLocaleString()}
                    <span className="block text-xs text-ink-muted">
                      hasta {new Date(item.endsAt).toLocaleString()}
                    </span>
                  </td>
                  <td className="px-3 py-4">{item.evaluatorNames.join(', ')}</td>
                  <td className="px-3 py-4">{item.priority}</td>
                  <td className="px-3 py-4">{formatStatusLabel(item.status)}</td>
                  <td className="px-3 py-4 text-right">
                    {!['CANCELADA', 'COMPLETADA'].includes(item.status) && (
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
              ))}
            </tbody>
          </table>
          {loading && <TableSkeleton rows={5} columns={6} />}
          {!loading && !error && result.items.length === 0 && (
            <p className="p-10 text-center text-sm text-ink-muted">No hay visitas programadas.</p>
          )}
        </div>
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

function localDateTime(value: string) {
  const date = new Date(value)
  const offset = date.getTimezoneOffset() * 60_000
  return new Date(date.getTime() - offset).toISOString().slice(0, 16)
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
  const [startsAt, setStartsAt] = useState(item ? localDateTime(item.startsAt) : '')
  const [endsAt, setEndsAt] = useState(item ? localDateTime(item.endsAt) : '')
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
        startsAt: new Date(startsAt).toISOString(),
        endsAt: new Date(endsAt).toISOString(),
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
              onChange={(e) => setStartsAt(e.target.value)}
              className="mt-1.5 min-h-11 w-full rounded-xl border px-3 font-normal"
            />
          </label>
          <label className="text-sm font-bold">
            Fin
            <input
              required
              type="datetime-local"
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
