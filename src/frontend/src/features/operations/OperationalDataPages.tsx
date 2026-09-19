import { useEffect, useState, type FormEvent, type ReactNode } from 'react'
import {
  createSurveillance,
  downloadEvaluationReport,
  generateEvaluationReport,
  getAuditEvents,
  getEvaluationHistory,
  getEvaluationTimeline,
  getFinding,
  getFindings,
  getSurveillance,
  getSurveillanceOptions,
  updateFindingStatus,
  updateSurveillance,
  type AuditEventRecord,
  type FindingDetail,
  type FindingRecord,
  type HistoricalEvaluation,
  type SurveillanceDraft,
  type SurveillanceOptions,
  type SurveillanceRecord,
  type TimelineEvent,
} from '../../lib/api'
import { alerts } from '../../lib/alerts'
import { formatStatusLabel } from '../../lib/formatters'
import { useAuth } from '../../contexts/useAuth'
import { Pagination, useClientPagination } from '../../components/ui/Pagination'

const fieldClass =
  'mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 bg-white px-3 font-normal'
const labelClass = 'text-sm font-bold text-ink-body'

function PageHeader({
  eyebrow,
  title,
  description,
  action,
}: {
  eyebrow: string
  title: string
  description: string
  action?: ReactNode
}) {
  return (
    <div className="flex flex-col justify-between gap-4 md:flex-row md:items-end">
      <div>
        <p className="text-xs font-bold tracking-[0.14em] text-brand-700 uppercase">{eyebrow}</p>
        <h1 className="mt-2 text-2xl font-extrabold text-ink-strong">{title}</h1>
        <p className="mt-2 max-w-3xl text-sm text-ink-muted">{description}</p>
      </div>
      {action}
    </div>
  )
}

function ErrorBox({ message }: { message: string }) {
  return message ? (
    <div
      role="alert"
      className="mt-5 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800"
    >
      {message}
    </div>
  ) : null
}

function dateTimeLocal(value?: string) {
  const date = value ? new Date(value) : new Date()
  const offset = date.getTimezoneOffset() * 60_000
  return new Date(date.getTime() - offset).toISOString().slice(0, 16)
}

