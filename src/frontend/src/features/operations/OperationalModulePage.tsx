import { useMemo, useState, type FormEvent } from 'react'
import { alerts } from '../../lib/alerts'
import { formatStatusLabel } from '../../lib/formatters'

export interface OperationalModuleConfig {
  title: string
  eyebrow: string
  description: string
  actionLabel: string
  referenceLabel: string
  detailLabel: string
  statuses: string[]
  canCreate: boolean
}

interface DraftRecord {
  id: string
  reference: string
  detail: string
  status: string
  updatedAt: string
}

export function OperationalModulePage({ config }: { config: OperationalModuleConfig }) {
  const [records, setRecords] = useState<DraftRecord[]>([])
  const [query, setQuery] = useState('')
  const [status, setStatus] = useState('')
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<DraftRecord | null>(null)
  const [draft, setDraft] = useState({ reference: '', detail: '', status: config.statuses[0] })

  const filtered = useMemo(() => {
    const normalized = query.trim().toLowerCase()
    return records.filter(
      (record) =>
        (!status || record.status === status) &&
        (!normalized ||
          record.reference.toLowerCase().includes(normalized) ||
          record.detail.toLowerCase().includes(normalized)),
    )
  }, [query, records, status])

  function openCreate() {
    setEditing(null)
    setDraft({ reference: '', detail: '', status: config.statuses[0] })
    setModalOpen(true)
  }

  function openEdit(record: DraftRecord) {
    setEditing(record)
    setDraft({ reference: record.reference, detail: record.detail, status: record.status })
    setModalOpen(true)
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    const updated: DraftRecord = {
      id: editing?.id ?? crypto.randomUUID(),
      ...draft,
      updatedAt: new Date().toISOString(),
    }
    setRecords((current) =>
      editing
        ? current.map((record) => (record.id === editing.id ? updated : record))
        : [updated, ...current],
    )
    setModalOpen(false)
    await alerts.success(
      editing ? 'Borrador actualizado' : 'Borrador agregado',
      'El registro queda listo para conectarse con su caso de uso del API.',
    )
  }

  async function remove(record: DraftRecord) {
    const confirmed = await alerts.confirm({
      title: '¿Descartar este borrador?',
      text: `Se removerá «${record.reference}» de esta sesión.`,
      confirmText: 'Descartar',
    })
    if (confirmed) {
      setRecords((current) => current.filter((item) => item.id !== record.id))
      await alerts.success('Borrador descartado')
    }
  }

  return (
    <section aria-labelledby="operational-title">
      <div className="flex flex-col justify-between gap-4 md:flex-row md:items-end">
        <div>
          <p className="text-xs font-bold tracking-[0.14em] text-brand-700 uppercase">
            {config.eyebrow}
          </p>
          <h1 id="operational-title" className="mt-2 text-2xl font-extrabold text-ink-strong">
            {config.title}
          </h1>
          <p className="mt-2 max-w-3xl text-sm leading-6 text-ink-muted">{config.description}</p>
        </div>
        {config.canCreate && (
          <button
            type="button"
            onClick={openCreate}
            className="min-h-11 rounded-xl bg-brand-700 px-5 font-bold text-white shadow-sm"
          >
            + {config.actionLabel}
          </button>
        )}
      </div>

      <div className="mt-6 rounded-card bg-white p-5 shadow-card">
        <div className="grid gap-3 md:grid-cols-[1fr_14rem]">
          <label className="text-sm font-bold text-ink-body">
            Buscar
            <input
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder={`Buscar por ${config.referenceLabel.toLowerCase()} o detalle`}
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
            />
          </label>
          <label className="text-sm font-bold text-ink-body">
            Estado
            <select
              value={status}
              onChange={(event) => setStatus(event.target.value)}
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
            >
              <option value="">Todos</option>
              {config.statuses.map((item) => (
                <option key={item}>{item}</option>
              ))}
            </select>
          </label>
        </div>

        <div className="mt-5 overflow-x-auto">
          <table className="w-full min-w-[720px] text-left text-sm">
            <thead>
              <tr className="border-b border-slate-200 text-xs tracking-wide text-ink-muted uppercase">
                <th className="px-3 py-3">{config.referenceLabel}</th>
                <th className="px-3 py-3">{config.detailLabel}</th>
                <th className="px-3 py-3">Estado</th>
                <th className="px-3 py-3">Actualización</th>
                <th className="px-3 py-3 text-right">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {filtered.map((record) => (
                <tr key={record.id} className="border-b border-slate-100">
                  <td className="px-3 py-4 font-bold text-ink-strong">{record.reference}</td>
                  <td className="px-3 py-4 text-ink-body">{record.detail}</td>
                  <td className="px-3 py-4">
                    <span className="text-brand-800 rounded-full bg-brand-50 px-2.5 py-1 text-xs font-bold">
                      {formatStatusLabel(record.status)}
                    </span>
                  </td>
                  <td className="px-3 py-4 text-ink-muted">
                    {new Intl.DateTimeFormat('es-DO', {
                      dateStyle: 'medium',
                      timeStyle: 'short',
                    }).format(new Date(record.updatedAt))}
                  </td>
                  <td className="px-3 py-4 text-right">
                    {config.canCreate ? (
                      <div className="flex justify-end gap-2">
                        <button
                          type="button"
                          onClick={() => openEdit(record)}
                          className="rounded-lg border border-slate-300 px-3 py-2 font-bold"
                        >
                          Editar
                        </button>
                        <button
                          type="button"
                          onClick={() => void remove(record)}
                          className="rounded-lg border border-slate-300 px-3 py-2 font-bold text-red-700"
                        >
                          Descartar
                        </button>
                      </div>
                    ) : (
                      <span className="text-xs text-ink-muted">Solo lectura</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {filtered.length === 0 && (
            <div className="p-10 text-center">
              <p className="font-bold text-ink-strong">No hay registros para mostrar</p>
              <p className="mt-2 text-sm text-ink-muted">
                Use los filtros o{' '}
                {config.canCreate
                  ? `agregue un ${config.actionLabel.toLowerCase()} como borrador.`
                  : 'espere registros autorizados para su ámbito.'}
              </p>
            </div>
          )}
        </div>
      </div>

      {modalOpen && (
        <div
          className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/55 p-4"
          role="dialog"
          aria-modal="true"
          aria-labelledby="operation-modal-title"
        >
          <div className="w-full max-w-xl rounded-card bg-white shadow-2xl">
            <div className="flex justify-between border-b border-slate-200 px-6 py-5">
              <h2 id="operation-modal-title" className="text-xl font-extrabold text-ink-strong">
                {editing ? `Editar ${config.actionLabel}` : config.actionLabel}
              </h2>
              <button
                type="button"
                onClick={() => setModalOpen(false)}
                aria-label="Cerrar formulario"
                className="px-3 text-xl text-slate-500"
              >
                ×
              </button>
            </div>
            <form onSubmit={submit} className="grid gap-4 p-6">
              <label className="text-sm font-bold text-ink-body">
                {config.referenceLabel}
                <input
                  required
                  value={draft.reference}
                  onChange={(event) => setDraft({ ...draft, reference: event.target.value })}
                  className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
                />
              </label>
              <label className="text-sm font-bold text-ink-body">
                {config.detailLabel}
                <textarea
                  required
                  rows={4}
                  value={draft.detail}
                  onChange={(event) => setDraft({ ...draft, detail: event.target.value })}
                  className="mt-1.5 w-full rounded-xl border border-slate-300 p-3 font-normal"
                />
              </label>
              <label className="text-sm font-bold text-ink-body">
                Estado
                <select
                  value={draft.status}
                  onChange={(event) => setDraft({ ...draft, status: event.target.value })}
                  className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
                >
                  {config.statuses.map((item) => (
                    <option key={item}>{item}</option>
                  ))}
                </select>
              </label>
              <div className="flex justify-end gap-3 border-t border-slate-200 pt-5">
                <button
                  type="button"
                  onClick={() => setModalOpen(false)}
                  className="min-h-11 rounded-xl border border-slate-300 px-5 font-bold"
                >
                  Cancelar
                </button>
                <button className="min-h-11 rounded-xl bg-brand-700 px-5 font-bold text-white">
                  Guardar borrador
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </section>
  )
}
