import { useEffect, useState, type FormEvent } from 'react'
import { TableSkeleton } from '../../components/feedback/Skeletons'
import {
  createEstablishment,
  getEstablishment,
  getEstablishmentOptions,
  getEstablishments,
  updateEstablishment,
  type EstablishmentDetails,
  type EstablishmentDraft,
  type EstablishmentOptions,
  type EstablishmentsPage,
} from '../../lib/api'
import { alerts } from '../../lib/alerts'
import { formatStatusLabel } from '../../lib/formatters'
import { EstablishmentFormModal } from '../operations/EstablishmentFormModal'

const emptyPage: EstablishmentsPage = { items: [], page: 1, pageSize: 10, total: 0 }

export function EstablishmentsManagement() {
  const [result, setResult] = useState(emptyPage)
  const [options, setOptions] = useState<EstablishmentOptions | null>(null)
  const [search, setSearch] = useState('')
  const [query, setQuery] = useState('')
  const [status, setStatus] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<EstablishmentDetails | null>(null)
  const [saving, setSaving] = useState(false)

  async function load() {
    setLoading(true)
    try {
      setResult(await getEstablishments({ search: query, status }))
      setError('')
    } catch (caught) {
      setError(
        caught instanceof Error ? caught.message : 'No se pudieron cargar los establecimientos.',
      )
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    let active = true
    void Promise.all([getEstablishments({ search: query, status }), getEstablishmentOptions()])
      .then(([page, catalogs]) => {
        if (!active) return
        setResult(page)
        setOptions(catalogs)
        setError('')
      })
      .catch((caught) => {
        if (active)
          setError(
            caught instanceof Error
              ? caught.message
              : 'No se pudieron cargar los establecimientos.',
          )
      })
      .finally(() => active && setLoading(false))
    return () => {
      active = false
    }
  }, [query, status])

  function searchRecords(event: FormEvent) {
    event.preventDefault()
    setQuery(search.trim())
  }

  async function edit(id: string) {
    try {
      const value = await getEstablishment(id)
      setEditing(value)
      setModalOpen(true)
    } catch (caught) {
      await alerts.error(caught, 'No se pudo abrir el establecimiento')
    }
  }

  async function save(draft: EstablishmentDraft) {
    setSaving(true)
    try {
      if (editing) {
        await updateEstablishment(editing.id, draft)
        await alerts.success('Establecimiento actualizado', 'Los cambios fueron guardados.')
      } else {
        await createEstablishment(draft)
        await alerts.success(
          'Establecimiento registrado',
          'El registro quedó disponible para inspecciones.',
        )
      }
      setModalOpen(false)
      setEditing(null)
      await load()
    } catch (caught) {
      await alerts.error(caught, 'No se pudo guardar el establecimiento')
      throw caught
    } finally {
      setSaving(false)
    }
  }

  return (
    <section aria-labelledby="establishments-title">
      <div className="flex flex-col justify-between gap-4 md:flex-row md:items-end">
        <div>
          <p className="text-xs font-bold tracking-[0.14em] text-brand-700 uppercase">
            Registro sanitario
          </p>
          <h1 id="establishments-title" className="mt-2 text-2xl font-extrabold text-ink-strong">
            Gestión de establecimientos
          </h1>
          <p className="mt-2 text-sm text-ink-muted">
            Registre y actualice establecimientos con los catálogos oficiales del sistema.
          </p>
        </div>
        <button
          type="button"
          disabled={!options}
          onClick={() => {
            setEditing(null)
            setModalOpen(true)
          }}
          className="min-h-11 rounded-xl bg-brand-700 px-5 font-bold text-white disabled:opacity-50"
        >
          + Nuevo establecimiento
        </button>
      </div>

      <div className="mt-6 rounded-card bg-white p-5 shadow-card">
        <form
          onSubmit={searchRecords}
          className="grid gap-3 md:grid-cols-[minmax(0,1fr)_16rem_auto]"
        >
          <label className="text-sm font-bold text-ink-body">
            Buscar dentro de establecimientos
            <input
              value={search}
              onChange={(event) => setSearch(event.target.value)}
              placeholder="Código, establecimiento o empresa"
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-200 px-3 font-normal"
            />
          </label>
          <label className="text-sm font-bold text-ink-body">
            Estado
            <select
              value={status}
              onChange={(event) => setStatus(event.target.value)}
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-200 px-3 font-normal"
            >
              <option value="">Todos</option>
              {options?.statuses.map((value) => (
                <option key={value.parametersId} value={value.stringData ?? ''}>
                  {formatStatusLabel(value.stringData ?? '')}
                </option>
              ))}
            </select>
          </label>
          <button className="text-brand-800 min-h-11 self-end rounded-xl border border-brand-700 px-5 text-sm font-bold">
            Buscar
          </button>
        </form>

        {options && options.statuses.length === 0 && (
          <p className="mt-3 text-sm text-amber-700">
            El catálogo ESTADO_ESTABLECIMIENTO no tiene valores activos.
          </p>
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
                <th className="px-3 py-3">Código</th>
                <th className="px-3 py-3">Establecimiento</th>
                <th className="px-3 py-3">Empresa</th>
                <th className="px-3 py-3">Ubicación</th>
                <th className="px-3 py-3">Contacto</th>
                <th className="px-3 py-3">Estado</th>
                <th className="px-3 py-3 text-right">Acción</th>
              </tr>
            </thead>
            <tbody>
              {result.items.map((item) => (
                <tr key={item.id} className="border-b border-slate-100">
                  <td className="px-3 py-4 font-bold">{item.code}</td>
                  <td className="px-3 py-4 font-semibold text-ink-strong">{item.name}</td>
                  <td className="px-3 py-4">{item.companyName}</td>
                  <td className="px-3 py-4">
                    {[item.municipalityName, item.provinceName].filter(Boolean).join(', ') ||
                      'No registrada'}
                  </td>
                  <td className="px-3 py-4">{item.phone || item.email || 'No registrado'}</td>
                  <td className="px-3 py-4">{formatStatusLabel(item.status)}</td>
                  <td className="px-3 py-4 text-right">
                    <button
                      type="button"
                      onClick={() => void edit(item.id)}
                      className="text-brand-800 rounded-lg border border-slate-200 px-3 py-2 font-bold"
                    >
                      Editar
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {loading && <TableSkeleton rows={5} columns={7} />}
          {!loading && !error && result.items.length === 0 && (
            <p className="p-10 text-center text-sm text-ink-muted">
              No hay establecimientos registrados con estos filtros.
            </p>
          )}
        </div>
      </div>

      {modalOpen && options && (
        <EstablishmentFormModal
          open
          options={options}
          initial={editing}
          saving={saving}
          onClose={() => {
            setModalOpen(false)
            setEditing(null)
          }}
          onSave={save}
        />
      )}
    </section>
  )
}