export function SurveillanceManagement() {
  const [items, setItems] = useState<SurveillanceRecord[]>([])
  const [options, setOptions] = useState<SurveillanceOptions | null>(null)
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [kind, setKind] = useState('')
  const [error, setError] = useState('')
  const [editing, setEditing] = useState<SurveillanceRecord | null>(null)
  const [modalOpen, setModalOpen] = useState(false)
  const pagination = useClientPagination(items)

  async function load() {
    try {
      const result = await getSurveillance({ search, kind, pageSize: 100 })
      setItems(result.items)
      setError('')
    } catch (caught) {
      setError(
        caught instanceof Error ? caught.message : 'No se pudo cargar la vigilancia sanitaria.',
      )
    }
  }

  useEffect(() => {
    let active = true
    void getSurveillance({ search, kind, pageSize: 100 })
      .then((result) => {
        if (active) {
          setItems(result.items)
          setError('')
        }
      })
      .catch((caught) => {
        if (active)
          setError(
            caught instanceof Error ? caught.message : 'No se pudo cargar la vigilancia sanitaria.',
          )
      })
    return () => {
      active = false
    }
  }, [search, kind])
  useEffect(() => {
    void getSurveillanceOptions()
      .then(setOptions)
      .catch((caught) =>
        setError(caught instanceof Error ? caught.message : 'No se pudieron cargar las opciones.'),
      )
  }, [])

  return (
    <section aria-labelledby="surveillance-title">
      <PageHeader
        eyebrow="Vigilancia sanitaria"
        title="Alertas LAPCH y denuncias"
        description="Registre, evalúe y dé seguimiento a cada alerta o denuncia hasta su decisión."
        action={
          options?.canManage && (
            <button
              type="button"
              onClick={() => {
                setEditing(null)
                setModalOpen(true)
              }}
              className="min-h-11 rounded-xl bg-brand-700 px-5 font-bold text-white"
            >
              + Nuevo registro
            </button>
          )
        }
      />
      <div className="mt-6 rounded-card bg-white p-5 shadow-card">
        <form
          onSubmit={(event) => {
            event.preventDefault()
            setSearch(searchInput.trim())
          }}
          className="grid gap-3 md:grid-cols-[1fr_14rem_auto]"
        >
          <label className={labelClass}>
            Buscar
            <input
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder="Número, producto, tipo o establecimiento"
              className={fieldClass}
            />
          </label>
          <label className={labelClass}>
            Tipo
            <select
              value={kind}
              onChange={(event) => setKind(event.target.value)}
              className={fieldClass}
            >
              <option value="">Todos</option>
              <option value="ALERTA_LAPCH">Alerta LAPCH</option>
              <option value="DENUNCIA">Denuncia</option>
            </select>
          </label>
          <button className="text-brand-800 mt-auto min-h-11 rounded-xl border border-brand-700 px-5 font-bold">
            Aplicar
          </button>
        </form>
        <ErrorBox message={error} />
        <div className="mt-5 overflow-x-auto">
          <table className="w-full min-w-[950px] text-left text-sm">
            <thead>
              <tr className="border-b text-xs text-ink-muted uppercase">
                <th className="px-3 py-3">Registro</th>
                <th className="px-3 py-3">Empresa / establecimiento</th>
                <th className="px-3 py-3">Asunto</th>
                <th className="px-3 py-3">Prioridad</th>
                <th className="px-3 py-3">Resultado</th>
                <th className="px-3 py-3 text-right">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {pagination.visibleItems.map((item) => (
                <tr key={`${item.kind}-${item.id}`} className="border-b border-slate-100">
                  <td className="px-3 py-4">
                    <strong>{item.number}</strong>
                    <span className="block text-xs text-ink-muted">
                      {item.kind === 'ALERTA_LAPCH' ? 'Alerta LAPCH' : 'Denuncia'} ·{' '}
                      {new Intl.DateTimeFormat('es-DO').format(new Date(item.occurredAt))}
                    </span>
                  </td>
                  <td className="px-3 py-4">
                    {item.companyName ?? 'Sin empresa'}
                    <span className="block text-xs text-ink-muted">
                      {item.establishmentName ?? 'Sin establecimiento'}
                    </span>
                  </td>
                  <td className="px-3 py-4">
                    <strong>{item.subject}</strong>
                    <span className="block max-w-md truncate text-xs text-ink-muted">
                      {item.description}
                    </span>
                  </td>
                  <td className="px-3 py-4">{item.priority}</td>
                  <td className="px-3 py-4">
                    {item.result ? formatStatusLabel(item.result) : 'Pendiente'}
                    {item.hasCase && (
                      <span className="text-brand-800 ml-2 rounded-full bg-brand-50 px-2 py-1 text-xs font-bold">
                        Con caso
                      </span>
                    )}
                  </td>
                  <td className="px-3 py-4 text-right">
                    {options?.canManage && (
                      <button
                        type="button"
                        onClick={() => {
                          setEditing(item)
                          setModalOpen(true)
                        }}
                        className="rounded-lg border px-3 py-2 font-bold"
                      >
                        Editar / decidir
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {items.length === 0 && !error && (
            <p className="p-10 text-center text-sm text-ink-muted">
              No hay alertas ni denuncias registradas.
            </p>
          )}
        </div>
        <Pagination
          page={pagination.page}
          pageSize={pagination.pageSize}
          total={items.length}
          label="alertas y denuncias"
          onChange={pagination.setPage}
        />
      </div>
      {modalOpen && options && (
        <SurveillanceForm
          item={editing}
          options={options}
          onClose={() => setModalOpen(false)}
          onSave={async (draft) => {
            try {
              if (editing) await updateSurveillance(editing.id, draft)
              else await createSurveillance(draft)
              setModalOpen(false)
              await load()
              await alerts.success(editing ? 'Registro actualizado' : 'Registro creado')
            } catch (caught) {
              await alerts.error(caught, 'No se pudo guardar el registro')
              throw caught
            }
          }}
        />
      )}
    </section>
  )
}

function SurveillanceForm({
  item,
  options,
  onClose,
  onSave,
}: {
  item: SurveillanceRecord | null
  options: SurveillanceOptions
  onClose: () => void
  onSave: (draft: SurveillanceDraft) => Promise<void>
}) {
  const [kind, setKind] = useState<'ALERTA_LAPCH' | 'DENUNCIA'>(item?.kind ?? 'ALERTA_LAPCH')
  const [occurredAt, setOccurredAt] = useState(dateTimeLocal(item?.occurredAt))
  const [companyId, setCompanyId] = useState(item?.companyId ?? '')
  const [establishmentId, setEstablishmentId] = useState(item?.establishmentId ?? '')
  const [subject, setSubject] = useState(item?.subject ?? '')
  const [description, setDescription] = useState(item?.description ?? '')
  const [priority, setPriority] = useState(item?.priority ?? 3)
  const [channel, setChannel] = useState(item?.channel ?? '')
  const [isAnonymous, setAnonymous] = useState(item?.isAnonymous ?? false)
  const [isConfidential, setConfidential] = useState(item?.isConfidential ?? true)
  const [result, setResult] = useState(item?.result ?? '')
  const [saving, setSaving] = useState(false)
  const establishments = options.establishments.filter(
    (option) => !companyId || option.companyId === companyId,
  )
  const results =
    kind === 'ALERTA_LAPCH'
      ? ['PROCEDE_EVALUACION', 'NO_PROCEDE', 'REQUIERE_INFORMACION']
      : ['PROCEDE', 'NO_PROCEDE', 'REMITIDA_OTRO_PROCESO', 'REQUIERE_INFORMACION']
  async function submit(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    try {
      await onSave({
        kind,
        occurredAt: new Date(occurredAt).toISOString(),
        companyId: companyId || null,
        establishmentId: establishmentId || null,
        subject,
        description,
        priority,
        channel: kind === 'DENUNCIA' ? channel : null,
        isAnonymous: kind === 'DENUNCIA' && isAnonymous,
        isConfidential: kind === 'DENUNCIA' && isConfidential,
        result: result || null,
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
        aria-labelledby="surveillance-form-title"
        className="sigersa-modal-panel max-h-[92vh] w-full max-w-3xl overflow-y-auto p-6"
      >
        <div className="flex justify-between">
          <div>
            <h2 id="surveillance-form-title" className="text-xl font-extrabold">
              {item ? 'Actualizar vigilancia' : 'Nuevo registro de vigilancia'}
            </h2>
            <p className="mt-1 text-sm text-ink-muted">
              Los datos se guardan directamente en el expediente sanitario.
            </p>
          </div>
          <button type="button" onClick={onClose} aria-label="Cerrar" className="p-2">
            ✕
          </button>
        </div>
        <form onSubmit={(event) => void submit(event)} className="mt-6 grid gap-4 md:grid-cols-2">
          <label className={labelClass}>
            Tipo
            <select
              disabled={Boolean(item)}
              value={kind}
              onChange={(event) => {
                setKind(event.target.value as typeof kind)
                setResult('')
              }}
              className={fieldClass}
            >
              <option value="ALERTA_LAPCH">Alerta LAPCH</option>
              <option value="DENUNCIA">Denuncia</option>
            </select>
          </label>
          <label className={labelClass}>
            Fecha y hora
            <input
              required
              type="datetime-local"
              value={occurredAt}
              onChange={(event) => setOccurredAt(event.target.value)}
              className={fieldClass}
            />
          </label>
          <label className={labelClass}>
            Empresa
            <select
              required={kind === 'ALERTA_LAPCH'}
              value={companyId}
              onChange={(event) => {
                setCompanyId(event.target.value)
                setEstablishmentId('')
              }}
              className={fieldClass}
            >
              <option value="">Seleccione</option>
              {options.companies.map((option) => (
                <option key={option.id} value={option.id}>
                  {option.name}
                </option>
              ))}
            </select>
          </label>
          <label className={labelClass}>
            Establecimiento
            <select
              required={kind === 'DENUNCIA'}
              value={establishmentId}
              onChange={(event) => {
                const selected = options.establishments.find(
                  (option) => option.id === event.target.value,
                )
                setEstablishmentId(event.target.value)
                if (selected?.companyId) setCompanyId(selected.companyId)
              }}
              className={fieldClass}
            >
              <option value="">{kind === 'DENUNCIA' ? 'Seleccione' : 'Opcional'}</option>
              {establishments.map((option) => (
                <option key={option.id} value={option.id}>
                  {option.name}
                </option>
              ))}
            </select>
          </label>
          <label className={`${labelClass} md:col-span-2`}>
            {kind === 'ALERTA_LAPCH' ? 'Producto' : 'Tipo de denuncia'}
            <input
              required
              maxLength={300}
              value={subject}
              onChange={(event) => setSubject(event.target.value)}
              className={fieldClass}
            />
          </label>
          <label className={`${labelClass} md:col-span-2`}>
            Descripción
            <textarea
              required
              rows={4}
              maxLength={5000}
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              className={`${fieldClass} py-3`}
            />
          </label>
          {kind === 'ALERTA_LAPCH' ? (
            <label className={labelClass}>
              Prioridad (1 crítica – 5 baja)
              <input
                required
                type="number"
                min="1"
                max="5"
                value={priority}
                onChange={(event) => setPriority(Number(event.target.value))}
                className={fieldClass}
              />
            </label>
          ) : (
            <>
              <label className={labelClass}>
                Canal
                <input
                  required
                  maxLength={50}
                  value={channel}
                  onChange={(event) => setChannel(event.target.value)}
                  className={fieldClass}
                />
              </label>
              <label className="flex items-center gap-2 text-sm font-bold">
                <input
                  type="checkbox"
                  checked={isAnonymous}
                  onChange={(event) => setAnonymous(event.target.checked)}
                />{' '}
                Denuncia anónima
              </label>
              <label className="flex items-center gap-2 text-sm font-bold">
                <input
                  type="checkbox"
                  checked={isConfidential}
                  onChange={(event) => setConfidential(event.target.checked)}
                />{' '}
                Información confidencial
              </label>
            </>
          )}
          <label className={labelClass}>
            Resultado
            <select
              value={result}
              onChange={(event) => setResult(event.target.value)}
              className={fieldClass}
            >
              <option value="">Pendiente de decisión</option>
              {results.map((value) => (
                <option key={value} value={value}>
                  {formatStatusLabel(value)}
                </option>
              ))}
            </select>
          </label>
          <div className="flex justify-end gap-3 border-t pt-5 md:col-span-2">
            <button
              type="button"
              onClick={onClose}
              className="rounded-xl border px-4 py-2.5 font-bold"
            >
              Cancelar
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

export function FindingsManagement() {
  const { roles } = useAuth()
  const [items, setItems] = useState<FindingRecord[]>([])
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [error, setError] = useState('')
  const [detail, setDetail] = useState<FindingDetail | null>(null)
  const [loadingDetail, setLoadingDetail] = useState(false)
  const [updatingId, setUpdatingId] = useState('')
  const canManage = roles.some((role) => role === 'ADMINISTRADOR' || role === 'COORDINADOR')
  const pagination = useClientPagination(items)
  useEffect(() => {
    let active = true
    void getFindings({ search, status, pageSize: 100 })
      .then((result) => {
        if (active) {
          setItems(result.items)
          setError('')
        }
      })
      .catch((caught) => {
        if (active)
          setError(
            caught instanceof Error ? caught.message : 'No se pudieron cargar los hallazgos.',
          )
      })
    return () => {
      active = false
    }
  }, [search, status])
  async function showDetail(item: FindingRecord) {
    setLoadingDetail(true)
    try {
      setDetail(await getFinding(item.id))
    } catch (caught) {
      await alerts.error(caught, 'No se pudo cargar el detalle del hallazgo')
    } finally {
      setLoadingDetail(false)
    }
  }
  async function advanceStatus(item: FindingRecord) {
    const nextStatus =
      item.status === 'ABIERTA'
        ? 'EN_CORRECCION'
        : item.status === 'EN_CORRECCION'
          ? 'VALIDADO'
          : item.status === 'VALIDADO'
            ? 'CERRADO'
            : null
    if (!nextStatus) return
    const reason =
      nextStatus === 'CERRADO'
        ? await alerts.textInput({
            title: 'Cerrar no conformidad',
            label: 'Justifique el cierre después de validar la corrección.',
            confirmText: 'Cerrar hallazgo',
          })
        : (await alerts.confirm({
              title:
                nextStatus === 'EN_CORRECCION' ? 'Iniciar corrección' : 'Validar la corrección',
              text:
                nextStatus === 'EN_CORRECCION'
                  ? 'El hallazgo quedará identificado como una corrección en curso.'
                  : 'Confirme que la evidencia de corrección fue revisada.',
              confirmText: nextStatus === 'EN_CORRECCION' ? 'Iniciar' : 'Validar',
            }))
          ? undefined
          : null
    if (reason === null) return
    setUpdatingId(item.id)
    try {
      await updateFindingStatus(item.id, item.rowVersion, nextStatus, reason)
      setItems((current) =>
        current.map((value) =>
          value.id === item.id
            ? {
                ...value,
                status: nextStatus,
                rowVersion: value.rowVersion + 1,
                closedAt: nextStatus === 'CERRADO' ? new Date().toISOString() : null,
              }
            : value,
        ),
      )
      if (detail?.id === item.id) setDetail(null)
      await alerts.success(`Hallazgo ${formatStatusLabel(nextStatus).toLowerCase()}`)
    } catch (caught) {
      await alerts.error(caught, 'No se pudo actualizar el hallazgo')
    } finally {
      setUpdatingId('')
    }
  }
  return (
    <section aria-labelledby="findings-title">
      <PageHeader
        eyebrow="Ejecución de campo"
        title="Hallazgos y no conformidades"
        description="Consulte los incumplimientos generados automáticamente desde las respuestas de las evaluaciones."
      />
      {canManage && (
        <div className="mt-5 rounded-xl border border-brand-200 bg-brand-50 p-4 text-sm text-ink-body">
          <strong className="text-brand-900">Flujo de seguimiento</strong>
          <p className="mt-1">
            Use la acción de cada registro para avanzar en orden: Abierta → En corrección → Validado
            → Cerrado. El cierre exige una justificación.
          </p>
        </div>
      )}
      <div className="mt-6 rounded-card bg-white p-5 shadow-card">
        <form
          onSubmit={(event) => {
            event.preventDefault()
            setSearch(searchInput.trim())
          }}
          className="grid gap-3 md:grid-cols-[1fr_14rem_auto]"
        >
          <label className={labelClass}>
            Buscar
            <input
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder="Código, evaluación o establecimiento"
              className={fieldClass}
            />
          </label>
          <label className={labelClass}>
            Estado
            <select
              value={status}
              onChange={(event) => setStatus(event.target.value)}
              className={fieldClass}
            >
              <option value="">Todos</option>
              {['ABIERTA', 'EN_CORRECCION', 'VALIDADO', 'CERRADO'].map((value) => (
                <option key={value} value={value}>
                  {formatStatusLabel(value)}
                </option>
              ))}
            </select>
          </label>
          <button className="text-brand-800 mt-auto min-h-11 rounded-xl border border-brand-700 px-5 font-bold">
            Aplicar
          </button>
        </form>
        <ErrorBox message={error} />
        <div className="mt-5 overflow-x-auto">
          <table className="w-full min-w-[900px] text-left text-sm">
            <thead>
              <tr className="border-b text-xs text-ink-muted uppercase">
                <th className="px-3 py-3">Hallazgo</th>
                <th className="px-3 py-3">Evaluación</th>
                <th className="px-3 py-3">Ítem</th>
                <th className="px-3 py-3">Criticidad</th>
                <th className="px-3 py-3">Estado</th>
                <th className="px-3 py-3 text-right">Acción</th>
              </tr>
            </thead>
            <tbody>
              {pagination.visibleItems.map((item) => (
                <tr key={item.id} className="border-b border-slate-100">
                  <td className="px-3 py-4">
                    <strong>{item.code}</strong>
                    <span className="block max-w-xs truncate text-xs text-ink-muted">
                      {item.description}
                    </span>
                  </td>
                  <td className="px-3 py-4">
                    {item.evaluationNumber}
                    <span className="block text-xs text-ink-muted">{item.establishmentName}</span>
                  </td>
                  <td className="px-3 py-4">
                    {item.sourceItem} · {item.itemTitle}
                  </td>
                  <td className="px-3 py-4">{item.criticality}</td>
                  <td className="px-3 py-4">{formatStatusLabel(item.status)}</td>
                  <td className="px-3 py-4 text-right">
                    <div className="flex justify-end gap-2">
                      {canManage && item.status !== 'CERRADO' && (
                        <button
                          type="button"
                          onClick={() => void advanceStatus(item)}
                          disabled={updatingId === item.id}
                          className="rounded-lg bg-brand-700 px-3 py-2 font-bold text-white disabled:opacity-50"
                        >
                          {updatingId === item.id
                            ? 'Actualizando…'
                            : item.status === 'ABIERTA'
                              ? 'Iniciar corrección'
                              : item.status === 'EN_CORRECCION'
                                ? 'Validar'
                                : 'Cerrar'}
                        </button>
                      )}
                      <button
                        type="button"
                        onClick={() => void showDetail(item)}
                        disabled={loadingDetail}
                        className="border-brand-300 text-brand-800 rounded-lg border px-3 py-2 font-bold disabled:opacity-50"
                      >
                        Ver detalles
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {items.length === 0 && !error && (
            <p className="p-10 text-center text-sm text-ink-muted">No hay hallazgos registrados.</p>
          )}
        </div>
        <Pagination
          page={pagination.page}
          pageSize={pagination.pageSize}
          total={items.length}
          label="hallazgos"
          onChange={pagination.setPage}
        />
      </div>
      {detail && <FindingDetails detail={detail} onClose={() => setDetail(null)} />}
    </section>
  )
}

function FindingDetails({ detail, onClose }: { detail: FindingDetail; onClose: () => void }) {
  return (
    <div
      className="sigersa-modal-overlay fixed inset-0 z-50 grid place-items-center p-4"
      role="presentation"
    >
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby="finding-detail-title"
        className="sigersa-modal-panel w-full max-w-2xl p-6"
      >
        <div className="flex justify-between">
          <h2 id="finding-detail-title" className="text-xl font-extrabold">
            {detail.code} · {formatStatusLabel(detail.criticality)}
          </h2>
          <button type="button" onClick={onClose} aria-label="Cerrar">
            ✕
          </button>
        </div>
        <div className="mt-6 grid gap-4 text-sm">
          <div className="grid gap-3 rounded-xl bg-slate-50 p-4 md:grid-cols-2">
            <p>
              <strong>Evaluación:</strong> {detail.evaluationNumber}
            </p>
            <p>
              <strong>Caso:</strong> {detail.caseNumber}
            </p>
            <p>
              <strong>Empresa:</strong> {detail.companyName}
            </p>
            <p>
              <strong>Establecimiento:</strong> {detail.establishmentName}
            </p>
            <p className="md:col-span-2">
              <strong>Ítem:</strong> {detail.itemCode} · {detail.itemTitle}
            </p>
            <p>
              <strong>Respuesta:</strong> {formatStatusLabel(detail.rating)}
            </p>
            <p>
              <strong>Estado:</strong> {formatStatusLabel(detail.status)}
            </p>
          </div>
          <div>
            <strong>Descripción</strong>
            <p className="mt-1 text-ink-muted">{detail.description}</p>
          </div>
          {detail.observation && (
            <div>
              <strong>Observación</strong>
              <p className="mt-1 text-ink-muted">{detail.observation}</p>
            </div>
          )}
          {detail.technicalComment && (
            <div>
              <strong>Comentario técnico</strong>
              <p className="mt-1 text-ink-muted">{detail.technicalComment}</p>
            </div>
          )}
          <div>
            <strong>Evidencias ({detail.evidences.length}/3)</strong>
            {detail.evidences.length === 0 ? (
              <p className="mt-1 text-ink-muted">No se adjuntaron evidencias a este ítem.</p>
            ) : (
              <ul className="mt-2 space-y-2">
                {detail.evidences.map((evidence) => (
                  <li
                    key={evidence.id}
                    className="rounded-lg border border-emerald-200 bg-emerald-50 p-3 text-emerald-900"
                  >
                    {evidence.originalName} · {formatStatusLabel(evidence.evidenceType)}
                  </li>
                ))}
              </ul>
            )}
          </div>
          <div className="flex justify-end gap-3 border-t pt-5">
            <button
              type="button"
              onClick={onClose}
              className="rounded-xl border px-4 py-2.5 font-bold"
            >
              Cerrar
            </button>
          </div>
        </div>
      </section>
    </div>
  )
}

export function HistoryManagement() {
  const initialSearch = new URLSearchParams(window.location.search).get('search') ?? ''
  const [items, setItems] = useState<HistoricalEvaluation[]>([])
  const [searchInput, setSearchInput] = useState(initialSearch)
  const [search, setSearch] = useState(initialSearch)
  const [status, setStatus] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [error, setError] = useState('')
  const [timeline, setTimeline] = useState<{
    evaluation: HistoricalEvaluation
    events: TimelineEvent[]
  } | null>(null)
  const pagination = useClientPagination(items)
  const [generatingReport, setGeneratingReport] = useState<string | null>(null)
  const [refresh, setRefresh] = useState(0)
  const { roles } = useAuth()
  const canReview = roles.some((role) => role === 'ADMINISTRADOR' || role === 'COORDINADOR')
  useEffect(() => {
    let active = true
    void getEvaluationHistory({
      search,
      status,
      from: from ? new Date(`${from}T00:00:00`).toISOString() : undefined,
      to: to ? new Date(`${to}T23:59:59`).toISOString() : undefined,
      pageSize: 100,
    })
      .then((result) => {
        if (active) {
          setItems(result.items)
          setError('')
        }
      })
      .catch((caught) => {
        if (active)
          setError(caught instanceof Error ? caught.message : 'No se pudo cargar el histórico.')
      })
    return () => {
      active = false
    }
  }, [search, status, from, to, refresh])
  async function showTimeline(evaluation: HistoricalEvaluation) {
    try {
      setTimeline({ evaluation, events: await getEvaluationTimeline(evaluation.id) })
    } catch (caught) {
      await alerts.error(caught, 'No se pudo cargar la línea de tiempo')
    }
  }
  async function generateReport(evaluation: HistoricalEvaluation, official: boolean) {
    setGeneratingReport(evaluation.id)
    try {
      const report = await generateEvaluationReport(evaluation.id, official)
      setRefresh((value) => value + 1)
      await alerts.success(
        official ? 'Informe oficial emitido' : 'Borrador de informe generado',
        `${report.reportNumber}, versión ${report.version}.`,
      )
    } catch (caught) {
      await alerts.error(
        caught,
        official ? 'No se pudo emitir el informe' : 'No se pudo generar el informe',
      )
    } finally {
      setGeneratingReport(null)
    }
  }
  async function downloadReport(reportId: string, number: string) {
    try {
      const blob = await downloadEvaluationReport(reportId)
      const url = URL.createObjectURL(blob)
      const anchor = document.createElement('a')
      anchor.href = url
      anchor.download = `${number}.pdf`
      anchor.click()
      URL.revokeObjectURL(url)
    } catch (caught) {
      await alerts.error(caught, 'No se pudo descargar el informe')
    }
  }
  return (
    <section aria-labelledby="history-title">
      <PageHeader
        eyebrow="Análisis y decisión"
        title="Informes e histórico"
        description="Consulte evaluaciones por empresa, caso, fecha o estado y revise su trazabilidad completa."
      />
      <div className="mt-6 rounded-card bg-white p-5 shadow-card">
        <form
          onSubmit={(event) => {
            event.preventDefault()
            setSearch(searchInput.trim())
          }}
          className="grid gap-3 lg:grid-cols-[1fr_12rem_11rem_11rem_auto]"
        >
          <label className={labelClass}>
            Buscar
            <input
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder="Empresa, caso, evaluación o establecimiento"
              className={fieldClass}
            />
          </label>
          <label className={labelClass}>
            Estado
            <select
              value={status}
              onChange={(event) => setStatus(event.target.value)}
              className={fieldClass}
            >
              <option value="">Todos</option>
              {[
                'ASIGNADA',
                'EN_EJECUCION',
                'FINALIZADA',
                'ENVIADA',
                'EN_REVISION',
                'EN_CORRECCION',
                'APROBADA',
                'NO_APROBADA',
                'CERRADA',
              ].map((value) => (
                <option key={value} value={value}>
                  {formatStatusLabel(value)}
                </option>
              ))}
            </select>
          </label>
          <label className={labelClass}>
            Desde
            <input
              type="date"
              value={from}
              onChange={(event) => setFrom(event.target.value)}
              className={fieldClass}
            />
          </label>
          <label className={labelClass}>
            Hasta
            <input
              type="date"
              value={to}
              onChange={(event) => setTo(event.target.value)}
              className={fieldClass}
            />
          </label>
          <button className="text-brand-800 mt-auto min-h-11 rounded-xl border border-brand-700 px-5 font-bold">
            Aplicar
          </button>
        </form>
        <ErrorBox message={error} />
        <div className="mt-5 overflow-x-auto">
          <table className="w-full min-w-[1050px] text-left text-sm">
            <thead>
              <tr className="border-b text-xs text-ink-muted uppercase">
                <th className="px-3 py-3">Evaluación</th>
                <th className="px-3 py-3">Empresa / establecimiento</th>
                <th className="px-3 py-3">Técnico</th>
                <th className="px-3 py-3">Resultado</th>
                <th className="px-3 py-3">Informe</th>
                <th className="px-3 py-3 text-right">Detalle</th>
              </tr>
            </thead>
            <tbody>
              {pagination.visibleItems.map((item) => (
                <tr key={item.id} className="border-b border-slate-100">
                  <td className="px-3 py-4">
                    <strong>{item.number}</strong>
                    <span className="block text-xs text-ink-muted">
                      {item.caseNumber} · {formatStatusLabel(item.status)}
                    </span>
                  </td>
                  <td className="px-3 py-4">
                    {item.companyName}
                    <span className="block text-xs text-ink-muted">{item.establishmentName}</span>
                  </td>
                  <td className="px-3 py-4">{item.evaluatorName}</td>
                  <td className="px-3 py-4">
                    {item.compliancePercentage === null
                      ? 'Sin calcular'
                      : `${item.compliancePercentage.toFixed(1)}%`}
                    <span className="block text-xs text-ink-muted">
                      {item.riskLevel
                        ? `${formatStatusLabel(item.riskLevel)} · ${formatStatusLabel(item.frequency ?? '')}`
                        : 'Sin riesgo calculado'}
                    </span>
                  </td>
                  <td className="px-3 py-4">
                    {item.reportNumber ?? 'No generado'}
                    {item.hasOfficialReport && (
                      <span className="block text-xs font-bold text-brand-700">Oficial</span>
                    )}
                    <div className="mt-2 flex flex-wrap gap-2">
                      {canReview &&
                        !item.hasOfficialReport &&
                        [
                          'FINALIZADA',
                          'ENVIADA',
                          'EN_REVISION',
                          'EN_CORRECCION',
                          'APROBADA',
                          'NO_APROBADA',
                        ].includes(item.status) && (
                          <button
                            type="button"
                            onClick={() => void generateReport(item, false)}
                            disabled={generatingReport !== null}
                            className="rounded border px-2 py-1 text-xs font-bold"
                          >
                            {generatingReport === item.id ? 'Generando…' : 'Generar borrador'}
                          </button>
                        )}
                      {canReview &&
                        ['APROBADA', 'NO_APROBADA'].includes(item.status) &&
                        !item.hasOfficialReport && (
                          <button
                            type="button"
                            onClick={() => void generateReport(item, true)}
                            disabled={generatingReport !== null}
                            className="rounded bg-brand-700 px-2 py-1 text-xs font-bold text-white"
                          >
                            {generatingReport === item.id ? 'Generando…' : 'Emitir oficial'}
                          </button>
                        )}
                      {item.reportId && (
                        <button
                          type="button"
                          onClick={() =>
                            void downloadReport(item.reportId!, item.reportNumber ?? 'informe')
                          }
                          className="rounded border px-2 py-1 text-xs font-bold"
                        >
                          Descargar
                        </button>
                      )}
                    </div>
                  </td>
                  <td className="px-3 py-4 text-right">
                    <button
                      type="button"
                      onClick={() => void showTimeline(item)}
                      className="rounded-lg border px-3 py-2 font-bold"
                    >
                      Línea de tiempo
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {items.length === 0 && !error && (
            <p className="p-10 text-center text-sm text-ink-muted">
              No hay evaluaciones para los filtros seleccionados.
            </p>
          )}
        </div>
        <Pagination
          page={pagination.page}
          pageSize={pagination.pageSize}
          total={items.length}
          label="historial de evaluaciones"
          onChange={pagination.setPage}
        />
      </div>
      {timeline && (
        <div
          className="sigersa-modal-overlay fixed inset-0 z-50 grid place-items-center p-4"
          role="presentation"
        >
          <section
            role="dialog"
            aria-modal="true"
            aria-labelledby="timeline-title"
            className="sigersa-modal-panel max-h-[90vh] w-full max-w-2xl overflow-y-auto p-6"
          >
            <div className="flex justify-between">
              <div>
                <h2 id="timeline-title" className="text-xl font-extrabold">
                  Trazabilidad de {timeline.evaluation.number}
                </h2>
                <p className="mt-1 text-sm text-ink-muted">
                  {timeline.evaluation.establishmentName}
                </p>
              </div>
              <button type="button" onClick={() => setTimeline(null)} aria-label="Cerrar">
                ✕
              </button>
            </div>
            <ol className="mt-6 space-y-4 border-l-2 border-brand-100 pl-5">
              {timeline.events.map((event, index) => (
                <li key={`${event.occurredAt}-${index}`} className="relative">
                  <span className="absolute top-1 -left-[1.65rem] size-3 rounded-full bg-brand-600" />
                  <strong className="block text-sm">{event.title}</strong>
                  <span className="text-xs text-ink-muted">
                    {new Intl.DateTimeFormat('es-DO', {
                      dateStyle: 'medium',
                      timeStyle: 'short',
                    }).format(new Date(event.occurredAt))}
                    {event.actorName ? ` · ${event.actorName}` : ''}
                  </span>
                  {event.detail && <p className="mt-1 text-sm text-ink-body">{event.detail}</p>}
                </li>
              ))}
            </ol>
          </section>
        </div>
      )}
      {generatingReport && (
        <div
          className="sigersa-modal-overlay fixed inset-0 z-50 grid place-items-center p-4"
          role="status"
          aria-live="polite"
          aria-label="Generando informe"
        >
          <div className="sigersa-modal-panel flex max-w-sm items-center gap-4 p-6">
            <span
              className="size-10 animate-spin rounded-full border-4 border-brand-100 border-t-brand-700"
              aria-hidden="true"
            />
            <div>
              <strong className="block text-ink-strong">Generando informe</strong>
              <span className="text-sm text-ink-muted">Preparando el PDF y sus anexos…</span>
            </div>
          </div>
        </div>
      )}
    </section>
  )
}

export function AuditManagement() {
  const initialSearch = new URLSearchParams(window.location.search).get('search') ?? ''
  const [items, setItems] = useState<AuditEventRecord[]>([])
  const [searchInput, setSearchInput] = useState(initialSearch)
  const [search, setSearch] = useState(initialSearch)
  const [result, setResult] = useState('')
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [error, setError] = useState('')
  const pagination = useClientPagination(items)
  useEffect(() => {
    let active = true
    void getAuditEvents({
      search,
      result,
      from: from ? new Date(`${from}T00:00:00`).toISOString() : undefined,
      to: to ? new Date(`${to}T23:59:59`).toISOString() : undefined,
      pageSize: 100,
    })
      .then((page) => {
        if (active) {
          setItems(page.items)
          setError('')
        }
      })
      .catch((caught) => {
        if (active)
          setError(caught instanceof Error ? caught.message : 'No se pudo cargar la auditoría.')
      })
    return () => {
      active = false
    }
  }, [search, result, from, to])
  return (
    <section aria-labelledby="audit-title">
      <PageHeader
        eyebrow="Trazabilidad"
        title="Auditoría"
        description="Eventos inmutables de seguridad, cambios sensibles, decisiones y resultados."
      />
      <div className="mt-6 rounded-card bg-white p-5 shadow-card">
        <form
          onSubmit={(event) => {
            event.preventDefault()
            setSearch(searchInput.trim())
          }}
          className="grid gap-3 lg:grid-cols-[1fr_12rem_11rem_11rem_auto]"
        >
          <label className={labelClass}>
            Buscar
            <input
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder="Acción, recurso o actor"
              className={fieldClass}
            />
          </label>
          <label className={labelClass}>
            Resultado
            <select
              value={result}
              onChange={(event) => setResult(event.target.value)}
              className={fieldClass}
            >
              <option value="">Todos</option>
              {['EXITOSO', 'FALLIDO', 'DENEGADO'].map((value) => (
                <option key={value} value={value}>
                  {formatStatusLabel(value)}
                </option>
              ))}
            </select>
          </label>
          <label className={labelClass}>
            Desde
            <input
              type="date"
              value={from}
              onChange={(event) => setFrom(event.target.value)}
              className={fieldClass}
            />
          </label>
          <label className={labelClass}>
            Hasta
            <input
              type="date"
              value={to}
              onChange={(event) => setTo(event.target.value)}
              className={fieldClass}
            />
          </label>
          <button className="text-brand-800 mt-auto min-h-11 rounded-xl border border-brand-700 px-5 font-bold">
            Aplicar
          </button>
        </form>
        <ErrorBox message={error} />
        <div className="mt-5 overflow-x-auto">
          <table className="w-full min-w-[950px] text-left text-sm">
            <thead>
              <tr className="border-b text-xs text-ink-muted uppercase">
                <th className="px-3 py-3">Fecha</th>
                <th className="px-3 py-3">Acción</th>
                <th className="px-3 py-3">Recurso</th>
                <th className="px-3 py-3">Actor</th>
                <th className="px-3 py-3">Resultado</th>
                <th className="px-3 py-3">Motivo</th>
              </tr>
            </thead>
            <tbody>
              {pagination.visibleItems.map((item) => (
                <tr key={item.id} className="border-b border-slate-100">
                  <td className="px-3 py-4">
                    {new Intl.DateTimeFormat('es-DO', {
                      dateStyle: 'medium',
                      timeStyle: 'short',
                    }).format(new Date(item.occurredAt))}
                  </td>
                  <td className="px-3 py-4 font-bold">{formatStatusLabel(item.action)}</td>
                  <td className="px-3 py-4">
                    {item.resourceType}
                    <span className="block text-xs text-ink-muted">
                      {item.resourceId ?? 'Sin identificador'}
                    </span>
                  </td>
                  <td className="px-3 py-4">{item.actorName ?? 'Sistema'}</td>
                  <td className="px-3 py-4">{formatStatusLabel(item.result)}</td>
                  <td className="px-3 py-4">{item.reason ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
          {items.length === 0 && !error && (
            <p className="p-10 text-center text-sm text-ink-muted">
              No hay eventos para los filtros seleccionados.
            </p>
          )}
        </div>
        <Pagination
          page={pagination.page}
          pageSize={pagination.pageSize}
          total={items.length}
          label="auditoría"
          onChange={pagination.setPage}
        />
      </div>
    </section>
  )
}
