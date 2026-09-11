import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { TableSkeleton } from '../../components/feedback/Skeletons'
import {
  createEvaluation,
  getEvaluationOptions,
  getEvaluations,
  type EvaluationCreateOptions,
  type EvaluationDraft,
  type EvaluationSummary,
  type EvaluationsPage,
} from '../../lib/api'
import { alerts } from '../../lib/alerts'
import { formatStatusLabel } from '../../lib/formatters'
import { useParameterOptions } from '../../hooks/useParameterOptions'
import { DynamicInspectionForm } from '../inspection/DynamicInspectionForm'

const emptyPage: EvaluationsPage = { items: [], page: 1, pageSize: 20, total: 0 }

export function EvaluationsManagement({
  mode = 'management',
}: {
  mode?: 'management' | 'inspection'
}) {
  const [result, setResult] = useState(emptyPage)
  const [options, setOptions] = useState<EvaluationCreateOptions | null>(null)
  const [status, setStatus] = useState('')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [creating, setCreating] = useState(false)
  const [selected, setSelected] = useState<EvaluationSummary | null>(null)
  const evaluationStates = useParameterOptions('ESTADO_EVALUACION')
  const inspectionMode = mode === 'inspection'

  async function load() {
    setLoading(true)
    try {
      setResult(await getEvaluations({ status }))
      setError('')
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'No se pudieron cargar las evaluaciones.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    let active = true
    void Promise.all([getEvaluations({ status }), getEvaluationOptions()])
      .then(([page, choices]) => {
        if (!active) return
        setResult(page)
        setOptions(choices)
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
  }, [status])

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

  return (
    <section aria-labelledby="evaluations-title">
      <div className="flex flex-col justify-between gap-4 md:flex-row md:items-end">
        <div>
          <p className="text-xs font-bold tracking-[0.14em] text-brand-700 uppercase">
            {inspectionMode ? 'Ejecución de campo' : 'Inspección BPM'}
          </p>
          <h1 id="evaluations-title" className="mt-2 text-2xl font-extrabold">
            {inspectionMode ? 'Inspecciones' : 'Evaluaciones'}
          </h1>
          <p className="mt-2 text-sm text-ink-muted">
            {inspectionMode
              ? 'Seleccione una evaluación asignada y comience la inspección del establecimiento.'
              : 'Consulte sus asignaciones y abra la ficha inmutable autorizada.'}
          </p>
        </div>
        {!inspectionMode && options?.canCreate && (
          <button
            type="button"
            onClick={() => setCreating(true)}
            className="min-h-11 rounded-xl bg-brand-700 px-5 font-bold text-white"
          >
            + Nueva evaluación
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
                  </td>
                  <td className="px-3 py-4 text-right">
                    <button
                      type="button"
                      onClick={() => setSelected(item)}
                      className="rounded-lg border px-3 py-2 font-bold"
                    >
                      {inspectionMode ? 'Iniciar inspección' : 'Abrir ficha'}
                    </button>
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
      </div>

      {selected && (
        <div className="mt-8 border-t border-slate-200 pt-8">
          <DynamicInspectionForm selectedEvaluationId={selected.id} />
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
