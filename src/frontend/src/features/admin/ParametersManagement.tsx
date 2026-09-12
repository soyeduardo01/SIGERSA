import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { TableSkeleton } from '../../components/feedback/Skeletons'
import {
  activateParameter,
  createParameter,
  deleteParameter,
  getAllParameters,
  updateParameter,
  type ParameterControl,
  type ParameterControlDraft,
} from '../../lib/api'
import { alerts } from '../../lib/alerts'

const emptyDraft: ParameterControlDraft = {
  keyWord: '',
  companyCode: null,
  oCode: null,
  cCode: null,
  numericData: null,
  doubleData: null,
  stringData: null,
  booleanData: null,
  dateData: null,
}

export function ParametersManagement() {
  const [items, setItems] = useState<ParameterControl[]>([])
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [editing, setEditing] = useState<ParameterControl | null>(null)
  const [open, setOpen] = useState(false)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      setItems(await getAllParameters(search))
      setError('')
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'No se pudieron cargar los parámetros.')
    } finally {
      setLoading(false)
    }
  }, [search])

  useEffect(() => {
    queueMicrotask(() => void load())
  }, [load])

  async function save(draft: ParameterControlDraft) {
    try {
      if (editing) await updateParameter(editing.parametersId, draft)
      else await createParameter(draft)
      setOpen(false)
      await load()
      await alerts.success(editing ? 'Parámetro actualizado' : 'Parámetro creado')
    } catch (caught) {
      await alerts.error(caught, 'No se pudo guardar el parámetro')
      throw caught
    }
  }

  async function remove(item: ParameterControl) {
    const confirmed = await alerts.confirm({
      title: '¿Desactivar este parámetro?',
      text: `${item.keyWord}: ${item.stringData ?? item.cCode ?? item.numericData ?? item.parametersId}`,
      confirmText: 'Desactivar',
    })
    if (!confirmed) return
    try {
      await deleteParameter(item.parametersId)
      await load()
      await alerts.success('Parámetro desactivado')
    } catch (caught) {
      await alerts.error(caught, 'No se pudo desactivar el parámetro')
    }
  }

  async function activate(item: ParameterControl) {
    const confirmed = await alerts.confirm({
      title: '¿Activar este parámetro?',
      text: `${item.keyWord}: ${item.stringData ?? item.cCode ?? item.numericData ?? item.parametersId}`,
      confirmText: 'Activar',
    })
    if (!confirmed) return
    try {
      await activateParameter(item.parametersId)
      await load()
      await alerts.success('Parámetro activado')
    } catch (caught) {
      await alerts.error(caught, 'No se pudo activar el parámetro')
    }
  }

  return (
    <section aria-labelledby="parameters-title">
      <div className="flex flex-col justify-between gap-4 md:flex-row md:items-end">
        <div>
          <p className="text-xs font-bold tracking-[0.14em] text-brand-700 uppercase">
            Configuración
          </p>
          <h1 id="parameters-title" className="mt-2 text-2xl font-extrabold text-ink-strong">
            Parámetros y catálogos
          </h1>
          <p className="mt-2 text-sm text-ink-muted">
            Administre los valores globales consumidos por los módulos del sistema.
          </p>
        </div>
        <button
          type="button"
          onClick={() => {
            setEditing(null)
            setOpen(true)
          }}
          className="min-h-11 rounded-xl bg-brand-700 px-5 font-bold text-white"
        >
          + Nuevo parámetro
        </button>
      </div>

      <div className="mt-6 rounded-card bg-white p-5 shadow-card">
        <form
          onSubmit={(event) => {
            event.preventDefault()
            setSearch(searchInput.trim())
          }}
          className="flex flex-col gap-3 md:flex-row"
        >
          <label className="flex-1 text-sm font-bold">
            Buscar
            <input
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder="KeyWord, código o valor"
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
            />
          </label>
          <button className="text-brand-800 mt-auto min-h-11 rounded-xl border border-brand-700 px-5 font-bold">
            Aplicar
          </button>
        </form>
        {error && (
          <p
            role="alert"
            className="mt-4 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800"
          >
            {error}
          </p>
        )}
        <div className="mt-5 overflow-x-auto">
          <table className="w-full min-w-[850px] text-left text-sm">
            <thead>
              <tr className="border-b border-slate-200 text-xs text-ink-muted uppercase">
                <th className="p-3">KeyWord</th>
                <th className="p-3">Código</th>
                <th className="p-3">Valor</th>
                <th className="p-3">Orden</th>
                <th className="p-3">Ámbito</th>
                <th className="p-3">Estado</th>
                <th className="p-3 text-right">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {items.map((item) => (
                <tr key={item.parametersId} className="border-b border-slate-100">
                  <td className="p-3 font-bold">{item.keyWord}</td>
                  <td className="p-3">{item.cCode ?? '—'}</td>
                  <td className="p-3">{displayValue(item)}</td>
                  <td className="p-3">{item.numericData ?? '—'}</td>
                  <td className="p-3">{item.companyCode ?? 'Global'}</td>
                  <td className="p-3">
                    <span
                      className={`rounded-full px-2.5 py-1 text-xs font-bold ${
                        item.status
                          ? 'bg-emerald-100 text-emerald-800'
                          : 'bg-slate-200 text-slate-700'
                      }`}
                    >
                      {item.status ? 'Activo' : 'Inactivo'}
                    </span>
                  </td>
                  <td className="p-3 text-right">
                    <div className="flex justify-end gap-2">
                      <button
                        type="button"
                        onClick={() => {
                          setEditing(item)
                          setOpen(true)
                        }}
                        className="rounded-lg border px-3 py-2 font-bold"
                      >
                        Editar
                      </button>
                      {item.status ? (
                        <button
                          type="button"
                          onClick={() => void remove(item)}
                          className="rounded-lg border border-red-300 px-3 py-2 font-bold text-red-700"
                        >
                          Desactivar
                        </button>
                      ) : (
                        <button
                          type="button"
                          onClick={() => void activate(item)}
                          className="rounded-lg border border-emerald-300 px-3 py-2 font-bold text-emerald-700"
                        >
                          Activar
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {loading && <TableSkeleton rows={5} columns={7} />}
          {!loading && !error && items.length === 0 && (
            <p className="p-10 text-center text-sm text-ink-muted">
              No hay parámetros para mostrar.
            </p>
          )}
        </div>
      </div>
      {open && <ParameterForm item={editing} onClose={() => setOpen(false)} onSave={save} />}
    </section>
  )
}

function displayValue(item: ParameterControl) {
  if (item.stringData !== null) return item.stringData
  if (item.doubleData !== null) return item.doubleData
  if (item.booleanData !== null) return item.booleanData ? 'Sí' : 'No'
  if (item.dateData !== null)
    return new Intl.DateTimeFormat('es-DO').format(new Date(item.dateData))
  return '—'
}

function ParameterForm({
  item,
  onClose,
  onSave,
}: {
  item: ParameterControl | null
  onClose: () => void
  onSave: (draft: ParameterControlDraft) => Promise<void>
}) {
  const [draft, setDraft] = useState<ParameterControlDraft>(() =>
    item
      ? {
          keyWord: item.keyWord,
          companyCode: item.companyCode,
          oCode: item.oCode,
          cCode: item.cCode,
          numericData: item.numericData,
          doubleData: item.doubleData,
          stringData: item.stringData,
          booleanData: item.booleanData,
          dateData: item.dateData,
        }
      : emptyDraft,
  )
  const [saving, setSaving] = useState(false)
  const numeric = (value: string) => (value === '' ? null : Number(value))
  async function submit(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    try {
      await onSave(draft)
    } finally {
      setSaving(false)
    }
  }
  const field = 'mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal'
  return (
    <div
      className="sigersa-modal-overlay fixed inset-0 z-50 grid place-items-center p-4"
      role="presentation"
    >
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby="parameter-form-title"
        className="sigersa-modal-panel w-full max-w-3xl"
      >
        <header className="flex items-start justify-between border-b border-slate-200 px-6 py-5">
          <div>
            <p className="text-xs font-bold tracking-[0.14em] text-brand-700 uppercase">
              ParametersControl
            </p>
            <h2 id="parameter-form-title" className="mt-1 text-xl font-extrabold">
              {item ? 'Editar parámetro' : 'Nuevo parámetro'}
            </h2>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Cerrar"
            className="rounded-xl p-2 text-xl hover:bg-slate-100"
          >
            ×
          </button>
        </header>
        <form onSubmit={(event) => void submit(event)} className="grid gap-4 p-6 md:grid-cols-2">
          <label className="text-sm font-bold md:col-span-2">
            KeyWord
            <input
              required
              maxLength={50}
              value={draft.keyWord}
              onChange={(e) => setDraft({ ...draft, keyWord: e.target.value.toUpperCase() })}
              className={field}
            />
          </label>
          <label className="text-sm font-bold">
            Código C
            <input
              maxLength={9}
              value={draft.cCode ?? ''}
              onChange={(e) => setDraft({ ...draft, cCode: e.target.value || null })}
              className={field}
            />
          </label>
          <label className="text-sm font-bold">
            Código de empresa
            <input
              type="number"
              value={draft.companyCode ?? ''}
              onChange={(e) => setDraft({ ...draft, companyCode: numeric(e.target.value) })}
              className={field}
            />
          </label>
          <label className="text-sm font-bold">
            Orden / valor entero
            <input
              type="number"
              value={draft.numericData ?? ''}
              onChange={(e) => setDraft({ ...draft, numericData: numeric(e.target.value) })}
              className={field}
            />
          </label>
          <label className="text-sm font-bold">
            Código O
            <input
              type="number"
              value={draft.oCode ?? ''}
              onChange={(e) => setDraft({ ...draft, oCode: numeric(e.target.value) })}
              className={field}
            />
          </label>
          <label className="text-sm font-bold md:col-span-2">
            Valor de texto
            <input
              value={draft.stringData ?? ''}
              onChange={(e) => setDraft({ ...draft, stringData: e.target.value || null })}
              className={field}
            />
          </label>
          <label className="text-sm font-bold">
            Valor decimal
            <input
              type="number"
              step="any"
              value={draft.doubleData ?? ''}
              onChange={(e) => setDraft({ ...draft, doubleData: numeric(e.target.value) })}
              className={field}
            />
          </label>
          <label className="text-sm font-bold">
            Valor lógico
            <select
              value={draft.booleanData === null ? '' : String(draft.booleanData)}
              onChange={(e) =>
                setDraft({
                  ...draft,
                  booleanData: e.target.value === '' ? null : e.target.value === 'true',
                })
              }
              className={field}
            >
              <option value="">Sin valor</option>
              <option value="true">Sí</option>
              <option value="false">No</option>
            </select>
          </label>
          <label className="text-sm font-bold md:col-span-2">
            Fecha
            <input
              type="date"
              value={draft.dateData?.slice(0, 10) ?? ''}
              onChange={(e) =>
                setDraft({
                  ...draft,
                  dateData: e.target.value ? `${e.target.value}T00:00:00Z` : null,
                })
              }
              className={field}
            />
          </label>
          <div className="flex justify-end gap-3 border-t border-slate-200 pt-5 md:col-span-2">
            <button
              type="button"
              onClick={onClose}
              className="min-h-11 rounded-xl border px-5 font-bold"
            >
              Cancelar
            </button>
            <button
              disabled={saving}
              className="min-h-11 rounded-xl bg-brand-700 px-5 font-bold text-white disabled:opacity-60"
            >
              {saving ? 'Guardando…' : 'Guardar'}
            </button>
          </div>
        </form>
      </section>
    </div>
  )
}
