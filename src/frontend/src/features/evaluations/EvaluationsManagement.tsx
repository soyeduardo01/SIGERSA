import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { TableSkeleton } from '../../components/feedback/Skeletons'
import { Pagination } from '../../components/ui/Pagination'
import {
  createEvaluation,
  getEvaluationOptions,
  getEvaluations,
  startEvaluation,
  transitionEvaluation,
  type EvaluationCreateOptions,
  type EvaluationDraft,
  type EvaluationSummary,
  type EvaluationsPage,
} from '../../lib/api'
import { alerts } from '../../lib/alerts'
import { formatStatusLabel } from '../../lib/formatters'
import { useParameterOptions } from '../../hooks/useParameterOptions'
import { prepareEvaluationOffline } from '../../offline/evaluationCache'
import { DynamicInspectionForm } from '../inspection/DynamicInspectionForm'
import { useAuth } from '../../contexts/useAuth'
import {
  canEditInspection,
  canCloseEvaluation,
  canExecuteInspection,
  canFinalizeReview,
  canSubmitForReview,
  inspectionActionLabel,
} from './evaluationActions'

const emptyPage: EvaluationsPage = { items: [], page: 1, pageSize: 20, total: 0 }

export function EvaluationsManagement() {
  const [result, setResult] = useState(emptyPage)
  const [options, setOptions] = useState<EvaluationCreateOptions | null>(null)
  const [status, setStatus] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [creating, setCreating] = useState(false)
  const [selected, setSelected] = useState<EvaluationSummary | null>(null)
  const [offlineReadyIds, setOfflineReadyIds] = useState<Set<string>>(new Set())
  const [preparingOfflineId, setPreparingOfflineId] = useState('')
  const [pageNumber, setPageNumber] = useState(1)
  const evaluationStates = useParameterOptions('ESTADO_EVALUACION')
  const { roles } = useAuth()
  const canExecute = canExecuteInspection(roles)

  async function load() {
    setLoading(true)
    try {
      const page = await getEvaluations({ status, page: pageNumber, pageSize: 10 })
      setResult(page)
      setSelected((current) =>
        current ? (page.items.find((item) => item.id === current.id) ?? current) : current,
      )
      setError('')
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'No se pudieron cargar las evaluaciones.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    let active = true
    void Promise.all([
      getEvaluations({ status, page: pageNumber, pageSize: 10 }),
      getEvaluationOptions(),
    ])
      .then(([page, choices]) => {
        if (!active) return
        setResult(page)
        setOptions(choices)
        const requestedId = new URLSearchParams(window.location.search).get('evaluation')
        const requested = page.items.find((item) => item.id === requestedId)
        if (requested) setSelected(requested)
        setError('')
      })
      .catch((caught) => {
        if (active)
          setError(
            caught instanceof Error ? caught.message : 'No se pudieron cargar las evaluaciones.',
          )
      })
      .finally(() => active && setLoading(false))
    return () => {
      active = false
    }
  }, [pageNumber, status])

  useEffect(() => {
    if (!navigator.onLine || !canExecute) return
    const candidates = result.items.filter((item) => item.status === 'EN_EJECUCION' && item.canEdit)
    if (candidates.length === 0) return
    let active = true
    void Promise.allSettled(
      candidates.map(async (item) => {
        await prepareEvaluationOffline(item.id)
        return item.id
      }),
    ).then((results) => {
      if (!active) return
      const prepared = results.flatMap((entry) =>
        entry.status === 'fulfilled' ? [entry.value] : [],
      )
      if (prepared.length > 0) setOfflineReadyIds((current) => new Set([...current, ...prepared]))
    })
    return () => {
      active = false
    }
  }, [canExecute, result.items])

  async function save(draft: EvaluationDraft) {
    try {
      const created = await createEvaluation(draft)
      setCreating(false)
      await load()
      await alerts.success('Evaluación creada', `Se creó la evaluación ${created.number}.`)
    } catch (caught) {
      await alerts.error(caught, 'No se pudo crear la evaluación')
      throw caught
    }
  }

  async function runTransition(
    item: EvaluationSummary,
    action: 'start' | 'submit' | 'review' | 'approve' | 'reject' | 'close',
  ) {
    const labels = {
      start: 'Iniciar evaluación',
      submit: 'Enviar a revisión',
      review: 'Comenzar revisión',
      approve: 'Aprobar evaluación',
      reject: 'Marcar como no aprobada',
      close: 'Cerrar expediente',
    }
    const confirmed = await alerts.confirm({
      title: labels[action],
      text:
        action === 'close'
          ? item.status === 'APROBADA'
            ? 'El cierre de una evaluación aprobada requiere un informe oficial emitido.'
            : 'La evaluación no aprobada y su caso quedarán cerrados.'
          : 'El cambio quedará registrado en la trazabilidad.',
      confirmText: labels[action],
    })
    if (!confirmed) return
    try {
      if (action === 'start') await startEvaluation(item.id, item.rowVersion)
      else await transitionEvaluation(item.id, action, item.rowVersion)
      await load()
      await alerts.success(
        labels[action],
        action === 'close' && item.status === 'NO_APROBADA'
          ? 'El sistema creó automáticamente una reinspección a 30 días cuando existían no conformidades pendientes.'
          : undefined,
      )
    } catch (caught) {
      await alerts.error(caught, `No se pudo ${labels[action].toLowerCase()}`)
    }
  }

  async function prepareOffline(item: EvaluationSummary) {
    if (!navigator.onLine) {
      await alerts.error(new Error('Se requiere conexión para descargar la ficha.'), 'Sin conexión')
      return
    }
    setPreparingOfflineId(item.id)
    try {
      await prepareEvaluationOffline(item.id)
      setOfflineReadyIds((current) => new Set(current).add(item.id))
      await alerts.success(
        'Evaluación disponible offline',
        'Puede completar respuestas y adjuntar evidencias sin conexión; se sincronizarán al volver la red.',
      )
    } catch (caught) {
      await alerts.error(caught, 'No se pudo preparar la evaluación offline')
    } finally {
      setPreparingOfflineId('')
    }
  }

  return (
    <section aria-labelledby="evaluations-title">
      <div className="flex flex-col justify-between gap-4 md:flex-row md:items-end">
        <div>
          <p className="text-xs font-bold tracking-[0.14em] text-brand-700 uppercase">
            Inspección BPM
          </p>
          <h1 id="evaluations-title" className="mt-2 text-2xl font-extrabold">
            Evaluaciones e inspecciones
          </h1>
          <p className="mt-2 text-sm text-ink-muted">
            Cree y supervise evaluaciones. Mientras estén en ejecución podrá realizar la inspección;
            después de finalizarlas, la ficha quedará disponible únicamente para consulta.
          </p>
        </div>
        {options?.canCreate && (
          <button
            type="button"
            onClick={() => setCreating(true)}
            className="min-h-11 rounded-xl bg-brand-700 px-5 font-bold text-white"
          >
            + Nueva evaluación
          </button>
        )}
      </div>

      {roles.includes('COORDINADOR') && (
        <div className="mt-5 rounded-xl border border-brand-200 bg-brand-50 p-4 text-sm text-ink-body">
          <strong className="text-brand-900">¿Necesita una reinspección?</strong>
          <p className="mt-1">
            Marque la evaluación como no aprobada y use “Cerrar y programar reinspección”. Si hay no
            conformidades pendientes, el sistema creará el caso, la programación y la evaluación de
            seguimiento para dentro de 30 días.
          </p>
        </div>
      )}

      <div className="mt-6 rounded-card bg-white p-5 shadow-card">
        <label className="block max-w-xs text-sm font-bold">
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
            {evaluationStates.options.map((option) => (
              <option key={option.parametersId} value={option.stringData ?? ''}>
                {formatStatusLabel(option.stringData ?? '')}
              </option>
            ))}
          </select>
          {!evaluationStates.loading && evaluationStates.options.length === 0 && (
            <span className="mt-1 block text-xs font-normal text-amber-700">
              El catálogo ESTADO_EVALUACION no tiene valores activos.
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
          <table className="w-full min-w-[920px] text-left text-sm">
            <thead>
              <tr className="border-b text-xs text-ink-muted uppercase">
                <th className="px-3 py-3">Evaluación</th>
                <th className="px-3 py-3">Establecimiento</th>
                <th className="px-3 py-3">Técnico</th>
                <th className="px-3 py-3">Estado</th>
                <th className="px-3 py-3">Resultado</th>
                <th className="px-3 py-3 text-right">Acción</th>
              </tr>
            </thead>
            <tbody>
              {result.items.map((item) => (
                <tr key={item.id} className="border-b border-slate-100">
                  <td className="px-3 py-4 font-bold">
                    {item.number}
                    <span className="block text-xs font-normal text-ink-muted">
                      Caso {item.caseNumber}
                    </span>
                  </td>
                  <td className="px-3 py-4">{item.establishmentName}</td>
                  <td className="px-3 py-4">{item.evaluatorName}</td>
                  <td className="px-3 py-4">{formatStatusLabel(item.status)}</td>
                  <td className="px-3 py-4">
                    {item.compliancePercentage === null
                      ? `${item.answeredItems} respuestas`
                      : `${item.compliancePercentage.toFixed(1)}% · ${formatStatusLabel(item.riskLevel ?? '')}`}
                    {item.frequency && (
                      <span className="mt-1 block text-xs font-semibold text-brand-700">
                        Frecuencia {formatStatusLabel(item.frequency)}
                        {item.nextInspectionAt
                          ? ` · Próxima ${new Intl.DateTimeFormat('es-DO', { dateStyle: 'medium' }).format(new Date(item.nextInspectionAt))}`
                          : ''}
                      </span>
                    )}
                  </td>
                  <td className="px-3 py-4 text-right">
                    <div className="flex flex-wrap justify-end gap-2">
                      {item.status === 'ASIGNADA' && canExecute && (
                        <button
                          type="button"
                          onClick={() => void runTransition(item, 'start')}
                          className="rounded-lg bg-brand-700 px-3 py-2 font-bold text-white"
                        >
                          Iniciar
                        </button>
                      )}
                      {canSubmitForReview(item.status, roles) && (
                        <button
                          type="button"
                          onClick={() => void runTransition(item, 'submit')}
                          className="rounded-lg bg-brand-700 px-3 py-2 font-bold text-white"
                        >
                          Enviar a revisión
                        </button>
                      )}
                      {canFinalizeReview(item.status, roles) && (
                        <>
                          <button
                            type="button"
                            onClick={() => void runTransition(item, 'approve')}
                            className="rounded-lg bg-brand-700 px-3 py-2 font-bold text-white"
                          >
                            Aprobar
                          </button>
                          <button
                            type="button"
                            onClick={() => void runTransition(item, 'reject')}
                            className="rounded-lg border border-red-300 px-3 py-2 font-bold text-red-700"
                          >
                            No aprobar
                          </button>
                        </>
                      )}
                      {canCloseEvaluation(item.status, roles) && (
                        <button
                          type="button"
                          onClick={() => void runTransition(item, 'close')}
                          className="rounded-lg border border-slate-300 px-3 py-2 font-bold"
                        >
                          {item.status === 'NO_APROBADA'
                            ? 'Cerrar y programar reinspección'
                            : 'Cerrar expediente'}
                        </button>
                      )}
                      {canEditInspection(item.status, canExecute, item.canEdit) && (
                        <button
                          type="button"
                          onClick={() => void prepareOffline(item)}
                          disabled={preparingOfflineId === item.id}
                          className="border-brand-300 text-brand-800 rounded-lg border px-3 py-2 font-bold disabled:opacity-50"
                        >
                          {preparingOfflineId === item.id
                            ? 'Preparando…'
                            : offlineReadyIds.has(item.id)
                              ? 'Disponible offline'
                              : 'Preparar offline'}
                        </button>
                      )}
                      <button
                        type="button"
                        onClick={() => setSelected(item)}
                        className={`rounded-lg px-3 py-2 font-bold ${
                          canEditInspection(item.status, canExecute, item.canEdit)
                            ? 'bg-brand-700 text-white'
                            : 'border border-slate-300 text-ink-body'
                        }`}
                      >
                        {inspectionActionLabel(item.status, canExecute, item.canEdit)}
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {loading && <TableSkeleton rows={5} columns={6} />}
          {!loading && !error && result.items.length === 0 && (
            <p className="p-10 text-center text-sm text-ink-muted">
              No hay evaluaciones disponibles en su ámbito.
            </p>
          )}
        </div>
        <Pagination
          page={pageNumber}
          pageSize={result.pageSize}
          total={result.total}
          disabled={loading}
          label="evaluaciones"
          onChange={setPageNumber}
        />
      </div>

      {selected && (
        <div className="mt-8 border-t border-slate-200 pt-8">
          <DynamicInspectionForm
            selectedEvaluationId={selected.id}
            onFinalized={() => {
              setSelected((current) => (current ? { ...current, status: 'FINALIZADA' } : current))
              void load()
            }}
            readOnly={!canEditInspection(selected.status, canExecute, selected.canEdit)}
          />
        </div>
      )}
      {creating && options && (
        <EvaluationForm options={options} onClose={() => setCreating(false)} onSave={save} />
      )}
    </section>
  )
}

function EvaluationForm({
  options,
  onClose,
  onSave,
}: {
  options: EvaluationCreateOptions
  onClose: () => void
  onSave: (draft: EvaluationDraft) => Promise<void>
}) {
  const [caseId, setCaseId] = useState('')
  const [establishmentId, setEstablishmentId] = useState('')
  const [inspectionTemplateId, setTemplateId] = useState(options.templates[0]?.id ?? '')
  const [riskRuleVersionId, setRiskRuleId] = useState(options.riskRules[0]?.id ?? '')
  const [evaluatorId, setEvaluatorId] = useState('')
  const [scheduledStart, setStart] = useState('')
  const [scheduledEnd, setEnd] = useState('')
  const [saving, setSaving] = useState(false)
  const companyId = options.cases.find((item) => item.id === caseId)?.companyId
  const establishments = useMemo(
    () => options.establishments.filter((item) => !companyId || item.companyId === companyId),
    [companyId, options.establishments],
  )

  async function submit(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    try {
      await onSave({
        caseId,
        establishmentId,
        inspectionTemplateId,
        evaluatorId,
        riskRuleVersionId,
        scheduledStart: scheduledStart ? new Date(scheduledStart).toISOString() : null,
        scheduledEnd: scheduledEnd ? new Date(scheduledEnd).toISOString() : null,
      })
    } finally {
      setSaving(false)
    }
  }

  const selectClass = 'mt-1.5 min-h-11 w-full rounded-xl border px-3 font-normal'
  return (
    <div
      className="sigersa-modal-overlay fixed inset-0 z-50 grid place-items-center p-4"
      role="presentation"
    >
      <section
        role="dialog"
        aria-modal="true"
        aria-labelledby="evaluation-form-title"
        className="sigersa-modal-panel w-full max-w-2xl p-6"
      >
        <div className="flex justify-between">
          <h2 id="evaluation-form-title" className="text-xl font-extrabold">
            Nueva evaluación
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
              value={caseId}
              onChange={(e) => {
                setCaseId(e.target.value)
                setEstablishmentId('')
              }}
              className={selectClass}
            >
              <option value="">Seleccione</option>
              {options.cases.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.name}
                </option>
              ))}
            </select>
          </label>
          <label className="text-sm font-bold">
            Establecimiento
            <select
              required
              value={establishmentId}
              onChange={(e) => setEstablishmentId(e.target.value)}
              className={selectClass}
            >
              <option value="">Seleccione</option>
              {establishments.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.name}
                </option>
              ))}
            </select>
          </label>
          <label className="text-sm font-bold">
            Técnico
            <select
              required
              value={evaluatorId}
              onChange={(e) => setEvaluatorId(e.target.value)}
              className={selectClass}
            >
              <option value="">Seleccione</option>
              {options.evaluators.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.name}
                </option>
              ))}
            </select>
          </label>
          <label className="text-sm font-bold">
            Ficha publicada
            <select
              required
              value={inspectionTemplateId}
              onChange={(e) => setTemplateId(e.target.value)}
              className={selectClass}
            >
              {options.templates.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.name}
                </option>
              ))}
            </select>
          </label>
          <label className="text-sm font-bold">
            Regla de riesgo
            <select
              required
              value={riskRuleVersionId}
              onChange={(e) => setRiskRuleId(e.target.value)}
              className={selectClass}
            >
              {options.riskRules.map((item) => (
                <option key={item.id} value={item.id}>
                  {item.name}
                </option>
              ))}
            </select>
          </label>
          <label className="text-sm font-bold">
            Inicio
            <input
              type="datetime-local"
              value={scheduledStart}
              onChange={(e) => setStart(e.target.value)}
              className={selectClass}
            />
          </label>
          <label className="text-sm font-bold">
            Fin
            <input
              type="datetime-local"
              min={scheduledStart}
              value={scheduledEnd}
              onChange={(e) => setEnd(e.target.value)}
              className={selectClass}
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
              disabled={saving || options.templates.length === 0 || options.riskRules.length === 0}
              className="rounded-xl bg-brand-700 px-5 py-2.5 font-bold text-white disabled:opacity-50"
            >
              {saving ? 'Guardando…' : 'Crear evaluación'}
            </button>
          </div>
        </form>
      </section>
    </div>
  )
}
