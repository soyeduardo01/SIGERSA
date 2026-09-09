import { useEffect, useMemo, useState } from 'react'
import {
  calculateInspectionRisk,
  getAllItems,
  type AllItem,
  type AllItemScore,
} from '../../lib/api'
import { offlineDb } from '../../offline/database'
import { queueAnswer, queueEvidence } from '../../offline/syncQueue'

const ratingOptions = [
  { code: 'CUMPLE', short: 'C', label: 'Cumple' },
  { code: 'CUMPLE_PARCIAL', short: 'CP', label: 'Cumple parcial' },
  { code: 'NO_CUMPLE', short: 'IT', label: 'Incumplimiento' },
  { code: 'NO_APLICA', short: 'N/A', label: 'No aplica' },
]

export function DynamicInspectionForm() {
  const [items, setItems] = useState<AllItem[]>([])
  const [ratings, setRatings] = useState<Record<number, string>>({})
  const [scores, setScores] = useState<AllItemScore[]>([])
  const [message, setMessage] = useState('Cargando ficha…')
  const [evaluationId] = useState(
    () => localStorage.getItem('sigersa.active-evaluation') || crypto.randomUUID(),
  )

  useEffect(() => {
    localStorage.setItem('sigersa.active-evaluation', evaluationId)
    void loadTemplate()
    async function loadTemplate() {
      const cached = await offlineDb.inspectionTemplates.get('all-items-active')
      if (cached) {
        setItems(cached.definition as AllItem[])
        setMessage('Ficha disponible desde el almacenamiento local.')
      }
      if (!navigator.onLine) return
      try {
        const current = await getAllItems()
        setItems(current)
        setMessage('Ficha actualizada desde el servidor.')
        await offlineDb.inspectionTemplates.put({
          key: 'all-items-active',
          id: 'all-items',
          code: 'BPM_BASE',
          version: 1,
          status: 'PUBLICADA',
          definition: current,
          updatedAt: new Date().toISOString(),
        })
      } catch (error) {
        setMessage(error instanceof Error ? error.message : 'No fue posible cargar la ficha.')
      }
    }
  }, [evaluationId])

  const depthByItem = useMemo(() => calculateDepths(items), [items])
  const answered = Object.keys(ratings).length
  const questions = items.filter((item) => item.sectionType.toUpperCase() === 'I').length

  async function selectRating(item: AllItem, rating: string) {
    setRatings((current) => ({ ...current, [item.items]: rating }))
    await queueAnswer({ evaluationId, itemId: String(item.items), value: rating })
  }

  async function calculate() {
    try {
      setScores(
        await calculateInspectionRisk(
          Object.entries(ratings).map(([item, rating]) => ({ item: Number(item), rating })),
        ),
      )
      setMessage('Riesgo calculado por el backend.')
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'No fue posible calcular el riesgo.')
    }
  }

  async function addEvidence(file: File | undefined) {
    if (!file) return
    const extension = file.name.includes('.') ? `.${file.name.split('.').pop()}` : ''
    const path = `evaluaciones/${evaluationId.replaceAll('-', '')}/${crypto.randomUUID().replaceAll('-', '')}${extension}`
    try {
      await queueEvidence({
        evaluationId,
        bucketName: import.meta.env.VITE_SUPABASE_EVIDENCE_BUCKET || 'evidencias',
        supabasePath: path,
        mimeType: file.type,
        fileName: file.name,
        file,
      })
      setMessage('Evidencia guardada en la cola segura del dispositivo.')
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'La evidencia no pudo guardarse.')
    }
  }

  const rootScores = scores.filter(
    (score) => items.find((item) => item.items === score.item)?.parents === null,
  )

  return (
    <section aria-labelledby="dynamic-form-title">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-sm font-semibold text-brand-700">
            Evaluación {evaluationId.slice(0, 8)}
          </p>
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
        {items.map((item) => {
          const isQuestion = item.sectionType.toUpperCase() === 'I'
          return (
            <article
              key={item.items}
              className={`rounded-xl bg-white p-4 shadow-card ${isQuestion ? '' : 'border-l-4 border-brand-500'}`}
              style={{ marginLeft: `${Math.min(depthByItem[item.items] ?? 0, 3) * 0.75}rem` }}
            >
              {isQuestion ? (
                <fieldset>
                  <legend className="text-sm leading-6 font-semibold text-ink-body">
                    {item.description}
                  </legend>
                  <div className="mt-3 flex flex-wrap gap-2">
                    {ratingOptions.map((option) => (
                      <label key={option.code} className="cursor-pointer">
                        <input
                          className="peer sr-only"
                          type="radio"
                          name={`rating-${item.items}`}
                          value={option.code}
                          checked={ratings[item.items] === option.code}
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
                <h2 className="font-bold text-ink-strong">{item.description}</h2>
              )}
            </article>
          )
        })}
      </div>

      <div className="sticky bottom-3 mt-6 flex flex-wrap items-center gap-3 rounded-xl bg-surface-inverse p-4 text-white shadow-card">
        <button
          type="button"
          onClick={() => void calculate()}
          className="min-h-11 rounded-xl bg-brand-500 px-4 font-bold"
        >
          Calcular riesgo
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
        {rootScores.map((score) => (
          <span key={score.item} className="rounded-lg bg-white/10 px-3 py-2 text-sm">
            {score.itemsId}:{' '}
            {score.isCalculable ? `${Math.round((score.score ?? 0) * 100)}%` : 'No calculable'}
          </span>
        ))}
      </div>
    </section>
  )
}

function calculateDepths(items: AllItem[]) {
  const parentByCode = new Map(items.map((item) => [item.itemsId, item.parents]))
  return Object.fromEntries(
    items.map((item) => {
      let depth = 0
      let cursor = item.parents
      const visited = new Set<string>()
      while (cursor && visited.add(cursor)) {
        depth += 1
        cursor = parentByCode.get(cursor) ?? null
      }
      return [item.items, depth]
    }),
  )
}
