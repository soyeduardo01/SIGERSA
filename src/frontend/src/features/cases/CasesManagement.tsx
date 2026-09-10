import { useEffect, useState, type FormEvent } from 'react'
import { TableSkeleton } from '../../components/feedback/Skeletons'
import {
  closeCase,
  createCase,
  getCaseOptions,
  getCases,
  updateCase,
  type CaseDraft,
  type CaseOptions,
  type CasesPage,
  type InspectionCase,
} from '../../lib/api'
import { alerts } from '../../lib/alerts'
import { formatStatusLabel } from '../../lib/formatters'
import { queueCase } from '../../offline/syncQueue'

const emptyPage: CasesPage = { items: [], page: 1, pageSize: 10, total: 0 }

export function CasesManagement() {
  const [result, setResult] = useState(emptyPage)
  const [options, setOptions] = useState<CaseOptions | null>(null)
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [editing, setEditing] = useState<InspectionCase | null>(null)
  const [modalOpen, setModalOpen] = useState(false)

  useEffect(() => {
    let active = true
    void getCaseOptions()
      .then((value) => active && setOptions(value))
      .catch(
        (caught) =>
          active &&
          setError(
            caught instanceof Error ? caught.message : 'No se pudieron cargar las opciones.',
          ),
      )
    return () => {
      active = false
    }
  }, [])

  async function load() {
    setLoading(true)
    try {
      setResult(await getCases({ search, status, pageSize: 50 }))
      setError('')
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'No se pudieron cargar los casos.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    let active = true
    void getCases({ search, status, pageSize: 50 })
      .then((value) => {
        if (active) {
          setResult(value)
          setError('')
        }
      })
      .catch(
        (caught) =>
          active &&
          setError(caught instanceof Error ? caught.message : 'No se pudieron cargar los casos.'),
      )
      .finally(() => active && setLoading(false))
    return () => {
      active = false
    }
  }, [search, status])

  async function save(draft: CaseDraft) {
    try {
      if (editing) await updateCase(editing.id, draft)
      else if (navigator.onLine) await createCase(draft)
      else await queueCase({ ...draft, rowVersion: null })
      setModalOpen(false)
      if (navigator.onLine) await load()
      await alerts.success(
        editing
          ? 'Caso actualizado'
          : navigator.onLine
            ? 'Caso creado'
            : 'Caso guardado sin conexión',
      )
    } catch (caught) {
      await alerts.error(caught, 'No se pudo guardar el caso')
      throw caught
    }
  }

  async function close(item: InspectionCase) {
    const reason = await alerts.textInput({
      title: 'Cerrar caso',
      label: 'Explique por qué se cierra este caso.',
      confirmText: 'Cerrar caso',
    })
    if (!reason) return
    try {
      await closeCase(item.id, item.rowVersion, reason)
      await load()
      await alerts.success('Caso cerrado')
    } catch (caught) {
      await alerts.error(caught, 'No se pudo cerrar el caso')
    }
  }

  return (
    <section aria-labelledby="cases-title">
      <div className="flex flex-col justify-between gap-4 md:flex-row md:items-end">
        <div>
          <p className="text-xs font-bold tracking-[0.14em] text-brand-700 uppercase">
            Gestión EBR/BPM
          </p>
          <h1 id="cases-title" className="mt-2 text-2xl font-extrabold text-ink-strong">
            Casos
          </h1>
          <p className="mt-2 text-sm text-ink-muted">
            Analice, priorice y asigne responsable a cada caso sanitario.
          </p>
        </div>
        {options?.canManage && (
          <button
            type="button"
            onClick={() => {
              setEditing(null)
              setModalOpen(true)
            }}
            className="min-h-11 rounded-xl bg-brand-700 px-5 font-bold text-white"
          >
            + Nuevo caso
          </button>
        )}
      </div>
      <div className="mt-6 rounded-card bg-white p-5 shadow-card">
        <form
          onSubmit={(event) => {
            event.preventDefault()
            setSearch(searchInput.trim())
          }}
          className="grid gap-3 md:grid-cols-[1fr_14rem_auto]"
        >
          <label className="text-sm font-bold">
            Buscar
            <input
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder="Número, empresa o establecimiento"
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
            />
          </label>
          <label className="text-sm font-bold">
            Estado
            <select
              value={status}
              onChange={(event) => setStatus(event.target.value)}
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
            >
              <option value="">Todos</option>
              <option value="ABIERTO">Abierto</option>
              <option value="ANALIZADO">Analizado</option>
              <option value="CERRADO">Cerrado</option>
            </select>
          </label>
          <button className="text-brand-800 mt-auto min-h-11 rounded-xl border border-brand-700 px-5 font-bold">
            Aplicar
          </button>
        </form>
        {error && (
          <div
            role="alert"
            className="mt-5 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800"
          >
            {error}
          </div>
        )}
        <div className="mt-5 overflow-x-auto">
          <table className="w-full min-w-[900px] border-collapse text-left text-sm">
            <thead>
              <tr className="border-b border-slate-200 text-xs text-ink-muted uppercase">
                <th className="px-3 py-3">Caso</th>
                <th className="px-3 py-3">Empresa / establecimiento</th>
                <th className="px-3 py-3">Prioridad</th>
                <th className="px-3 py-3">Responsable</th>
                <th className="px-3 py-3">Estado</th>
                <th className="px-3 py-3 text-right">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {result.items.map((item) => (
                <tr key={item.id} className="border-b border-slate-100">
                  <td className="px-3 py-4 font-bold">{item.number}</td>
                  <td className="px-3 py-4">
                    {item.companyName}
                    <span className="block text-xs text-ink-muted">{item.establishmentName}</span>
                  </td>
                  <td className="px-3 py-4">{item.priority}</td>
                  <td className="px-3 py-4">{item.responsibleName ?? 'Sin asignar'}</td>
                  <td className="px-3 py-4">
                    <span className="rounded-full bg-slate-100 px-2.5 py-1 text-xs font-bold">
                      {formatStatusLabel(item.status)}
                    </span>
                  </td>
                  <td className="px-3 py-4 text-right">
                    {options?.canManage && item.status !== 'CERRADO' && (
                      <div className="flex justify-end gap-2">
                        <button
                          type="button"
                          onClick={() => {
                            setEditing(item)
                            setModalOpen(true)
                          }}
                          className="rounded-lg border px-3 py-2 font-bold"
                        >
                          Analizar
                        </button>
                        <button
                          type="button"
                          onClick={() => void close(item)}
                          className="rounded-lg border border-red-300 px-3 py-2 font-bold text-red-700"
                        >
                          Cerrar
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
            <p className="p-10 text-center text-sm text-ink-muted">No hay casos para mostrar.</p>
          )}
        </div>
      </div>
      {modalOpen && options && (
        <CaseForm
          item={editing}
          options={options}
          onClose={() => setModalOpen(false)}
          onSave={save}
        />
      )}
    </section>
  )
}

function CaseForm({
  item,
  options,
  onClose,
  onSave,
}: {
  item: InspectionCase | null
  options: CaseOptions
  onClose: () => void
  onSave: (draft: CaseDraft) => Promise<void>
}) {
  const [requestId, setRequestId] = useState(item?.requestId ?? '')
  const [priority, setPriority] = useState(item?.priority ?? 3)
  const [responsibleId, setResponsibleId] = useState(item?.responsibleId ?? '')
  const [decision, setDecision] = useState(item?.analysisDecision ?? '')
  const [reason, setReason] = useState(item?.decisionReason ?? '')
  const [saving, setSaving] = useState(false)
  async function submit(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    try {
      await onSave({
        requestId,
        priority,
        responsibleId: responsibleId || null,
        analysisDecision: decision || null,
        decisionReason: reason,
        idempotencyKey: crypto.randomUUID(),
        rowVersion: item?.rowVersion ?? null,
      })
    } finally {
      setSaving(false)
    }
  }
  return (
    <div
      className="fixed inset-0 z-50 grid place-items-center bg-slate-950/55 p-4"
      role="presentation"
    >
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby="case-form-title"
        className="w-full max-w-2xl rounded-2xl bg-white p-6 shadow-2xl"
      >
        <div className="flex justify-between">
          <div>
            <h2 id="case-form-title" className="text-xl font-extrabold">
              {item ? 'Analizar caso' : 'Crear caso desde solicitud'}
            </h2>
            <p className="mt-1 text-sm text-ink-muted">
              Defina prioridad, responsable y decisión inicial.
            </p>
          </div>
          <button type="button" onClick={onClose} aria-label="Cerrar" className="p-2">
            ✕
          </button>
        </div>
        <form onSubmit={(event) => void submit(event)} className="mt-6 grid gap-4 md:grid-cols-2">
          <label className="text-sm font-bold md:col-span-2">
            Solicitud enviada
            <select
              required
              disabled={Boolean(item)}
              value={requestId}
              onChange={(event) => setRequestId(event.target.value)}
              className="mt-1.5 min-h-11 w-full rounded-xl border px-3 font-normal disabled:bg-slate-100"
            >
              <option value="">Seleccione</option>
              {item?.requestId && <option value={item.requestId}>{item.number}</option>}
              {options.requests.map((option) => (
                <option key={option.id} value={option.id}>
                  {option.name}
                </option>
              ))}
            </select>
          </label>
          <label className="text-sm font-bold">
            Prioridad (1–5)
            <input
              required
              type="number"
              min="1"
              max="5"
              value={priority}
              onChange={(event) => setPriority(Number(event.target.value))}
              className="mt-1.5 min-h-11 w-full rounded-xl border px-3 font-normal"
            />
          </label>
          <label className="text-sm font-bold">
            Responsable
            <select
              value={responsibleId}
              onChange={(event) => setResponsibleId(event.target.value)}
              className="mt-1.5 min-h-11 w-full rounded-xl border px-3 font-normal"
            >
              <option value="">Sin asignar</option>
              {options.responsibleUsers.map((option) => (
                <option key={option.id} value={option.id}>
                  {option.name}
                </option>
              ))}
            </select>
          </label>
          <label className="text-sm font-bold">
            Decisión
            <select
              value={decision}
              onChange={(event) => setDecision(event.target.value)}
              className="mt-1.5 min-h-11 w-full rounded-xl border px-3 font-normal"
            >
              <option value="">Pendiente</option>
              <option value="PROCEDE">Procede</option>
              <option value="NO_PROCEDE">No procede</option>
              <option value="REQUIERE_INFORMACION">Requiere información</option>
            </select>
          </label>
          <label className="text-sm font-bold">
            Justificación
            <input
              value={reason}
              onChange={(event) => setReason(event.target.value)}
              maxLength={2000}
              className="mt-1.5 min-h-11 w-full rounded-xl border px-3 font-normal"
            />
          </label>
          <div className="flex justify-end gap-3 border-t pt-5 md:col-span-2">
            <button
              type="button"
              onClick={onClose}
              className="rounded-xl border px-4 py-2.5 font-bold"
            >
              Volver
            </button>
            <button
              disabled={saving}
              className="rounded-xl bg-brand-700 px-5 py-2.5 font-bold text-white"
            >
              {saving ? 'Guardando…' : 'Guardar'}
            </button>
          </div>
        </form>
      </section>
    </div>
  )
}
