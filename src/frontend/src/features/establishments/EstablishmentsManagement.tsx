import { useEffect, useState, type FormEvent } from 'react'
import { TableSkeleton } from '../../components/feedback/Skeletons'
import { RiskBadge } from '../../components/ui/RiskBadge'
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
  const [page, setPage] = useState(1)
  const [riskDetail, setRiskDetail] = useState<EstablishmentsPage['items'][number] | null>(null)

  async function load() {
    setLoading(true)
    try {
      setResult(await getEstablishments({ search: query, status, page, pageSize: 10 }))
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
    queueMicrotask(() => active && setLoading(true))
    void getEstablishments({ search: query, status, page, pageSize: 10 })
      .then((records) => {
        if (!active) return
        setResult(records)
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
  }, [page, query, status])

  useEffect(() => {
    let active = true
    void getEstablishmentOptions()
      .then((catalogs) => {
        if (active) setOptions(catalogs)
      })
      .catch((caught) => {
        if (active) void alerts.error(caught, 'No se pudieron cargar los catálogos')
      })
    return () => {
      active = false
    }
  }, [])

  function searchRecords(event: FormEvent) {
    event.preventDefault()
    setPage(1)
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
                <th className="px-3 py-3">Riesgo actual</th>
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
                  <td className="px-3 py-4">
                    {item.riskLevel ? (
                      <button
                        type="button"
                        className="rounded-full focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-brand-700"
                        onClick={() => setRiskDetail(item)}
                        aria-label={`Ver cálculo de riesgo de ${item.name}`}
                      >
                        <RiskBadge
                          level={
                            item.riskLevel === 'BAJO'
                              ? 'Bajo'
                              : item.riskLevel === 'MEDIO'
                                ? 'Medio'
                                : 'Alto'
                          }
                        />
                      </button>
                    ) : (
                      <span className="text-xs text-ink-muted">Pendiente de evaluación</span>
                    )}
                  </td>
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
          {loading && <TableSkeleton rows={5} columns={8} />}
          {!loading && !error && result.items.length === 0 && (
            <p className="p-10 text-center text-sm text-ink-muted">
              No hay establecimientos registrados con estos filtros.
            </p>
          )}
        </div>
        <Pagination
          page={page}
          pageSize={result.pageSize}
          total={result.total}
          disabled={loading}
          onChange={setPage}
        />
      </div>

      {riskDetail && (
        <EstablishmentRiskDialog item={riskDetail} onClose={() => setRiskDetail(null)} />
      )}

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

function Pagination({
  page,
  pageSize,
  total,
  disabled,
  onChange,
}: {
  page: number
  pageSize: number
  total: number
  disabled: boolean
  onChange: (page: number) => void
}) {
  const pages = Math.max(1, Math.ceil(total / pageSize))
  return (
    <nav
      aria-label="Paginación de establecimientos"
      className="mt-5 flex items-center justify-between border-t border-slate-100 pt-4 text-sm"
    >
      <span className="text-ink-muted">
        {total} registros · Página {page} de {pages}
      </span>
      <div className="flex gap-2">
        <button
          type="button"
          disabled={disabled || page <= 1}
          onClick={() => onChange(page - 1)}
          className="rounded-lg border px-3 py-2 font-bold disabled:opacity-40"
        >
          Anterior
        </button>
        <button
          type="button"
          disabled={disabled || page >= pages}
          onClick={() => onChange(page + 1)}
          className="rounded-lg border px-3 py-2 font-bold disabled:opacity-40"
        >
          Siguiente
        </button>
      </div>
    </nav>
  )
}

function EstablishmentRiskDialog({
  item,
  onClose,
}: {
  item: EstablishmentsPage['items'][number]
  onClose: () => void
}) {
  const factors = [
    ['Volumen de producción', 16, item.productionScore],
    ['Implementación del sistema HACCP', 9, item.haccpScore],
    ['Cumplimiento con las BPM', 56, item.bpmScore],
    ['Proveedor INABIE', 5, item.inabieScore],
    ['Rechazos microbiológicos', 6, item.rejectionScore],
    ['Plan de muestreo', 8, item.samplingScore],
  ] as const
  return (
    <div
      role="presentation"
      className="sigersa-modal-overlay fixed inset-0 z-50 grid place-items-center p-4"
      onMouseDown={(event) => event.target === event.currentTarget && onClose()}
    >
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby="risk-detail-title"
        className="sigersa-modal-panel max-h-[90vh] w-full max-w-4xl overflow-y-auto p-6"
      >
        <div className="flex items-start justify-between gap-4">
          <div>
            <p className="text-xs font-bold tracking-widest text-brand-700 uppercase">
              Cálculo auditable
            </p>
            <h2 id="risk-detail-title" className="mt-1 text-xl font-extrabold">
              Riesgo de {item.name}
            </h2>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Cerrar detalle"
            className="rounded-full border px-3 py-2"
          >
            ✕
          </button>
        </div>
        <div className="mt-5 grid gap-3 sm:grid-cols-4">
          <RiskValue label="Producto" value={item.productRisk} />
          <RiskValue label="Establecimiento" value={item.establishmentRisk} />
          <RiskValue label="Riesgo total" value={item.totalRisk} />
          <RiskValue
            label="Frecuencia"
            text={item.frequency ? formatStatusLabel(item.frequency) : 'No aplica'}
          />
        </div>
        <p className="mt-5 rounded-xl bg-brand-50 p-4 text-sm font-bold text-brand-900">
          Riesgo total = riesgo microbiológico del producto × riesgo del establecimiento
        </p>
        <div className="mt-5 overflow-x-auto">
          <table className="w-full min-w-[620px] text-left text-sm">
            <thead>
              <tr className="border-b text-xs text-ink-muted uppercase">
                <th className="px-3 py-3">Factor</th>
                <th className="px-3 py-3">Puntaje</th>
                <th className="px-3 py-3">Peso</th>
                <th className="px-3 py-3">Valor ponderado</th>
              </tr>
            </thead>
            <tbody>
              {factors.map(([name, weight, score]) => (
                <tr key={name} className="border-b border-slate-100">
                  <td className="px-3 py-3 font-semibold">{name}</td>
                  <td className="px-3 py-3">{score?.toFixed(2) ?? 'N/D'}</td>
                  <td className="px-3 py-3">{weight}%</td>
                  <td className="px-3 py-3 font-bold">
                    {score === null ? 'N/D' : ((score * weight) / 100).toFixed(2)}
                  </td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr className="bg-slate-50 font-extrabold">
                <td className="px-3 py-3" colSpan={3}>
                  Nivel de riesgo del establecimiento
                </td>
                <td className="px-3 py-3">{item.establishmentRisk?.toFixed(2) ?? 'N/D'}</td>
              </tr>
            </tfoot>
          </table>
        </div>
        <p className="mt-4 text-xs text-ink-muted">
          La categoría o subcategoría alimentaria aporta el riesgo microbiológico: bajo = 1, medio =
          2 y alto = 3.
        </p>
      </section>
    </div>
  )
}

function RiskValue({
  label,
  value,
  text,
}: {
  label: string
  value?: number | null
  text?: string
}) {
  return (
    <div className="rounded-xl border border-slate-200 p-3">
      <span className="block text-xs font-bold text-ink-muted uppercase">{label}</span>
      <strong className="mt-1 block text-lg">{text ?? value?.toFixed(2) ?? 'N/D'}</strong>
    </div>
  )
}
