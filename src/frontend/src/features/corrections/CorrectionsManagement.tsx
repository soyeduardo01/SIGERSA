import { useCallback, useEffect, useState, type FormEvent } from 'react'
import { TableSkeleton } from '../../components/feedback/Skeletons'
import { Pagination } from '../../components/ui/Pagination'
import { useAuth } from '../../contexts/useAuth'
import { useParameterOptions } from '../../hooks/useParameterOptions'
import {
  createCorrection,
  getEvaluationForm,
  getCorrectionOptions,
  getCorrections,
  transitionCorrection,
  type Correction,
  type CorrectionDraft,
  type CorrectionOptions,
  type CorrectionsPage,
  type EvaluationFormItem,
} from '../../lib/api'
import { alerts } from '../../lib/alerts'
import { formatStatusLabel } from '../../lib/formatters'
import { moduleHref } from '../../lib/navigation'
import {
  correctionMinimumLeadMinutes,
  futureLocalDateTime,
  toUtcIsoFromLocalInput,
} from '../../lib/dateTime'
import { queueCorrection } from '../../offline/syncQueue'

const emptyPage: CorrectionsPage = { items: [], page: 1, pageSize: 20, total: 0 }

export function CorrectionsManagement() {
  const correctionStates = useParameterOptions('ESTADO_CORRECCION')
  const { roles } = useAuth()
  const administrator = roles.includes('ADMINISTRADOR')
  const coordinator = roles.includes('COORDINADOR')
  const [result, setResult] = useState(emptyPage)
  const [options, setOptions] = useState<CorrectionOptions | null>(null)
  const [status, setStatus] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [open, setOpen] = useState(false)
  const [selectedCorrection, setSelectedCorrection] = useState<Correction | null>(null)
  const [pageNumber, setPageNumber] = useState(1)

  const load = useCallback(async () => {
    setLoading(true)
    try {
      const [page, choices] = await Promise.all([
        getCorrections({ status, page: pageNumber, pageSize: 20 }),
        getCorrectionOptions(),
      ])
      setResult(page)
      setOptions(choices)
      setError('')
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'No se pudieron cargar las correcciones.')
    } finally {
      setLoading(false)
    }
  }, [pageNumber, status])
  useEffect(() => {
    queueMicrotask(() => void load())
  }, [load])

  async function save(draft: CorrectionDraft) {
    try {
      if (navigator.onLine) await createCorrection(draft)
      else await queueCorrection(draft)
      setOpen(false)
      if (navigator.onLine) await load()
      await alerts.success(
        navigator.onLine ? 'Corrección solicitada' : 'Corrección guardada sin conexión',
      )
    } catch (caught) {
      await alerts.error(caught, 'No se pudo solicitar la corrección')
      throw caught
    }
  }

  async function transition(item: Correction, action: 'submit' | 'accept' | 'reject') {
    const coordinatorStage = item.status === 'PENDIENTE' || item.status === 'ENVIADA'
    const accepted = await alerts.confirm({
      title:
        action === 'submit'
          ? 'Enviar al coordinador'
          : action === 'accept'
            ? coordinatorStage
              ? 'Aprobar y enviar al administrador'
              : 'Confirmar corrección'
            : coordinatorStage
              ? 'Rechazar solicitud'
              : 'Rechazar corrección',
      text:
        action === 'accept'
          ? coordinatorStage
            ? 'La solicitud pasará al administrador. La evaluación continuará bloqueada hasta su decisión final.'
            : 'La corrección quedará aprobada y la evaluación volverá a ejecución.'
          : action === 'reject'
            ? 'La solicitud se rechazará y la evaluación volverá a ejecución para continuar.'
            : 'La solicitud quedará disponible para revisión del coordinador.',
      confirmText:
        action === 'accept' && coordinatorStage ? 'Enviar al administrador' : 'Confirmar',
    })
    if (!accepted) return
    try {
      await transitionCorrection(item.id, action, item.rowVersion)
      await load()
      await alerts.success('Estado actualizado')
    } catch (caught) {
      await alerts.error(caught, 'No se pudo actualizar la corrección')
    }
  }

  return (
    <section aria-labelledby="corrections-title">
      <div className="flex flex-col justify-between gap-4 md:flex-row md:items-end">
        <div>
          <p className="text-xs font-bold tracking-[0.14em] text-brand-700 uppercase">
            Seguimiento
          </p>
          <h1 id="corrections-title" className="mt-2 text-2xl font-extrabold">
            Correcciones
          </h1>
          <p className="mt-2 text-sm text-ink-muted">
            Solicite, atienda y revise correcciones dentro del ámbito autorizado.
          </p>
        </div>
        {options?.canCreate && (
          <button
            type="button"
            onClick={() => setOpen(true)}
            className="min-h-11 rounded-xl bg-brand-700 px-5 font-bold text-white"
          >
            + Solicitar corrección
          </button>
        )}
      </div>
      <div className="mt-6 rounded-card bg-white p-5 shadow-card">
        <label className="block max-w-xs text-sm font-bold">
          Estado
          <select
            value={status}
            onChange={(e) => {
              setStatus(e.target.value)
              setPageNumber(1)
            }}
            className="mt-1.5 min-h-11 w-full rounded-xl border px-3 font-normal"
          >
            <option value="">Todos</option>
            {correctionStates.options.map((value) => (
              <option key={value.parametersId} value={value.stringData ?? ''}>
                {formatStatusLabel(value.stringData ?? '')}
              </option>
            ))}
          </select>
          {!correctionStates.loading && correctionStates.options.length === 0 && (
            <span className="mt-1 block text-xs font-normal text-amber-700">
              El catálogo ESTADO_CORRECCION no tiene valores activos.
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
          <table className="w-full min-w-[950px] text-left text-sm">
            <thead>
              <tr className="border-b text-xs text-ink-muted uppercase">
                <th className="px-3 py-3">Evaluación</th>
                <th className="px-3 py-3">Revisión</th>
                <th className="px-3 py-3">Responsable</th>
                <th className="px-3 py-3">Solicitud</th>
                <th className="px-3 py-3">Límite</th>
                <th className="px-3 py-3">Estado</th>
                <th className="px-3 py-3 text-right">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {result.items.map((item) => (
                <tr key={item.id} className="border-b border-slate-100">
                  <td className="px-3 py-4 font-bold">
                    {item.evaluationNumber}
                    <span className="block text-xs font-normal text-ink-muted">
                      {item.establishmentName}
                    </span>
                  </td>
                  <td className="px-3 py-4">#{item.revisionNumber}</td>
                  <td className="px-3 py-4">
                    {formatStatusLabel(item.responsibleType)}
                    <span className="block text-xs text-ink-muted">
                      {item.assignedToName ?? 'Empresa evaluada'}
                    </span>
                  </td>
                  <td className="max-w-xs px-3 py-4">
                    {item.coordinatorObservation}
                    {item.fields.length > 0 && (
                      <ul className="mt-2 space-y-1 text-xs text-ink-muted">
                        {item.fields.map((field) => (
                          <li key={field.sourceItem}>
                            <strong>{field.itemTitle}:</strong> {field.reason} ·{' '}
                            {formatStatusLabel(field.status)}
                          </li>
                        ))}
                      </ul>
                    )}
                  </td>
                  <td className="px-3 py-4">{new Date(item.dueAt).toLocaleDateString()}</td>
                  <td className="px-3 py-4">{formatStatusLabel(item.status)}</td>
                  <td className="px-3 py-4 text-right">
                    <div className="flex justify-end gap-2">
                      {(coordinator || administrator) && (
                        <button
                          type="button"
                          onClick={() => setSelectedCorrection(item)}
                          className="rounded-lg border border-slate-300 px-3 py-2 font-bold text-slate-700"
                        >
                          Ver detalle
                        </button>
                      )}
                      {roles.includes('TECNICO_EVALUADOR') && item.status === 'PENDIENTE' && (
                        <button
                          type="button"
                          onClick={() => void transition(item, 'submit')}
                          className="rounded-lg border px-3 py-2 font-bold"
                        >
                          Enviar al coordinador
                        </button>
                      )}
                      {coordinator && ['PENDIENTE', 'ENVIADA'].includes(item.status) && (
                        <>
                          <button
                            type="button"
                            onClick={() => void transition(item, 'accept')}
                            className="rounded-lg border border-green-300 px-3 py-2 font-bold text-green-700"
                          >
                            Aprobar y enviar al administrador
                          </button>
                          <button
                            type="button"
                            onClick={() => void transition(item, 'reject')}
                            className="rounded-lg border border-red-300 px-3 py-2 font-bold text-red-700"
                          >
                            Rechazar solicitud
                          </button>
                        </>
                      )}
                      {administrator && item.status === 'EN_PROCESO' && (
                        <>
                          <a
                            href={moduleHref('fichas-bpm')}
                            className="rounded-lg border border-slate-300 px-3 py-2 font-bold text-slate-700"
                          >
                            Modificar ficha BPM
                          </a>
                          <button
                            type="button"
                            onClick={() => void transition(item, 'accept')}
                            className="rounded-lg border border-green-300 px-3 py-2 font-bold text-green-700"
                          >
                            Confirmar corrección
                          </button>
                          <button
                            type="button"
                            onClick={() => void transition(item, 'reject')}
                            className="rounded-lg border border-red-300 px-3 py-2 font-bold text-red-700"
                          >
                            Rechazar corrección
                          </button>
                        </>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {loading && <TableSkeleton rows={5} columns={7} />}
          {!loading && !error && result.items.length === 0 && (
            <p className="p-10 text-center text-sm text-ink-muted">
              No hay correcciones disponibles.
            </p>
          )}
        </div>
        <Pagination
          page={pageNumber}
          pageSize={result.pageSize}
          total={result.total}
          disabled={loading}
          label="correcciones"
          onChange={setPageNumber}
        />
      </div>
      {open && options && (
        <CorrectionForm options={options} onClose={() => setOpen(false)} onSave={save} />
      )}
      {selectedCorrection && (
        <CorrectionDetail
          correction={selectedCorrection}
          onClose={() => setSelectedCorrection(null)}
        />
      )}
    </section>
  )
}

function CorrectionDetail({
  correction,
  onClose,
}: {
  correction: Correction
  onClose: () => void
}) {
  return (
    <div className="sigersa-modal-overlay fixed inset-0 z-50 grid place-items-center p-4">
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby="correction-detail-title"
        className="sigersa-modal-panel max-h-[90vh] w-full max-w-3xl overflow-y-auto p-6"
      >
        <div className="flex items-start justify-between gap-4">
          <div>
            <p className="text-xs font-bold tracking-[0.12em] text-brand-700 uppercase">
              Evaluación {correction.evaluationNumber} · Revisión #{correction.revisionNumber}
            </p>
            <h2 id="correction-detail-title" className="mt-1 text-2xl font-extrabold">
              Detalle de la corrección
            </h2>
            <p className="mt-1 text-sm text-ink-muted">{correction.establishmentName}</p>
          </div>
          <button type="button" onClick={onClose} aria-label="Cerrar detalle">
            ✕
          </button>
        </div>

        <dl className="mt-6 grid gap-3 rounded-xl bg-slate-50 p-4 text-sm sm:grid-cols-2">
          <DetailTerm label="Estado" value={formatStatusLabel(correction.status)} />
          <DetailTerm
            label="Técnico solicitante"
            value={correction.assignedToName ?? 'No identificado'}
          />
          <DetailTerm label="Solicitada" value={formatDateTime(correction.requestedAt)} />
          <DetailTerm label="Fecha límite" value={formatDateTime(correction.dueAt)} />
        </dl>

        <div className="mt-5">
          <h3 className="font-extrabold">Justificación e instrucciones</h3>
          <p className="mt-2 rounded-xl border border-slate-200 p-4 text-sm leading-6 whitespace-pre-wrap">
            {correction.coordinatorObservation}
          </p>
        </div>

        <div className="mt-5">
          <h3 className="font-extrabold">Criterios incluidos</h3>
          {correction.fields.length === 0 ? (
            <p className="mt-2 rounded-xl bg-slate-50 p-4 text-sm text-ink-muted">
              La solicitud aplica de forma general y no seleccionó criterios específicos.
            </p>
          ) : (
            <div className="mt-3 space-y-3">
              {correction.fields.map((field) => (
                <article key={field.sourceItem} className="rounded-xl border border-slate-200 p-4">
                  <div className="flex flex-wrap justify-between gap-2">
                    <strong>{field.itemTitle}</strong>
                    <span className="rounded-full bg-slate-100 px-3 py-1 text-xs font-bold">
                      {formatStatusLabel(field.status)}
                    </span>
                  </div>
                  <p className="mt-2 text-sm">
                    <strong>Motivo:</strong> {field.reason}
                  </p>
                  <Snapshot label="Respuesta al solicitar" json={field.previousSnapshotJson} />
                  {field.newSnapshotJson && (
                    <Snapshot label="Respuesta corregida" json={field.newSnapshotJson} />
                  )}
                </article>
              ))}
            </div>
          )}
        </div>

        <div className="mt-6 flex justify-end border-t pt-5">
          <button
            type="button"
            onClick={onClose}
            className="rounded-xl bg-brand-700 px-5 py-2.5 font-bold text-white"
          >
            Cerrar
          </button>
        </div>
      </section>
    </div>
  )
}

function DetailTerm({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs font-bold text-ink-muted uppercase">{label}</dt>
      <dd className="mt-1 font-semibold">{value}</dd>
    </div>
  )
}

function Snapshot({ label, json }: { label: string; json: string | null }) {
  const snapshot = parseSnapshot(json)
  if (!snapshot) return null
  return (
    <div className="mt-3 rounded-lg bg-slate-50 p-3 text-xs leading-5 text-slate-700">
      <strong className="block text-slate-900">{label}</strong>
      <span>Respuesta: {formatStatusLabel(snapshot.rating ?? 'Sin respuesta')}</span>
      {snapshot.observation && <span className="block">Observación: {snapshot.observation}</span>}
      {snapshot.comment && <span className="block">Comentario: {snapshot.comment}</span>}
    </div>
  )
}

function parseSnapshot(json: string | null) {
  if (!json) return null
  try {
    return JSON.parse(json) as { rating?: string; observation?: string; comment?: string }
  } catch {
    return null
  }
}

function formatDateTime(value: string) {
  return new Intl.DateTimeFormat('es-DO', { dateStyle: 'medium', timeStyle: 'short' }).format(
    new Date(value),
  )
}

function CorrectionForm({
  options,
  onClose,
  onSave,
}: {
  options: CorrectionOptions
  onClose: () => void
  onSave: (draft: CorrectionDraft) => Promise<void>
}) {
  const [evaluationId, setEvaluationId] = useState('')
  const [observation, setObservation] = useState('')
  const [dueAt, setDueAt] = useState(() => futureLocalDateTime(24 * 60))
  const [saving, setSaving] = useState(false)
  const [evaluationItems, setEvaluationItems] = useState<EvaluationFormItem[]>([])
  const [fieldReasons, setFieldReasons] = useState<Record<number, string>>({})

  useEffect(() => {
    if (!evaluationId) return
    void getEvaluationForm(evaluationId)
      .then((items) => setEvaluationItems(items.filter((item) => item.isEvaluable)))
      .catch((caught) => void alerts.error(caught, 'No se pudieron cargar los criterios'))
  }, [evaluationId])
  async function submit(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    try {
      await onSave({
        evaluationId,
        responsibleType: 'TECNICO',
        assignedToId: null,
        coordinatorObservation: observation,
        dueAt: toUtcIsoFromLocalInput(dueAt),
        idempotencyKey: crypto.randomUUID(),
        fields: Object.entries(fieldReasons)
          .filter(([, reason]) => reason.trim())
          .map(([sourceItem, reason]) => ({
            sourceItem: Number(sourceItem),
            reason: reason.trim(),
          })),
      })
    } finally {
      setSaving(false)
    }
  }
  const field = 'mt-1.5 min-h-11 w-full rounded-xl border px-3 font-normal'
  return (
    <div
      className="sigersa-modal-overlay fixed inset-0 z-50 grid place-items-center p-4"
      role="presentation"
    >
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby="correction-form-title"
        className="sigersa-modal-panel max-h-[90vh] w-full max-w-2xl overflow-y-auto p-6"
      >
        <div className="flex justify-between">
          <h2 id="correction-form-title" className="text-xl font-extrabold">
            Solicitar corrección
          </h2>
          <button type="button" onClick={onClose} aria-label="Cerrar">
            ✕
          </button>
        </div>
        <form onSubmit={(event) => void submit(event)} className="mt-6 grid gap-4">
          <p className="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm text-amber-900">
            Al solicitar la corrección, la ficha quedará en modo de solo lectura. El coordinador
            deberá habilitar la evaluación para que puedas continuar.
          </p>
          <label className="text-sm font-bold">
            Evaluación
            <select
              required
              value={evaluationId}
              onChange={(e) => {
                setEvaluationId(e.target.value)
                setEvaluationItems([])
                setFieldReasons({})
              }}
              className={field}
            >
              <option value="">Seleccione</option>
              {options.evaluations.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.name}
                </option>
              ))}
            </select>
          </label>
          <label className="text-sm font-bold">
            Fecha límite
            <input
              required
              type="datetime-local"
              min={futureLocalDateTime(correctionMinimumLeadMinutes)}
              value={dueAt}
              onChange={(e) => setDueAt(e.target.value)}
              className={field}
            />
          </label>
          <label className="text-sm font-bold">
            Instrucciones
            <textarea
              required
              rows={4}
              value={observation}
              onChange={(e) => setObservation(e.target.value)}
              className={`${field} py-3`}
            />
          </label>
          {evaluationItems.length > 0 && (
            <fieldset className="rounded-xl border border-slate-200 p-4">
              <legend className="px-2 text-sm font-extrabold">Criterios observados</legend>
              <p className="mb-3 text-xs text-ink-muted">
                Marque los campos que el responsable debe corregir y especifique el motivo.
              </p>
              <div className="max-h-64 space-y-2 overflow-y-auto pr-1">
                {evaluationItems.map((item) => {
                  const selected = Object.hasOwn(fieldReasons, item.sourceItem)
                  return (
                    <div key={item.id} className="rounded-lg border border-slate-200 p-3">
                      <label className="flex items-start gap-2 text-sm font-bold">
                        <input
                          type="checkbox"
                          className="mt-1"
                          checked={selected}
                          onChange={(event) =>
                            setFieldReasons((current) => {
                              if (event.target.checked)
                                return { ...current, [item.sourceItem]: observation }
                              const next = { ...current }
                              delete next[item.sourceItem]
                              return next
                            })
                          }
                        />
                        {item.title}
                      </label>
                      {selected && (
                        <textarea
                          required
                          rows={2}
                          value={fieldReasons[item.sourceItem] ?? ''}
                          onChange={(event) =>
                            setFieldReasons((current) => ({
                              ...current,
                              [item.sourceItem]: event.target.value,
                            }))
                          }
                          className={`${field} mt-2 py-2 text-sm`}
                          placeholder="Motivo específico de la corrección"
                        />
                      )}
                    </div>
                  )
                })}
              </div>
            </fieldset>
          )}
          <div className="flex justify-end gap-3 border-t pt-5">
            <button
              type="button"
              onClick={onClose}
              className="rounded-xl border px-4 py-2.5 font-bold"
            >
              Volver
            </button>
            <button
              disabled={saving}
              className="rounded-xl bg-brand-700 px-5 py-2.5 font-bold text-white disabled:opacity-50"
            >
              {saving ? 'Guardando…' : 'Solicitar'}
            </button>
          </div>
        </form>
      </section>
    </div>
  )
}
