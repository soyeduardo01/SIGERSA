import { useEffect, useMemo, useState, type FormEvent } from 'react'
import { FormSkeleton } from '../../components/feedback/Skeletons'
import { useParameterOptions } from '../../hooks/useParameterOptions'
import {
  calculateEvaluation,
  finalizeEvaluation,
  getEvaluationForm,
  type EvaluationCalculation,
  type EvaluationFormItem,
} from '../../lib/api'
import { offlineDb } from '../../offline/database'
import { flushSyncQueue, queueAnswer, queueEvidence } from '../../offline/syncQueue'
import { alerts } from '../../lib/alerts'
import { getOptionalLocation } from '../../lib/geolocation'
import { formatStatusLabel } from '../../lib/formatters'

const ratingOptions = [
  { code: 'CUMPLE', short: 'C', label: 'Cumple' },
  { code: 'CUMPLE_PARCIAL', short: 'CP', label: 'Cumple parcialmente' },
  { code: 'NO_CUMPLE', short: 'NC', label: 'No cumple' },
  { code: 'NO_APLICA', short: 'N/A', label: 'No aplica' },
]

const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i

export function DynamicInspectionForm({
  selectedEvaluationId,
  onFinalized,
}: {
  selectedEvaluationId?: string
  onFinalized?: () => void
}) {
  const [evaluationInput, setEvaluationInput] = useState(
    () => localStorage.getItem('sigersa.active-evaluation') ?? '',
  )
  const [evaluationId, setEvaluationId] = useState(() => {
    const stored = localStorage.getItem('sigersa.active-evaluation') ?? ''
    return uuidPattern.test(stored) ? stored : ''
  })
  const [items, setItems] = useState<EvaluationFormItem[]>([])
  const [ratings, setRatings] = useState<Record<number, string>>({})
  const [observations, setObservations] = useState<Record<number, string>>({})
  const [comments, setComments] = useState<Record<number, string>>({})
  const [calculation, setCalculation] = useState<EvaluationCalculation | null>(null)
  const [productRisk, setProductRisk] = useState<number | null>(null)
  const productRiskOptions = useParameterOptions('NIVEL_RIESGO_ALIMENTO')
  const [message, setMessage] = useState('Indique una evaluación asignada para cargar su ficha.')
  const [loadingForm, setLoadingForm] = useState(() => {
    const stored = localStorage.getItem('sigersa.active-evaluation') ?? ''
    return uuidPattern.test(stored)
  })

  useEffect(() => {
    if (!selectedEvaluationId || !uuidPattern.test(selectedEvaluationId)) return
    localStorage.setItem('sigersa.active-evaluation', selectedEvaluationId)
    queueMicrotask(() => {
      setEvaluationInput(selectedEvaluationId)
      setLoadingForm(true)
      setEvaluationId(selectedEvaluationId)
    })
  }, [selectedEvaluationId])

  useEffect(() => {
    if (!evaluationId) return
    void loadEvaluation(evaluationId)

    async function loadEvaluation(id: string) {
      const cacheKey = `evaluation-${id}`
      const cached = await offlineDb.inspectionTemplates.get(cacheKey)
      if (cached) {
        setItems(cached.definition as EvaluationFormItem[])
        setMessage('Ficha inmutable disponible desde el almacenamiento local.')
      }
      if (!navigator.onLine) {
        if (!cached) setMessage('Esta evaluación todavía no está disponible sin conexión.')
        setLoadingForm(false)
        return
      }
      try {
        const form = await getEvaluationForm(id)
        setItems(form)
        setRatings({})
        setCalculation(null)
        setMessage('Ficha asignada actualizada desde el servidor.')
        await offlineDb.inspectionTemplates.put({
          key: cacheKey,
          id,
          code: 'EVALUATION_SNAPSHOT',
          version: 1,
          status: 'PUBLICADA',
          definition: form,
          updatedAt: new Date().toISOString(),
        })
      } catch (error) {
        setMessage(
          error instanceof Error ? error.message : 'No fue posible cargar la evaluación asignada.',
        )
        await alerts.error(error, 'No se pudo cargar la evaluación')
      } finally {
        setLoadingForm(false)
      }
    }
  }, [evaluationId])

  useEffect(() => {
    if (productRisk !== null) return
    const firstValue = productRiskOptions.options.find(
      (option) => option.numericData !== null,
    )?.numericData
    if (firstValue !== undefined && firstValue !== null) {
      queueMicrotask(() => setProductRisk(firstValue))
    }
  }, [productRisk, productRiskOptions.options])

  const answered = Object.keys(ratings).length
  const questions = useMemo(() => items.filter((item) => item.isEvaluable).length, [items])

  function activateEvaluation(event: FormEvent) {
    event.preventDefault()
    const normalized = evaluationInput.trim()
    if (!uuidPattern.test(normalized)) {
      setMessage('Ingrese el identificador UUID válido de una evaluación existente.')
      void alerts.error(
        new Error('Ingrese el identificador UUID válido de una evaluación existente.'),
        'Identificador no válido',
      )
      return
    }
    localStorage.setItem('sigersa.active-evaluation', normalized)
    setLoadingForm(true)
    if (normalized === evaluationId) {
      setEvaluationId('')
      queueMicrotask(() => setEvaluationId(normalized))
    } else {
      setEvaluationId(normalized)
    }
  }

  async function selectRating(item: EvaluationFormItem, rating: string) {
    if (!evaluationId) return
    setRatings((current) => ({ ...current, [item.sourceItem]: rating }))
    await queueAnswer({
      evaluationId,
      itemId: String(item.sourceItem),
      value: rating,
      observation: observations[item.sourceItem]?.trim() || undefined,
      comment: comments[item.sourceItem]?.trim() || undefined,
    })
    setMessage(
      navigator.onLine
        ? 'Respuesta guardada en la cola de sincronización.'
        : 'Respuesta guardada localmente; se sincronizará al recuperar conexión.',
    )
  }

  async function saveAnswerDetails(item: EvaluationFormItem) {
    const rating = ratings[item.sourceItem]
    if (!evaluationId || !rating) return
    await queueAnswer({
      evaluationId,
      itemId: String(item.sourceItem),
      value: rating,
      observation: observations[item.sourceItem]?.trim() || undefined,
      comment: comments[item.sourceItem]?.trim() || undefined,
    })
    setMessage('Detalle de la respuesta guardado para sincronización.')
  }

  async function calculate() {
    if (!evaluationId || productRisk === null || !navigator.onLine) {
      setMessage('El cálculo definitivo requiere conexión con el backend.')
      await alerts.error(
        new Error('Conéctese a internet y cargue una evaluación antes de calcular.'),
        'No es posible calcular ahora',
      )
      return
    }
    try {
      await flushSyncQueue()
      const result = await calculateEvaluation(evaluationId, productRisk)
      setCalculation(result)
      setMessage('Respuestas sincronizadas y riesgo calculado por el backend.')
      await alerts.success(
        'Evaluación calculada',
        'Las respuestas se sincronizaron y el riesgo fue actualizado.',
      )
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'No fue posible calcular el riesgo.')
      await alerts.error(error, 'No se pudo calcular el riesgo')
    }
  }

  async function finalize() {
    if (!evaluationId || productRisk === null || !navigator.onLine) {
      await alerts.error(
        new Error('Conéctese y seleccione el riesgo del producto.'),
        'No es posible finalizar',
      )
      return
    }
    const confirmed = await alerts.confirm({
      title: '¿Finalizar la evaluación?',
      text: 'Se sincronizarán las respuestas, se calculará el riesgo y la ficha quedará lista para envío.',
      confirmText: 'Finalizar',
    })
    if (!confirmed) return
    try {
      await flushSyncQueue()
      const result = await finalizeEvaluation(evaluationId, productRisk)
      setCalculation(result)
      setMessage('Evaluación finalizada y cálculo de riesgo almacenado.')
      await alerts.success('Evaluación finalizada')
      onFinalized?.()
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'No fue posible finalizar la evaluación.')
      await alerts.error(error, 'No se pudo finalizar la evaluación')
    }
  }

  async function addEvidence(file: File | undefined, sourceItem?: number) {
    if (!file || !evaluationId) return
    try {
      const location = await getOptionalLocation()
      await queueEvidence({
        evaluationId,
        sourceItem,
        mimeType: file.type,
        fileName: file.name,
        evidenceType: 'FOTOGRAFIA',
        file,
        ...location,
      })
      setMessage('Evidencia validada y guardada en la cola segura del dispositivo.')
      await alerts.success(
        'Evidencia guardada',
        'El archivo quedó preparado para su sincronización segura.',
      )
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'La evidencia no pudo guardarse.')
      await alerts.error(error, 'No se pudo guardar la evidencia')
    }
  }

  return (
    <section aria-labelledby="dynamic-form-title">
      <div className="rounded-xl bg-white p-4 shadow-card">
        <div className="mb-4 rounded-xl bg-brand-50 p-4 text-sm leading-6 text-ink-body">
          <strong className="text-brand-900">¿Cómo completar esta ficha?</strong>
          <ol className="mt-2 grid gap-2 pl-5 marker:font-bold md:grid-cols-3">
            <li>Cargue el identificador de la evaluación asignada.</li>
            <li>Responda cada pregunta siguiendo los títulos jerárquicos.</li>
            <li>Adjunte evidencias y pulse “Sincronizar y calcular”.</li>
          </ol>
        </div>
        {!selectedEvaluationId && (
          <form className="flex flex-wrap items-end gap-3" onSubmit={activateEvaluation}>
            <label className="min-w-72 flex-1 text-sm font-bold text-ink-body">
              Identificador de evaluación asignada
              <span
                className="ml-1 cursor-help text-brand-700"
                title="Este código UUID se encuentra en la asignación de la inspección."
                aria-label="Ayuda sobre el identificador"
              >
                ⓘ
              </span>
              <input
                required
                value={evaluationInput}
                onChange={(event) => setEvaluationInput(event.target.value)}
                placeholder="00000000-0000-0000-0000-000000000000"
                className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
              />
            </label>
            <button
              type="submit"
              className="min-h-11 rounded-xl bg-brand-700 px-4 font-bold text-white"
            >
              Cargar evaluación
            </button>
          </form>
        )}
      </div>

      <div className="mt-5 flex flex-wrap items-end justify-between gap-4">
        <div>
          {evaluationId && (
            <p className="text-sm font-semibold text-brand-700">
              Evaluación {evaluationId.slice(0, 8)}
            </p>
          )}
          <h1 id="dynamic-form-title" className="mt-1 text-2xl font-extrabold text-ink-strong">
            Ficha BPM dinámica
          </h1>
          <p className="mt-2 text-sm text-ink-muted" role="status">
            {message}
          </p>
        </div>
        <div className="rounded-xl bg-white px-4 py-3 text-sm shadow-card">
          <strong>{answered}</strong> de <strong>{questions}</strong> preguntas respondidas
        </div>
      </div>

      {loadingForm && (
        <div className="mt-6 rounded-card bg-white p-5 shadow-card">
          <FormSkeleton />
        </div>
      )}
      <div className="mt-6 space-y-3" aria-busy={loadingForm}>
        {items.map((item) => (
          <article
            key={item.id}
            className={`rounded-xl bg-white p-4 shadow-card ${item.isEvaluable ? '' : 'border-l-4 border-brand-500'}`}
            style={{ marginLeft: `${Math.min(item.level, 3) * 0.75}rem` }}
          >
            {item.isEvaluable ? (
              <fieldset>
                <legend className="text-sm leading-6 font-semibold text-ink-body">
                  {item.title}
                </legend>
                <div className="mt-3 flex flex-wrap gap-2">
                  {ratingOptions.map((option) => (
                    <label key={option.code} className="cursor-pointer">
                      <input
                        className="peer sr-only"
                        type="radio"
                        name={`rating-${item.id}`}
                        value={option.code}
                        checked={ratings[item.sourceItem] === option.code}
                        onChange={() => void selectRating(item, option.code)}
                      />
                      <span className="inline-flex min-h-10 items-center rounded-lg border border-slate-300 px-3 text-sm font-bold peer-checked:border-brand-700 peer-checked:bg-brand-100 peer-checked:text-brand-900">
                        {option.short} · {option.label}
                      </span>
                    </label>
                  ))}
                </div>
                <div className="mt-3 grid gap-3 md:grid-cols-2">
                  <label className="text-xs font-bold text-ink-muted">
                    Observación
                    <textarea
                      rows={2}
                      value={observations[item.sourceItem] ?? ''}
                      onChange={(event) =>
                        setObservations((current) => ({
                          ...current,
                          [item.sourceItem]: event.target.value,
                        }))
                      }
                      onBlur={() => void saveAnswerDetails(item)}
                      className="mt-1 w-full rounded-lg border border-slate-300 p-2 text-sm font-normal text-ink-body"
                      placeholder="Detalle verificable observado durante la inspección"
                    />
                  </label>
                  <label className="text-xs font-bold text-ink-muted">
                    Comentario técnico
                    <textarea
                      rows={2}
                      value={comments[item.sourceItem] ?? ''}
                      onChange={(event) =>
                        setComments((current) => ({
                          ...current,
                          [item.sourceItem]: event.target.value,
                        }))
                      }
                      onBlur={() => void saveAnswerDetails(item)}
                      className="mt-1 w-full rounded-lg border border-slate-300 p-2 text-sm font-normal text-ink-body"
                      placeholder="Comentario complementario o recomendación"
                    />
                  </label>
                </div>
                <label className="mt-3 inline-flex min-h-10 cursor-pointer items-center rounded-lg border border-brand-200 px-3 text-sm font-bold text-brand-800 hover:bg-brand-50">
                  Adjuntar evidencia a este criterio
                  <input
                    type="file"
                    className="sr-only"
                    accept="image/jpeg,image/png,application/pdf,video/mp4"
                    onChange={(event) =>
                      void addEvidence(event.target.files?.[0], item.sourceItem)
                    }
                  />
                </label>
              </fieldset>
            ) : (
              <h2 className="font-bold text-ink-strong">{item.title}</h2>
            )}
          </article>
        ))}
      </div>

      {items.length > 0 && (
        <div className="sticky bottom-3 mt-6 flex flex-wrap items-center gap-3 rounded-xl bg-surface-inverse p-4 text-white shadow-card">
          <label className="text-sm font-bold">
            Riesgo del producto
            <select
              value={productRisk ?? ''}
              onChange={(event) =>
                setProductRisk(event.target.value ? Number(event.target.value) : null)
              }
              className="ml-2 min-h-11 rounded-lg bg-white px-3 text-ink-strong"
            >
              <option value="">Seleccione</option>
              {productRiskOptions.options
                .filter((option) => option.numericData !== null)
                .map((option) => (
                  <option key={option.parametersId} value={option.numericData ?? ''}>
                    {option.numericData} · {formatStatusLabel(option.stringData ?? '')}
                  </option>
                ))}
            </select>
          </label>
          {!productRiskOptions.loading && productRiskOptions.options.length === 0 && (
            <span className="text-xs text-amber-200">
              El catálogo NIVEL_RIESGO_ALIMENTO no tiene valores activos.
            </span>
          )}
          <button
            type="button"
            onClick={() => void calculate()}
            disabled={productRisk === null}
            className="min-h-11 rounded-xl bg-brand-500 px-4 font-bold disabled:cursor-not-allowed disabled:opacity-50"
          >
            Sincronizar y calcular
          </button>
          <button
            type="button"
            onClick={() => void finalize()}
            disabled={productRisk === null}
            className="min-h-11 rounded-xl bg-white px-4 font-bold text-brand-900 disabled:cursor-not-allowed disabled:opacity-50"
          >
            Finalizar evaluación
          </button>
          <label className="inline-flex min-h-11 cursor-pointer items-center rounded-xl border border-white/30 px-4 font-bold">
            Adjuntar evidencia
            <input
              type="file"
              className="sr-only"
              accept="image/jpeg,image/png,application/pdf,video/mp4"
              onChange={(event) => void addEvidence(event.target.files?.[0])}
            />
          </label>
          {calculation && (
            <output className="rounded-lg bg-white/10 px-3 py-2 text-sm">
              Cumplimiento {calculation.compliancePercentage?.toFixed(1) ?? 'N/D'}% · Riesgo{' '}
              {calculation.totalRisk?.toFixed(2) ?? 'N/D'} · {calculation.riskLevel}
            </output>
          )}
        </div>
      )}
    </section>
  )
}
