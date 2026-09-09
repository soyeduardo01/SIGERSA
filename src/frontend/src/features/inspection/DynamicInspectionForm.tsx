import { useEffect, useMemo, useState, type FormEvent } from 'react'
import {
  calculateEvaluation,
  getEvaluationForm,
  type EvaluationCalculation,
  type EvaluationFormItem,
} from '../../lib/api'
import { offlineDb } from '../../offline/database'
import { flushSyncQueue, queueAnswer, queueEvidence } from '../../offline/syncQueue'

const ratingOptions = [
  { code: 'CUMPLE', short: 'C', label: 'Cumple' },
  { code: 'CUMPLE_PARCIAL', short: 'CP', label: 'Cumple parcial' },
  { code: 'NO_CUMPLE', short: 'IT', label: 'Incumplimiento' },
  { code: 'NO_APLICA', short: 'N/A', label: 'No aplica' },
]

const uuidPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i

export function DynamicInspectionForm() {
  const [evaluationInput, setEvaluationInput] = useState(
    () => localStorage.getItem('sigersa.active-evaluation') ?? '',
  )
  const [evaluationId, setEvaluationId] = useState(() => {
    const stored = localStorage.getItem('sigersa.active-evaluation') ?? ''
    return uuidPattern.test(stored) ? stored : ''
  })
  const [items, setItems] = useState<EvaluationFormItem[]>([])
  const [ratings, setRatings] = useState<Record<number, string>>({})
  const [calculation, setCalculation] = useState<EvaluationCalculation | null>(null)
  const [productRisk, setProductRisk] = useState(1)
  const [message, setMessage] = useState('Indique una evaluación asignada para cargar su ficha.')

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
      }
    }
  }, [evaluationId])

  const answered = Object.keys(ratings).length
  const questions = useMemo(() => items.filter((item) => item.isEvaluable).length, [items])

  function activateEvaluation(event: FormEvent) {
    event.preventDefault()
    const normalized = evaluationInput.trim()
    if (!uuidPattern.test(normalized)) {
      setMessage('Ingrese el identificador UUID válido de una evaluación existente.')
      return
    }
    localStorage.setItem('sigersa.active-evaluation', normalized)
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
    await queueAnswer({ evaluationId, itemId: String(item.sourceItem), value: rating })
    setMessage(
      navigator.onLine
        ? 'Respuesta guardada en la cola de sincronización.'
        : 'Respuesta guardada localmente; se sincronizará al recuperar conexión.',
    )
  }

  async function calculate() {
    if (!evaluationId || !navigator.onLine) {
      setMessage('El cálculo definitivo requiere conexión con el backend.')
      return
    }
    try {
      await flushSyncQueue()
      const result = await calculateEvaluation(evaluationId, productRisk)
      setCalculation(result)
      setMessage('Respuestas sincronizadas y riesgo calculado por el backend.')
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'No fue posible calcular el riesgo.')
    }
  }

  async function addEvidence(file: File | undefined) {
    if (!file || !evaluationId) return
    try {
      await queueEvidence({
        evaluationId,
        mimeType: file.type,
        fileName: file.name,
        evidenceType: 'FOTOGRAFIA',
        file,
      })
      setMessage('Evidencia validada y guardada en la cola segura del dispositivo.')
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'La evidencia no pudo guardarse.')
    }
  }

  return (
    <section aria-labelledby="dynamic-form-title">
      <div className="rounded-xl bg-white p-4 shadow-card">
        <form className="flex flex-wrap items-end gap-3" onSubmit={activateEvaluation}>
          <label className="min-w-72 flex-1 text-sm font-bold text-ink-body">
            Identificador de evaluación asignada
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

      <div className="mt-6 space-y-3">
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
              value={productRisk}
              onChange={(event) => setProductRisk(Number(event.target.value))}
              className="ml-2 min-h-11 rounded-lg bg-white px-3 text-ink-strong"
            >
              <option value={1}>1 · Bajo</option>
              <option value={2}>2 · Medio</option>
              <option value={3}>3 · Alto</option>
            </select>
          </label>
          <button
            type="button"
            onClick={() => void calculate()}
            className="min-h-11 rounded-xl bg-brand-500 px-4 font-bold"
          >
            Sincronizar y calcular
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
