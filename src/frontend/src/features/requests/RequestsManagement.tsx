import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { TableSkeleton } from '../../components/feedback/Skeletons'
import {
  createInspectionRequest,
  getInspectionRequestOptions,
  getInspectionRequests,
  transitionInspectionRequest,
  updateInspectionRequest,
  uploadInspectionRequestDocument,
  type InspectionRequest,
  type InspectionRequestDraft,
  type InspectionRequestOptions,
  type InspectionRequestsPage,
} from '../../lib/api'
import { alerts } from '../../lib/alerts'
import { queueRequest } from '../../offline/syncQueue'

const emptyPage: InspectionRequestsPage = { items: [], page: 1, pageSize: 10, total: 0 }
const statusLabels: Record<InspectionRequest['status'], string> = {
  BORRADOR: 'Borrador',
  PENDIENTE_ASIGNACION: 'Pendiente de asignación',
  CANCELADA: 'Cancelada',
  RECHAZADA: 'Rechazada',
}

export function RequestsManagement() {
  const [result, setResult] = useState(emptyPage)
  const [options, setOptions] = useState<InspectionRequestOptions | null>(null)
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [editing, setEditing] = useState<InspectionRequest | null>(null)
  const [modalOpen, setModalOpen] = useState(false)

  useEffect(() => {
    let active = true
    void getInspectionRequestOptions()
      .then((value) => active && setOptions(value))
      .catch(async (caught) => {
        if (!active) return
        setError(caught instanceof Error ? caught.message : 'No se pudieron cargar las opciones.')
        await alerts.error(caught, 'No se pudo preparar Solicitudes')
      })
    return () => {
      active = false
    }
  }, [])

  useEffect(() => {
    let active = true
    void getInspectionRequests({ search, status, page, pageSize: 10 })
      .then((value) => {
        if (!active) return
        setResult(value)
        setError('')
      })
      .catch((caught) => {
        if (active)
          setError(
            caught instanceof Error ? caught.message : 'No se pudieron cargar las solicitudes.',
          )
      })
      .finally(() => active && setLoading(false))
    return () => {
      active = false
    }
  }, [page, search, status])

  async function reload() {
    setLoading(true)
    try {
      setResult(await getInspectionRequests({ search, status, page, pageSize: 10 }))
      setError('')
    } finally {
      setLoading(false)
    }
  }

  function applySearch(event: FormEvent) {
    event.preventDefault()
    setLoading(true)
    setPage(1)
    setSearch(searchInput.trim())
  }

  async function save(draft: InspectionRequestDraft, supportingDocument: File | null) {
    try {
      if (editing) {
        await updateInspectionRequest(editing.id, draft)
        if (supportingDocument)
          await uploadInspectionRequestDocument(editing.id, supportingDocument)
        await reload()
        await alerts.success('Solicitud actualizada')
      } else if (navigator.onLine) {
        const created = await createInspectionRequest(draft)
        if (supportingDocument)
          await uploadInspectionRequestDocument(created.id, supportingDocument)
        await reload()
        await alerts.success('Solicitud creada', 'El borrador quedó guardado en SIGERSA.')
      } else {
        await queueRequest({ ...draft, rowVersion: null })
        await alerts.success(
          'Solicitud guardada sin conexión',
          'Se enviará automáticamente cuando se restablezca la conexión.',
        )
      }
      setModalOpen(false)
    } catch (caught) {
      await alerts.error(caught, 'No se pudo guardar la solicitud')
      throw caught
    }
  }

  async function transition(item: InspectionRequest, action: 'submit' | 'cancel') {
    const confirmed = await alerts.confirm({
      title: action === 'submit' ? '¿Enviar esta solicitud?' : '¿Cancelar esta solicitud?',
      text:
        action === 'submit'
          ? 'Después de enviarla no podrá modificar el borrador.'
          : 'La cancelación quedará registrada y no puede deshacerse desde esta pantalla.',
      confirmText: action === 'submit' ? 'Enviar solicitud' : 'Cancelar solicitud',
    })
    if (!confirmed) return
    try {
      await transitionInspectionRequest(item.id, action, item.rowVersion)
      await reload()
      await alerts.success(action === 'submit' ? 'Solicitud enviada' : 'Solicitud cancelada')
    } catch (caught) {
      await alerts.error(caught, 'No se pudo cambiar el estado')
    }
  }

  const pages = Math.max(1, Math.ceil(result.total / result.pageSize))

  return (
    <section aria-labelledby="requests-title">
      <div className="flex flex-col justify-between gap-4 md:flex-row md:items-end">
        <div>
          <p className="text-xs font-bold tracking-[0.14em] text-brand-700 uppercase">
            Origen del proceso
          </p>
          <h1 id="requests-title" className="mt-2 text-2xl font-extrabold text-ink-strong">
            Solicitudes
          </h1>
          <p className="mt-2 text-sm text-ink-muted">
            Registre y dé seguimiento a solicitudes dentro del ámbito autorizado de su sesión.
          </p>
        </div>
        {options?.canManage && (
          <button
            type="button"
            onClick={() => {
              setEditing(null)
              setModalOpen(true)
            }}
            className="hover:bg-brand-800 min-h-11 rounded-xl bg-brand-700 px-5 font-bold text-white shadow-sm"
          >
            + Nueva solicitud
          </button>
        )}
      </div>

      <div className="mt-6 rounded-card bg-white p-5 shadow-card">
        <form onSubmit={applySearch} className="grid gap-3 md:grid-cols-[1fr_14rem_auto]">
          <label className="text-sm font-bold text-ink-body">
            Buscar
            <input
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder="Número, empresa, establecimiento o motivo"
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
            />
          </label>
          <label className="text-sm font-bold text-ink-body">
            Estado
            <select
              value={status}
              onChange={(event) => {
                setStatus(event.target.value)
                setPage(1)
              }}
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
            >
              <option value="">Todos</option>
              {Object.entries(statusLabels).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
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
          <table className="w-full min-w-[950px] border-collapse text-left text-sm">
            <thead>
              <tr className="border-b border-slate-200 text-xs tracking-wide text-ink-muted uppercase">
                <th className="px-3 py-3">Solicitud</th>
                <th className="px-3 py-3">Empresa / establecimiento</th>
                <th className="px-3 py-3">Motivo</th>
                <th className="px-3 py-3">Solicitante</th>
                <th className="px-3 py-3">Estado</th>
                <th className="px-3 py-3 text-right">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {result.items.map((item) => (
                <tr key={item.id} className="border-b border-slate-100 align-top">
                  <td className="px-3 py-4 font-bold text-ink-strong">
                    {item.number ?? 'Borrador sin número'}
                    <span className="mt-1 block text-xs font-normal text-ink-muted">
                      {new Date(item.createdAt).toLocaleDateString()}
                    </span>
                  </td>
                  <td className="px-3 py-4 text-ink-body">
                    {item.companyName}
                    <span className="mt-1 block text-xs text-ink-muted">
                      {item.establishmentName ?? 'Sin establecimiento'}
                    </span>
                  </td>
                  <td className="px-3 py-4 text-ink-body">{item.inspectionReasonName}</td>
                  <td className="px-3 py-4 text-ink-body">
                    {item.applicantName}
                    <span className="mt-1 block text-xs text-ink-muted">
                      {item.documentCount} documento(s)
                    </span>
                  </td>
                  <td className="px-3 py-4">
                    <span className="rounded-full bg-slate-100 px-2.5 py-1 text-xs font-bold text-ink-body">
                      {statusLabels[item.status]}
                    </span>
                  </td>
                  <td className="px-3 py-4 text-right">
                    {options?.canManage ? (
                      <div className="flex justify-end gap-2">
                        {item.status === 'BORRADOR' && (
                          <>
                            <button
                              type="button"
                              onClick={() => {
                                setEditing(item)
                                setModalOpen(true)
                              }}
                              className="rounded-lg border border-slate-300 px-3 py-2 font-bold"
                            >
                              Editar
                            </button>
                            <button
                              type="button"
                              onClick={() => void transition(item, 'submit')}
                              disabled={item.documentCount === 0}
                              title={
                                item.documentCount === 0
                                  ? 'Adjunte la documentación obligatoria antes de enviar.'
                                  : undefined
                              }
                              className="text-brand-800 rounded-lg border border-brand-700 px-3 py-2 font-bold disabled:cursor-not-allowed disabled:opacity-40"
                            >
                              Enviar
                            </button>
                          </>
                        )}
                        {(item.status === 'BORRADOR' ||
                          item.status === 'PENDIENTE_ASIGNACION') && (
                          <button
                            type="button"
                            onClick={() => void transition(item, 'cancel')}
                            className="rounded-lg border border-red-300 px-3 py-2 font-bold text-red-700"
                          >
                            Cancelar
                          </button>
                        )}
                      </div>
                    ) : (
                      <span className="text-xs text-ink-muted">Solo lectura</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {loading && <TableSkeleton rows={5} columns={6} />}
          {!loading && !error && result.items.length === 0 && (
            <p className="p-10 text-center text-sm text-ink-muted">
              No hay solicitudes para mostrar.
            </p>
          )}
        </div>

        <div className="mt-5 flex items-center justify-between border-t border-slate-200 pt-4 text-sm">
          <span className="text-ink-muted">{result.total} solicitud(es)</span>
          <div className="flex items-center gap-3">
            <button
              type="button"
              disabled={page <= 1 || loading}
              onClick={() => setPage((value) => Math.max(1, value - 1))}
              className="rounded-lg border border-slate-300 px-3 py-2 font-bold disabled:opacity-40"
            >
              Anterior
            </button>
            <span>
              Página {page} de {pages}
            </span>
            <button
              type="button"
              disabled={page >= pages || loading}
              onClick={() => setPage((value) => Math.min(pages, value + 1))}
              className="rounded-lg border border-slate-300 px-3 py-2 font-bold disabled:opacity-40"
            >
              Siguiente
            </button>
          </div>
        </div>
      </div>

      {modalOpen && options && (
        <RequestForm
          request={editing}
          options={options}
          onClose={() => setModalOpen(false)}
          onSave={save}
        />
      )}
    </section>
  )
}

function RequestForm({
  request,
  options,
  onClose,
  onSave,
}: {
  request: InspectionRequest | null
  options: InspectionRequestOptions
  onClose: () => void
  onSave: (draft: InspectionRequestDraft, supportingDocument: File | null) => Promise<void>
}) {
  const [companyId, setCompanyId] = useState(request?.companyId ?? options.companies[0]?.id ?? '')
  const [establishmentId, setEstablishmentId] = useState(request?.establishmentId ?? '')
  const [inspectionReasonId, setInspectionReasonId] = useState(request?.inspectionReasonId ?? '')
  const [reasonDetail, setReasonDetail] = useState(request?.reasonDetail ?? '')
  const [establishmentType, setEstablishmentType] = useState(request?.establishmentType ?? '')
  const [observations, setObservations] = useState(request?.observations ?? '')
  const [saving, setSaving] = useState(false)
  const [supportingDocument, setSupportingDocument] = useState<File | null>(null)
  const establishments = useMemo(
    () => options.establishments.filter((item) => item.companyId === companyId),
    [companyId, options.establishments],
  )

  async function submit(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    try {
      await onSave({
        companyId: companyId || null,
        establishmentId: establishmentId || null,
        inspectionReasonId,
        reasonDetail,
        establishmentType,
        observations,
        idempotencyKey: request ? crypto.randomUUID() : crypto.randomUUID(),
        rowVersion: request?.rowVersion ?? null,
      }, supportingDocument)
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
        aria-labelledby="request-form-title"
        className="sigersa-modal-panel w-full max-w-2xl p-6"
      >
        <div className="flex items-start justify-between gap-4">
          <div>
            <h2 id="request-form-title" className="text-xl font-extrabold text-ink-strong">
              {request ? 'Editar solicitud' : 'Nueva solicitud'}
            </h2>
            <p className="mt-1 text-sm text-ink-muted">
              Complete el origen y motivo de la inspección.
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg p-2 text-ink-muted"
            aria-label="Cerrar"
          >
            ✕
          </button>
        </div>
        <form onSubmit={(event) => void submit(event)} className="mt-6 grid gap-4 md:grid-cols-2">
          <label className="text-sm font-bold text-ink-body">
            Empresa
            <select
              required
              value={companyId}
              onChange={(event) => {
                setCompanyId(event.target.value)
                setEstablishmentId('')
              }}
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
            >
              <option value="">Seleccione</option>
              {options.companies.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.name}
                </option>
              ))}
            </select>
          </label>
          <label className="text-sm font-bold text-ink-body">
            Establecimiento
            <select
              value={establishmentId}
              onChange={(event) => setEstablishmentId(event.target.value)}
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
            >
              <option value="">Sin seleccionar</option>
              {establishments.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.name}
                </option>
              ))}
            </select>
          </label>
          <label className="text-sm font-bold text-ink-body">
            Motivo de inspección
            <select
              required
              value={inspectionReasonId}
              onChange={(event) => setInspectionReasonId(event.target.value)}
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
            >
              <option value="">Seleccione</option>
              {options.reasons.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.name}
                </option>
              ))}
            </select>
          </label>
          <label className="text-sm font-bold text-ink-body">
            Tipo de establecimiento
            <input
              value={establishmentType}
              onChange={(event) => setEstablishmentType(event.target.value)}
              maxLength={100}
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
            />
          </label>
          <label className="text-sm font-bold text-ink-body md:col-span-2">
            Detalle del motivo
            <textarea
              value={reasonDetail}
              onChange={(event) => setReasonDetail(event.target.value)}
              maxLength={2000}
              rows={3}
              className="mt-1.5 w-full rounded-xl border border-slate-300 p-3 font-normal"
            />
          </label>
          <label className="text-sm font-bold text-ink-body md:col-span-2">
            Observaciones
            <textarea
              value={observations}
              onChange={(event) => setObservations(event.target.value)}
              maxLength={4000}
              rows={3}
              className="mt-1.5 w-full rounded-xl border border-slate-300 p-3 font-normal"
            />
          </label>
          <label className="text-sm font-bold text-ink-body md:col-span-2">
            Documentación de soporte obligatoria para enviar
            <input
              type="file"
              accept="application/pdf,image/jpeg,image/png"
              onChange={(event) => setSupportingDocument(event.target.files?.[0] ?? null)}
              className="mt-1.5 block w-full rounded-xl border border-slate-300 bg-white p-2 font-normal"
            />
            <span className="mt-1 block text-xs font-normal text-ink-muted">
              Puede guardar el borrador sin archivo, pero deberá adjuntarlo antes de enviarlo. PDF,
              JPG o PNG; máximo 10 MB.
            </span>
          </label>
          <div className="flex justify-end gap-3 border-t border-slate-200 pt-5 md:col-span-2">
            <button
              type="button"
              onClick={onClose}
              className="rounded-xl border border-slate-300 px-4 py-2.5 font-bold"
            >
              Volver
            </button>
            <button
              disabled={saving}
              className="rounded-xl bg-brand-700 px-5 py-2.5 font-bold text-white disabled:opacity-50"
            >
              {saving ? 'Guardando…' : 'Guardar borrador'}
            </button>
          </div>
        </form>
      </section>
    </div>
  )
}
