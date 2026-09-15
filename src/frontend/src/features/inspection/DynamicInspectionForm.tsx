import { useEffect, useMemo, useRef, useState, type FormEvent } from 'react'
import { FormSkeleton } from '../../components/feedback/Skeletons'
import { useParameterOptions } from '../../hooks/useParameterOptions'
import {
  calculateEvaluation,
  finalizeEvaluation,
  getEvaluationWorkspace,
  saveEvaluationSupplement,
  type EvaluationCalculation,
  type EvaluationFollowUpItem,
  type EvaluationFormItem,
  type EvaluationInspectionPolicy,
  type EvaluationSupplement,
} from '../../lib/api'
import { offlineDb } from '../../offline/database'
import {
  flushSyncQueue,
  queueAnswer,
  queueEvidence,
  retryEvaluationMutations,
} from '../../offline/syncQueue'
import { alerts } from '../../lib/alerts'
import { getOptionalLocation } from '../../lib/geolocation'
import { formatStatusLabel } from '../../lib/formatters'
import {
  groupItemsByChapter,
  inspectionCriteria,
  isValidEvaluationId,
  itemsForInspectionPolicy,
  qualificationForPercentage,
  qualificationOptions,
  scoringCriteria,
} from './evaluationWorkspace'

const ratingOptions = [
  { code: 'CUMPLE', short: 'C', label: 'Cumple', score: '1 punto' },
  { code: 'CUMPLE_PARCIAL', short: 'CP', label: 'Cumplimiento parcial', score: '0.5 puntos' },
  { code: 'NO_CUMPLE', short: 'IT', label: 'Incumplimiento Total', score: '0 puntos' },
  { code: 'NO_APLICA', short: 'N/A', label: 'No aplica', score: 'No se calcula' },
]
const criticalityOptions = [
  { code: 'C', label: 'No Conformidad Crítica' },
  { code: 'M', label: 'No Conformidad Mayor' },
  { code: 'ME', short: 'Me', label: 'No Conformidad Menor' },
]
function today() {
  const date = new Date()
  return new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 10)
}

function emptySupplement(): EvaluationSupplement {
  return {
    previousInspectionDate: null,
    previousQualification: null,
    currentInspectionDate: today(),
    currentQualification: null,
    dpsDasOfficer1: null,
    dpsDasOfficer2: null,
    digemapsTechnician1: null,
    digemapsTechnician2: null,
    correctiveMeasures: [],
    recommendations: [],
    rowVersion: 0,
  }
}

export function DynamicInspectionForm({
  selectedEvaluationId,
  onFinalized,
  readOnly = false,
}: {
  selectedEvaluationId?: string
  onFinalized?: () => void
  readOnly?: boolean
}) {
  const [evaluationInput, setEvaluationInput] = useState(
    () =>
      (selectedEvaluationId && isValidEvaluationId(selectedEvaluationId)
        ? selectedEvaluationId
        : localStorage.getItem('sigersa.active-evaluation')) ?? '',
  )
  const [evaluationId, setEvaluationId] = useState(() => {
    const candidate =
      selectedEvaluationId && isValidEvaluationId(selectedEvaluationId)
        ? selectedEvaluationId
        : (localStorage.getItem('sigersa.active-evaluation') ?? '')
    return isValidEvaluationId(candidate) ? candidate : ''
  })
  const [items, setItems] = useState<EvaluationFormItem[]>([])
  const [policy, setPolicy] = useState<EvaluationInspectionPolicy | null>(null)
  const [ratings, setRatings] = useState<Record<number, string>>({})
  const [criticalities, setCriticalities] = useState<Record<number, string>>({})
  const [observations, setObservations] = useState<Record<number, string>>({})
  const [comments, setComments] = useState<Record<number, string>>({})
  const [answerStatus, setAnswerStatus] = useState<Record<number, string>>({})
  const [evidenceStatus, setEvidenceStatus] = useState<Record<number, string>>({})
  const [supplement, setSupplement] = useState<EvaluationSupplement>(emptySupplement)
  const [savingSupplement, setSavingSupplement] = useState(false)
  const [calculation, setCalculation] = useState<EvaluationCalculation | null>(null)
  const [productRisk, setProductRisk] = useState<number | null>(null)
  const [activeChapter, setActiveChapter] = useState(0)
  const chapterTopRef = useRef<HTMLDivElement>(null)
  const productRiskOptions = useParameterOptions('NIVEL_RIESGO_ALIMENTO')
  const [message, setMessage] = useState('Indique una evaluación asignada para cargar su ficha.')
  const [loadingForm, setLoadingForm] = useState(() => {
    const candidate =
      selectedEvaluationId ?? localStorage.getItem('sigersa.active-evaluation') ?? ''
    return isValidEvaluationId(candidate)
  })

  useEffect(() => {
    if (!selectedEvaluationId || !isValidEvaluationId(selectedEvaluationId)) return
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
        const cachedDefinition = cached.definition as
          EvaluationFormItem[] | { items: EvaluationFormItem[]; policy: EvaluationInspectionPolicy }
        if (Array.isArray(cachedDefinition)) {
          setItems(cachedDefinition)
          setPolicy(null)
        } else {
          setItems(cachedDefinition.items)
          setPolicy(cachedDefinition.policy)
        }
        setMessage('Ficha disponible desde el almacenamiento local.')
      }
      if (!navigator.onLine) {
        if (!cached) setMessage('Esta evaluación todavía no está disponible sin conexión.')
        setLoadingForm(false)
        return
      }
      try {
        const workspace = await getEvaluationWorkspace(id)
        setItems(workspace.items)
        setPolicy(workspace.policy)
        setRatings(
          Object.fromEntries(workspace.answers.map((answer) => [answer.sourceItem, answer.rating])),
        )
        setCriticalities(
          Object.fromEntries(
            workspace.answers
              .filter((answer) => answer.criticalityCode)
              .map((answer) => [answer.sourceItem, answer.criticalityCode as string]),
          ),
        )
        setObservations(
          Object.fromEntries(
            workspace.answers.map((answer) => [answer.sourceItem, answer.observation ?? '']),
          ),
        )
        setComments(
          Object.fromEntries(
            workspace.answers.map((answer) => [answer.sourceItem, answer.comment ?? '']),
          ),
        )
        setSupplement({
          ...workspace.supplement,
          previousInspectionDate:
            workspace.supplement.previousInspectionDate ?? workspace.policy.previousInspectionDate,
          previousQualification:
            workspace.supplement.previousQualification ??
            qualificationForPercentage(workspace.policy.previousCompliancePercentage),
          currentInspectionDate: workspace.supplement.currentInspectionDate ?? today(),
        })
        setAnswerStatus(
          Object.fromEntries(workspace.answers.map((answer) => [answer.sourceItem, 'Guardado'])),
        )
        setActiveChapter(0)
        setCalculation(null)
        setMessage('Ficha y respuestas actualizadas desde la base de datos.')
        await offlineDb.inspectionTemplates.put({
          key: cacheKey,
          id,
          code: 'EVALUATION_SNAPSHOT',
          version: 1,
          status: 'PUBLICADA',
          definition: { items: workspace.items, policy: workspace.policy },
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
    if (firstValue !== undefined && firstValue !== null)
      queueMicrotask(() => setProductRisk(firstValue))
  }, [productRisk, productRiskOptions.options])

  const visibleItems = useMemo(() => itemsForInspectionPolicy(items, policy), [items, policy])
  const requiredSources = useMemo(() => {
    if (!policy)
      return new Set(items.filter((item) => item.isEvaluable).map((item) => item.sourceItem))
    if (policy.mode === 'DENUNCIA_DISCRECIONAL')
      return new Set(
        items
          .filter((item) => item.isEvaluable && Boolean(ratings[item.sourceItem]))
          .map((item) => item.sourceItem),
      )
    return new Set(policy.requiredSourceItems)
  }, [items, policy, ratings])
  const answered = items.filter(
    (item) =>
      item.isEvaluable &&
      requiredSources.has(item.sourceItem) &&
      ratings[item.sourceItem] &&
      (ratings[item.sourceItem] !== 'NO_CUMPLE' || criticalities[item.sourceItem]),
  ).length
  const questions = requiredSources.size
  const chapters = useMemo(() => groupItemsByChapter(visibleItems), [visibleItems])
  const currentChapter = chapters[Math.min(activeChapter, Math.max(chapters.length - 1, 0))]
  const policyReady =
    policy?.mode === 'DENUNCIA_DISCRECIONAL' ? questions > 0 : (policy?.isReady ?? true)
  const formComplete = policyReady && questions > 0 && answered === questions

  function activateEvaluation(event: FormEvent) {
    event.preventDefault()
    const normalized = evaluationInput.trim()
    if (!isValidEvaluationId(normalized)) {
      setMessage('Ingrese el identificador UUID válido de una evaluación existente.')
      void alerts.error(new Error('Ingrese un UUID válido.'), 'Identificador no válido')
      return
    }
    localStorage.setItem('sigersa.active-evaluation', normalized)
    setLoadingForm(true)
    if (normalized === evaluationId) {
      setEvaluationId('')
      queueMicrotask(() => setEvaluationId(normalized))
    } else setEvaluationId(normalized)
  }

  function preserveViewport(update: () => void) {
    const left = window.scrollX
    const top = window.scrollY
    update()
    requestAnimationFrame(() => window.scrollTo({ left, top, behavior: 'auto' }))
  }

  async function persistAnswer(item: EvaluationFormItem, rating: string, criticality?: string) {
    if (!evaluationId) return
    setAnswerStatus((current) => ({ ...current, [item.sourceItem]: 'Guardando…' }))
    const key = await queueAnswer({
      evaluationId,
      itemId: String(item.sourceItem),
      value: rating,
      criticalityCode: rating === 'NO_CUMPLE' ? criticality : undefined,
      observation: observations[item.sourceItem]?.trim() || undefined,
      comment: comments[item.sourceItem]?.trim() || undefined,
    })
    if (!navigator.onLine) {
      setAnswerStatus((current) => ({ ...current, [item.sourceItem]: 'Pendiente sin conexión' }))
      setMessage(
        'Respuesta protegida localmente; se guardará en la base de datos al recuperar conexión.',
      )
      return
    }
    await flushSyncQueue()
    const pending = await offlineDb.syncQueue.get(key)
    if (pending) throw new Error(pending.lastError ?? 'La respuesta aún no pudo sincronizarse.')
    setAnswerStatus((current) => ({
      ...current,
      [item.sourceItem]: 'Guardado en la base de datos',
    }))
    setMessage('Respuesta guardada en la base de datos.')
  }

  async function selectRating(item: EvaluationFormItem, rating: string) {
    preserveViewport(() => {
      setRatings((current) => ({ ...current, [item.sourceItem]: rating }))
      if (rating !== 'NO_CUMPLE') {
        setCriticalities((current) => {
          const next = { ...current }
          delete next[item.sourceItem]
          return next
        })
      }
    })
    if (rating === 'NO_CUMPLE' && !criticalities[item.sourceItem]) {
      setAnswerStatus((current) => ({
        ...current,
        [item.sourceItem]: 'Seleccione el nivel de criticidad',
      }))
      return
    }
    try {
      await persistAnswer(item, rating, criticalities[item.sourceItem])
    } catch (error) {
      setAnswerStatus((current) => ({ ...current, [item.sourceItem]: 'Error al guardar' }))
      setMessage(error instanceof Error ? error.message : 'La respuesta no pudo guardarse.')
    }
  }

  async function selectCriticality(item: EvaluationFormItem, criticality: string) {
    preserveViewport(() =>
      setCriticalities((current) => ({ ...current, [item.sourceItem]: criticality })),
    )
    try {
      await persistAnswer(item, 'NO_CUMPLE', criticality)
    } catch (error) {
      setAnswerStatus((current) => ({ ...current, [item.sourceItem]: 'Error al guardar' }))
      setMessage(error instanceof Error ? error.message : 'La criticidad no pudo guardarse.')
    }
  }

  async function saveAnswerDetails(item: EvaluationFormItem) {
    const rating = ratings[item.sourceItem]
    if (!rating) return
    const criticality = criticalities[item.sourceItem]
    if (rating === 'NO_CUMPLE' && !criticality) return
    try {
      await persistAnswer(item, rating, criticality)
    } catch (error) {
      setAnswerStatus((current) => ({ ...current, [item.sourceItem]: 'Error al guardar' }))
      setMessage(error instanceof Error ? error.message : 'El detalle no pudo guardarse.')
    }
  }

  async function ensureEvaluationSynced() {
    if (evaluationId) await retryEvaluationMutations(evaluationId)
    await flushSyncQueue()
    const pending = (await offlineDb.syncQueue.toArray()).find((item) => {
      if (item.kind !== 'answer' && item.kind !== 'evidence') return false
      return 'evaluationId' in item.payload && item.payload.evaluationId === evaluationId
    })
    if (pending)
      throw new Error(pending.lastError ?? 'Quedan respuestas o evidencias pendientes de guardar.')
  }

  async function calculate() {
    if (!evaluationId || productRisk === null || !navigator.onLine) {
      await alerts.error(
        new Error('Conéctese y cargue una evaluación.'),
        'No es posible calcular ahora',
      )
      return
    }
    try {
      await ensureEvaluationSynced()
      const result = await calculateEvaluation(evaluationId, productRisk)
      setCalculation(result)
      const suggested = qualificationForPercentage(result.compliancePercentage)
      if (suggested) {
        setSupplement((current) => ({
          ...current,
          currentQualification: current.currentQualification ?? suggested,
        }))
      }
      setMessage('Todas las respuestas están en la base de datos y el riesgo fue calculado.')
      await alerts.success(
        'Evaluación calculada',
        'Las respuestas y evidencias están sincronizadas.',
      )
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'No fue posible calcular el riesgo.')
      await alerts.error(error, 'No se pudo calcular el riesgo')
    }
  }

  async function persistSupplement(showSuccess = true) {
    if (!evaluationId || !navigator.onLine)
      throw new Error('Se requiere conexión para guardar los datos complementarios.')
    setSavingSupplement(true)
    try {
      const saved = await saveEvaluationSupplement(evaluationId, supplement)
      setSupplement(saved)
      if (showSuccess) await alerts.success('Datos complementarios guardados')
      return saved
    } finally {
      setSavingSupplement(false)
    }
  }

  async function saveSupplement() {
    try {
      await persistSupplement()
      setMessage('Datos de control, medidas y recomendaciones guardados.')
    } catch (error) {
      await alerts.error(error, 'No se pudieron guardar los datos complementarios')
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
      text: 'Se comprobará que todas las respuestas estén persistidas y se guardarán los datos complementarios.',
      confirmText: 'Finalizar',
    })
    if (!confirmed) return
    try {
      await ensureEvaluationSynced()
      await persistSupplement(false)
      const result = await finalizeEvaluation(evaluationId, productRisk)
      setCalculation(result)
      setMessage('Evaluación finalizada y almacenada correctamente.')
      await alerts.success('Evaluación finalizada')
      onFinalized?.()
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'No fue posible finalizar la evaluación.')
      await alerts.error(error, 'No se pudo finalizar la evaluación')
    }
  }

  async function addEvidence(file: File | undefined, sourceItem: number) {
    if (!file || !evaluationId) return
    setEvidenceStatus((current) => ({ ...current, [sourceItem]: 'Preparando archivo…' }))
    try {
      const location = await getOptionalLocation()
      const key = await queueEvidence({
        evaluationId,
        sourceItem,
        mimeType: file.type,
        fileName: file.name,
        evidenceType: 'FOTOGRAFIA',
        file,
        ...location,
      })
      if (navigator.onLine) {
        setEvidenceStatus((current) => ({ ...current, [sourceItem]: 'Subiendo a Supabase…' }))
        await flushSyncQueue()
        const pending = await offlineDb.syncQueue.get(key)
        if (pending)
          throw new Error(pending.lastError ?? 'La evidencia todavía no pudo sincronizarse.')
        setEvidenceStatus((current) => ({ ...current, [sourceItem]: 'Guardada en Supabase' }))
        setMessage('Evidencia opcional guardada en Supabase.')
      } else {
        setEvidenceStatus((current) => ({ ...current, [sourceItem]: 'Pendiente sin conexión' }))
        setMessage('Evidencia protegida localmente; se subirá a Supabase al recuperar conexión.')
      }
    } catch (error) {
      setEvidenceStatus((current) => ({ ...current, [sourceItem]: 'Error al guardar' }))
      setMessage(error instanceof Error ? error.message : 'La evidencia no pudo guardarse.')
      await alerts.error(error, 'No se pudo guardar la evidencia')
    }
  }

  function selectChapter(index: number) {
    setActiveChapter(index)
    requestAnimationFrame(() => chapterTopRef.current?.scrollIntoView({ block: 'start' }))
  }

  return (
    <section aria-labelledby="dynamic-form-title">
      <div className="rounded-xl bg-white p-4 shadow-card">
        <div className="mb-4 rounded-xl bg-brand-50 p-4 text-sm leading-6 text-ink-body">
          <strong className="text-brand-900">
            {readOnly
              ? 'Consulte la evaluación por capítulos'
              : 'Complete la evaluación por capítulos'}
          </strong>
          <p className="mt-1">
            {readOnly
              ? 'Esta vista no intenta guardar respuestas, datos complementarios ni evidencias.'
              : 'Las respuestas se guardan en la base de datos. Las evidencias son opcionales y cada archivo puede pesar hasta 5 MB.'}
          </p>
        </div>
        {!selectedEvaluationId && (
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
            Ficha BPM
          </h1>
          <p className="mt-2 text-sm text-ink-muted" role="status">
            {message}
          </p>
        </div>
        <div className="rounded-xl bg-white px-4 py-3 text-sm shadow-card">
          <strong>{answered}</strong> de <strong>{questions}</strong> criterios respondidos
          <div
            className="mt-2 h-2 w-48 overflow-hidden rounded-full bg-slate-100"
            aria-hidden="true"
          >
            <div
              className="h-full rounded-full bg-brand-600"
              style={{ width: `${questions ? (answered / questions) * 100 : 0}%` }}
            />
          </div>
        </div>
      </div>

      {readOnly && items.length > 0 && (
        <div className="mt-5 rounded-xl border border-slate-300 bg-slate-100 p-4 text-sm text-slate-700">
          <strong>Ficha en modo de solo lectura:</strong> los campos agrisados no se pueden
          modificar en el estado actual.
        </div>
      )}

      {loadingForm && (
        <div className="mt-6 rounded-card bg-white p-5 shadow-card">
          <FormSkeleton />
        </div>
      )}
      {items.length > 0 && (
        <>
          {policy && <InspectionScope policy={policy} selectedItems={questions} />}
          <ControlDataSection
            supplement={supplement}
            onChange={setSupplement}
            readOnly={readOnly}
          />
          <ScoringCriteria />
          <InspectionCriteria />
          <div
            ref={chapterTopRef}
            className="mt-6 scroll-mt-24 rounded-card bg-white p-4 shadow-card"
          >
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <p className="text-xs font-bold tracking-[0.12em] text-brand-700 uppercase">
                  Navegación de la ficha
                </p>
                <h2 className="mt-1 text-lg font-extrabold">
                  Capítulo {activeChapter + 1} de {chapters.length}
                </h2>
              </div>
              <span className="text-brand-800 rounded-full bg-brand-50 px-3 py-1.5 text-xs font-bold">
                {currentChapter?.items.filter(
                  (item) =>
                    item.isEvaluable &&
                    requiredSources.has(item.sourceItem) &&
                    ratings[item.sourceItem] &&
                    (ratings[item.sourceItem] !== 'NO_CUMPLE' || criticalities[item.sourceItem]),
                ).length ?? 0}{' '}
                de{' '}
                {currentChapter?.items.filter(
                  (item) => item.isEvaluable && requiredSources.has(item.sourceItem),
                ).length ?? 0}
              </span>
            </div>
            <div
              className="mt-4 flex gap-2 overflow-x-auto pb-2"
              role="tablist"
              aria-label="Capítulos de la evaluación"
            >
              {chapters.map((chapter, index) => (
                <button
                  key={chapter.id}
                  type="button"
                  role="tab"
                  aria-selected={index === activeChapter}
                  onClick={() => selectChapter(index)}
                  className={`min-h-10 shrink-0 rounded-xl border px-3 text-left text-sm font-bold ${index === activeChapter ? 'border-brand-700 bg-brand-700 text-white' : 'border-slate-200 bg-white text-ink-body hover:bg-brand-50'}`}
                >
                  {chapter.code}
                </button>
              ))}
            </div>
          </div>
          <div className="mt-4 space-y-3" aria-busy={loadingForm}>
            {currentChapter?.items.map((item) => (
              <EvaluationItemCard
                key={item.id}
                item={item}
                rating={ratings[item.sourceItem]}
                criticality={criticalities[item.sourceItem]}
                observation={observations[item.sourceItem] ?? ''}
                comment={comments[item.sourceItem] ?? ''}
                answerStatus={answerStatus[item.sourceItem]}
                evidenceStatus={evidenceStatus[item.sourceItem]}
                readOnly={readOnly}
                headingDepth={
                  item.isEvaluable
                    ? 0
                    : item.level -
                      Math.min(
                        ...currentChapter.items
                          .filter((chapterItem) => !chapterItem.isEvaluable)
                          .map((chapterItem) => chapterItem.level),
                      )
                }
                onRating={(rating) => void selectRating(item, rating)}
                onCriticality={(criticality) => void selectCriticality(item, criticality)}
                onObservation={(value) =>
                  setObservations((current) => ({ ...current, [item.sourceItem]: value }))
                }
                onComment={(value) =>
                  setComments((current) => ({ ...current, [item.sourceItem]: value }))
                }
                onBlur={() => void saveAnswerDetails(item)}
                onEvidence={(file) => void addEvidence(file, item.sourceItem)}
              />
            ))}
          </div>
          <div className="mt-5 flex items-center justify-between gap-3 rounded-xl bg-white p-4 shadow-card">
            <button
              type="button"
              disabled={activeChapter === 0}
              onClick={() => selectChapter(activeChapter - 1)}
              className="rounded-xl border px-4 py-2.5 font-bold disabled:opacity-40"
            >
              ← Anterior
            </button>
            <span className="text-center text-sm text-ink-muted">{currentChapter?.title}</span>
            <button
              type="button"
              disabled={activeChapter >= chapters.length - 1}
              onClick={() => selectChapter(activeChapter + 1)}
              className="rounded-xl bg-brand-700 px-4 py-2.5 font-bold text-white disabled:opacity-40"
            >
              Siguiente →
            </button>
          </div>
          {formComplete ? (
            <FollowUpSection supplement={supplement} onChange={setSupplement} readOnly={readOnly} />
          ) : (
            <div className="mt-6 rounded-xl border border-dashed border-slate-300 bg-white p-5 text-sm text-ink-muted">
              {policy?.blockingReason ??
                (policy?.mode === 'DENUNCIA_DISCRECIONAL'
                  ? 'Responda los puntos relacionados con la denuncia. El inspector decide el alcance y debe incluir al menos uno.'
                  : 'Complete todos los criterios obligatorios de este flujo para habilitar las medidas correctivas y recomendaciones opcionales.')}
            </div>
          )}
          <div className="mt-6 rounded-xl bg-surface-inverse p-5 text-white shadow-card">
            <div className="flex flex-wrap items-end gap-3">
              <label className="text-sm font-bold">
                Riesgo del producto
                <select
                  disabled={readOnly}
                  value={productRisk ?? ''}
                  onChange={(event) =>
                    setProductRisk(event.target.value ? Number(event.target.value) : null)
                  }
                  className="mt-1.5 block min-h-11 rounded-lg bg-white px-3 text-ink-strong disabled:cursor-not-allowed disabled:bg-slate-300 disabled:text-slate-600"
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
              <button
                type="button"
                onClick={() => void saveSupplement()}
                disabled={readOnly || savingSupplement}
                className="min-h-11 rounded-xl border border-white/30 px-4 font-bold disabled:cursor-not-allowed disabled:border-slate-500 disabled:bg-slate-700 disabled:text-slate-400"
              >
                {savingSupplement ? 'Guardando…' : 'Guardar datos complementarios'}
              </button>
              <button
                type="button"
                onClick={() => void calculate()}
                disabled={readOnly || productRisk === null || !formComplete}
                className="min-h-11 rounded-xl bg-brand-500 px-4 font-bold disabled:cursor-not-allowed disabled:opacity-50"
              >
                Sincronizar y calcular
              </button>
              <button
                type="button"
                onClick={() => void finalize()}
                disabled={readOnly || productRisk === null || !formComplete}
                className="min-h-11 rounded-xl bg-white px-4 font-bold text-brand-900 disabled:cursor-not-allowed disabled:opacity-50"
              >
                Finalizar evaluación
              </button>
            </div>
            {!productRiskOptions.loading && productRiskOptions.options.length === 0 && (
              <p className="mt-3 text-xs text-amber-200">
                El catálogo NIVEL_RIESGO_ALIMENTO no tiene valores activos.
              </p>
            )}
            {calculation && (
              <output className="mt-4 block rounded-lg bg-white/10 px-4 py-3 text-sm">
                Cumplimiento {calculation.compliancePercentage?.toFixed(1) ?? 'N/D'}% · Riesgo{' '}
                {calculation.totalRisk?.toFixed(2) ?? 'N/D'} · {calculation.riskLevel}
                <span className="mt-2 block rounded-lg bg-emerald-950/40 p-3 text-base font-extrabold text-emerald-100">
                  Frecuencia de inspección: {formatStatusLabel(calculation.frequency)}
                  {calculation.nextInspectionDate
                    ? ` · Próxima inspección: ${new Intl.DateTimeFormat('es-DO', { dateStyle: 'long' }).format(new Date(`${calculation.nextInspectionDate}T12:00:00`))}`
                    : ''}
                </span>
                <strong className="mt-2 block text-emerald-100">
                  {calculation.decision.condition} · {calculation.decision.primaryRecommendation}
                </strong>
                <span className="mt-1 block text-xs text-emerald-100">
                  Alcance calculado: {calculation.decision.applicableItems} ítems · NC críticas:{' '}
                  {calculation.decision.criticalNonconformities} · mayores:{' '}
                  {calculation.decision.majorNonconformities} · menores:{' '}
                  {calculation.decision.minorNonconformities}
                </span>
                {calculation.decision.messages.length > 0 && (
                  <ul className="mt-2 list-disc space-y-1 pl-5 text-xs text-amber-100">
                    {calculation.decision.messages.map((guidance) => (
                      <li key={guidance}>{guidance}</li>
                    ))}
                  </ul>
                )}
              </output>
            )}
          </div>
        </>
      )}
    </section>
  )
}

function InspectionScope({
  policy,
  selectedItems,
}: {
  policy: EvaluationInspectionPolicy
  selectedItems: number
}) {
  const complaintPending = policy.mode === 'DENUNCIA_DISCRECIONAL' && selectedItems === 0
  const blocked = !policy.isReady && !complaintPending
  return (
    <section
      className={`mt-6 rounded-card border p-5 shadow-card ${
        blocked
          ? 'border-red-200 bg-red-50'
          : complaintPending
            ? 'border-amber-200 bg-amber-50'
            : 'border-brand-200 bg-brand-50'
      }`}
      aria-labelledby="inspection-scope-title"
    >
      <p className="text-xs font-extrabold tracking-[0.12em] text-brand-700 uppercase">
        Alcance aplicado automáticamente
      </p>
      <h2 id="inspection-scope-title" className="mt-1 text-lg font-extrabold text-ink-strong">
        {policy.title}
      </h2>
      <p className="mt-2 text-sm leading-6 text-ink-body">{policy.explanation}</p>
      {policy.previousInspectionDate && (
        <p className="mt-2 text-sm font-semibold text-ink-body">
          Inspección anterior: {policy.previousInspectionDate}
          {policy.previousCompliancePercentage !== null
            ? ` · ${policy.previousCompliancePercentage.toFixed(1)} %`
            : ''}
        </p>
      )}
      {(policy.blockingReason || complaintPending) && (
        <p className={`mt-3 text-sm font-bold ${blocked ? 'text-red-700' : 'text-amber-800'}`}>
          {policy.blockingReason}
        </p>
      )}
    </section>
  )
}

function EvaluationItemCard({
  item,
  rating,
  criticality,
  observation,
  comment,
  answerStatus,
  evidenceStatus,
  readOnly,
  headingDepth,
  onRating,
  onCriticality,
  onObservation,
  onComment,
  onBlur,
  onEvidence,
}: {
  item: EvaluationFormItem
  rating?: string
  criticality?: string
  observation: string
  comment: string
  answerStatus?: string
  evidenceStatus?: string
  readOnly: boolean
  headingDepth: number
  onRating: (rating: string) => void
  onCriticality: (criticality: string) => void
  onObservation: (value: string) => void
  onComment: (value: string) => void
  onBlur: () => void
  onEvidence: (file?: File) => void
}) {
  if (!item.isEvaluable) {
    const headingStyle =
      headingDepth <= 0
        ? 'border-brand-900 bg-brand-900 text-white shadow-card'
        : headingDepth === 1
          ? 'border-brand-600 bg-brand-600 text-white'
          : 'border-brand-200 bg-brand-100 text-brand-900'
    return (
      <div className={`rounded-xl border-l-[6px] p-4 ${headingStyle}`}>
        <p className="text-[0.65rem] font-extrabold tracking-[0.14em] uppercase opacity-80">
          {headingDepth <= 0 ? 'Capítulo' : headingDepth === 1 ? 'Sección' : 'Subsección'}
        </p>
        <h2 className="mt-1 font-extrabold">{item.title}</h2>
      </div>
    )
  }
  return (
    <article className={`rounded-xl p-4 shadow-card ${readOnly ? 'bg-slate-100' : 'bg-white'}`}>
      <fieldset disabled={readOnly} className={readOnly ? 'text-slate-500' : undefined}>
        <legend className="text-sm leading-6 font-semibold text-ink-body">{item.title}</legend>
        <div className="mt-3 flex flex-wrap gap-2">
          {ratingOptions.map((option) => (
            <label key={option.code} className={readOnly ? 'cursor-not-allowed' : 'cursor-pointer'}>
              <input
                className="peer sr-only"
                type="radio"
                name={`rating-${item.id}`}
                value={option.code}
                checked={rating === option.code}
                onChange={() => onRating(option.code)}
              />
              <span
                className={`inline-flex min-h-10 items-center rounded-lg border px-3 text-sm font-bold ${readOnly ? 'border-slate-300 bg-slate-200 text-slate-600 peer-checked:border-slate-500 peer-checked:bg-slate-300 peer-checked:text-slate-800' : 'border-slate-300 peer-checked:border-brand-700 peer-checked:bg-brand-100 peer-checked:text-brand-900'}`}
              >
                {option.short} · {option.label} · {option.score}
              </span>
            </label>
          ))}
        </div>
        {rating === 'NO_CUMPLE' && (
          <div className="mt-3 rounded-xl border border-amber-200 bg-amber-50 p-3">
            <p className="text-xs font-extrabold tracking-wide text-amber-900 uppercase">
              NC · Nivel de criticidad obligatorio
            </p>
            <div className="mt-2 flex flex-wrap gap-2">
              {criticalityOptions.map((option) => (
                <label
                  key={option.code}
                  className={readOnly ? 'cursor-not-allowed' : 'cursor-pointer'}
                >
                  <input
                    className="peer sr-only"
                    type="radio"
                    name={`criticality-${item.id}`}
                    value={option.code}
                    checked={criticality === option.code}
                    onChange={() => onCriticality(option.code)}
                  />
                  <span
                    className={`inline-flex min-h-10 items-center rounded-lg border px-3 text-sm font-bold ${readOnly ? 'border-slate-300 bg-slate-200 text-slate-600 peer-checked:border-slate-500 peer-checked:bg-slate-300 peer-checked:text-slate-800' : 'border-amber-300 bg-white peer-checked:border-red-700 peer-checked:bg-red-50 peer-checked:text-red-800'}`}
                  >
                    {option.short ?? option.code} · {option.label}
                  </span>
                </label>
              ))}
            </div>
          </div>
        )}
        <div className="mt-3 grid gap-3 md:grid-cols-2">
          <label className="text-xs font-bold text-ink-muted">
            Observación
            <textarea
              rows={2}
              value={observation}
              onChange={(event) => onObservation(event.target.value)}
              onBlur={onBlur}
              className="mt-1 w-full rounded-lg border border-slate-300 p-2 text-sm font-normal text-ink-body disabled:cursor-not-allowed disabled:bg-slate-200 disabled:text-slate-600"
              placeholder="Detalle verificable observado durante la inspección"
            />
          </label>
          <label className="text-xs font-bold text-ink-muted">
            Comentario técnico
            <textarea
              rows={2}
              value={comment}
              onChange={(event) => onComment(event.target.value)}
              onBlur={onBlur}
              className="mt-1 w-full rounded-lg border border-slate-300 p-2 text-sm font-normal text-ink-body disabled:cursor-not-allowed disabled:bg-slate-200 disabled:text-slate-600"
              placeholder="Comentario complementario o recomendación"
            />
          </label>
        </div>
        <div className="mt-3 flex flex-wrap items-center gap-3">
          <label
            className={`inline-flex min-h-10 items-center rounded-lg border px-3 text-sm font-bold ${readOnly ? 'cursor-not-allowed border-slate-300 bg-slate-200 text-slate-500' : 'text-brand-800 cursor-pointer border-brand-200 hover:bg-brand-50'}`}
          >
            Adjuntar evidencia <span className="ml-1 font-normal">(opcional, máx. 5 MB)</span>
            <input
              type="file"
              className="sr-only"
              accept="image/jpeg,image/png,application/pdf,video/mp4"
              onChange={(event) => onEvidence(event.target.files?.[0])}
            />
          </label>
          {answerStatus && (
            <span className="text-xs font-semibold text-ink-muted">Respuesta: {answerStatus}</span>
          )}
          {evidenceStatus && (
            <span className="text-xs font-semibold text-brand-700">
              Evidencia: {evidenceStatus}
            </span>
          )}
        </div>
      </fieldset>
    </article>
  )
}

function ControlDataSection({
  supplement,
  onChange,
  readOnly,
}: {
  supplement: EvaluationSupplement
  onChange: (value: EvaluationSupplement) => void
  readOnly: boolean
}) {
  const inputClass =
    'mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal disabled:cursor-not-allowed disabled:bg-slate-200 disabled:text-slate-600'
  const set = <K extends keyof EvaluationSupplement>(key: K, value: EvaluationSupplement[K]) =>
    onChange({ ...supplement, [key]: value })
  return (
    <section
      className={`mt-6 rounded-card p-5 shadow-card ${readOnly ? 'bg-slate-100' : 'bg-white'}`}
      aria-labelledby="control-data-title"
    >
      <p className="text-xs font-bold tracking-[0.12em] text-brand-700 uppercase">
        Encabezado de la inspección
      </p>
      <h2 id="control-data-title" className="mt-1 text-xl font-extrabold">
        Datos de control interno
      </h2>
      <div className="mt-5 grid gap-4 md:grid-cols-2">
        <label className="text-sm font-bold">
          Fecha de la última inspección{' '}
          <span className="font-normal text-ink-muted">(si aplica)</span>
          <input
            disabled={readOnly}
            type="date"
            max={supplement.currentInspectionDate ?? undefined}
            value={supplement.previousInspectionDate ?? ''}
            onChange={(event) => set('previousInspectionDate', event.target.value || null)}
            className={inputClass}
          />
        </label>
        <div className="text-sm font-bold">
          Calificación anterior <span className="font-normal text-ink-muted">(si aplica)</span>
          <QualificationSelect
            label="Calificación anterior"
            value={supplement.previousQualification}
            onChange={(value) => set('previousQualification', value)}
            className={inputClass}
            disabled={readOnly}
          />
        </div>
        <label className="text-sm font-bold">
          Fecha de la inspección actual
          <input
            disabled={readOnly}
            type="date"
            value={supplement.currentInspectionDate ?? ''}
            onChange={(event) => set('currentInspectionDate', event.target.value || null)}
            className={inputClass}
          />
        </label>
        <div className="text-sm font-bold">
          Calificación actual
          <QualificationSelect
            label="Calificación actual"
            value={supplement.currentQualification}
            onChange={(value) => set('currentQualification', value)}
            className={inputClass}
            disabled={readOnly}
          />
        </div>
        <label className="text-sm font-bold">
          Oficial de Salud DPS/DAS 1
          <input
            disabled={readOnly}
            maxLength={200}
            value={supplement.dpsDasOfficer1 ?? ''}
            onChange={(event) => set('dpsDasOfficer1', event.target.value || null)}
            className={inputClass}
          />
        </label>
        <label className="text-sm font-bold">
          Oficial de Salud DPS/DAS 2 <span className="font-normal text-ink-muted">(opcional)</span>
          <input
            disabled={readOnly}
            maxLength={200}
            value={supplement.dpsDasOfficer2 ?? ''}
            onChange={(event) => set('dpsDasOfficer2', event.target.value || null)}
            className={inputClass}
          />
        </label>
        <label className="text-sm font-bold">
          Técnico de DIGEMAPS 1
          <input
            disabled={readOnly}
            maxLength={200}
            value={supplement.digemapsTechnician1 ?? ''}
            onChange={(event) => set('digemapsTechnician1', event.target.value || null)}
            className={inputClass}
          />
        </label>
        <label className="text-sm font-bold">
          Técnico de DIGEMAPS 2 <span className="font-normal text-ink-muted">(opcional)</span>
          <input
            disabled={readOnly}
            maxLength={200}
            value={supplement.digemapsTechnician2 ?? ''}
            onChange={(event) => set('digemapsTechnician2', event.target.value || null)}
            className={inputClass}
          />
        </label>
      </div>
    </section>
  )
}

function QualificationSelect({
  label,
  value,
  onChange,
  className,
  disabled = false,
}: {
  label: string
  value: string | null
  onChange: (value: string | null) => void
  className: string
  disabled?: boolean
}) {
  return (
    <select
      disabled={disabled}
      aria-label={label}
      value={value ?? ''}
      onChange={(event) => onChange(event.target.value || null)}
      className={className}
    >
      <option value="">Seleccione</option>
      {qualificationOptions.map((option) => (
        <option key={option.value} value={option.value}>
          {option.label}
        </option>
      ))}
    </select>
  )
}

function ScoringCriteria() {
  return (
    <section
      className="mt-6 overflow-hidden rounded-card bg-white shadow-card"
      aria-labelledby="scoring-title"
    >
      <div className="bg-slate-700 px-5 py-3 text-white">
        <h2 id="scoring-title" className="text-lg font-extrabold">
          Criterios de calificación
        </h2>
      </div>
      <div className="grid divide-y divide-slate-200 md:grid-cols-[auto_1fr_1.3fr] md:divide-x md:divide-y-0">
        {scoringCriteria.map((criterion) => (
          <div key={criterion.range} className="contents">
            <strong className="text-brand-800 px-4 py-3">{criterion.range}</strong>
            <span className="px-4 py-3">{criterion.condition}</span>
            <span className="px-4 py-3 font-semibold">{criterion.action}</span>
          </div>
        ))}
      </div>
      <div className="border-t border-slate-200 bg-slate-50 p-4">
        <p className="text-sm font-extrabold text-ink-strong">Valores de las respuestas</p>
        <div className="mt-2 flex flex-wrap gap-2 text-xs font-semibold text-ink-body">
          <span className="rounded-full bg-white px-3 py-1.5">C · Cumple · 1 punto</span>
          <span className="rounded-full bg-white px-3 py-1.5">
            CP · Cumplimiento parcial · 0.5 puntos
          </span>
          <span className="rounded-full bg-white px-3 py-1.5">
            IT · Incumplimiento Total · 0 puntos
          </span>
          <span className="rounded-full bg-white px-3 py-1.5">
            N/A · No aplica · se excluye del cálculo
          </span>
        </div>
        <p className="mt-3 text-xs text-ink-muted">
          NC significa Nivel de Criticidad: C · No Conformidad Crítica; M · No Conformidad Mayor; Me
          · No Conformidad Menor.
        </p>
      </div>
    </section>
  )
}

function InspectionCriteria() {
  return (
    <details className="mt-6 rounded-card bg-white p-5 shadow-card">
      <summary className="cursor-pointer font-extrabold text-ink-strong">
        Criterios para las inspecciones y calificación
      </summary>
      <ol className="mt-4 grid gap-3 text-sm leading-6 text-ink-body">
        {inspectionCriteria.map((criterion, index) => (
          <li key={criterion} className="flex gap-3 rounded-xl bg-slate-50 p-3">
            <span className="grid h-7 w-7 shrink-0 place-items-center rounded-full bg-brand-700 text-xs font-bold text-white">
              {index + 1}
            </span>
            <span>{criterion}</span>
          </li>
        ))}
      </ol>
    </details>
  )
}

function FollowUpSection({
  supplement,
  onChange,
  readOnly,
}: {
  supplement: EvaluationSupplement
  onChange: (value: EvaluationSupplement) => void
  readOnly: boolean
}) {
  return (
    <section
      className={`mt-6 rounded-card p-5 shadow-card ${readOnly ? 'bg-slate-100' : 'bg-white'}`}
      aria-labelledby="follow-up-title"
    >
      <p className="text-xs font-bold tracking-[0.12em] text-brand-700 uppercase">Paso opcional</p>
      <h2 id="follow-up-title" className="mt-1 text-xl font-extrabold">
        Medidas correctivas y recomendaciones
      </h2>
      <p className="mt-2 text-sm text-ink-muted">
        Puede registrar hasta 10 elementos en cada sección. No son obligatorios para finalizar.
      </p>
      <div className="mt-5 grid gap-6 lg:grid-cols-2">
        <FollowUpEditor
          title="Medidas correctivas"
          values={supplement.correctiveMeasures}
          readOnly={readOnly}
          onChange={(correctiveMeasures) => onChange({ ...supplement, correctiveMeasures })}
        />
        <FollowUpEditor
          title="Recomendaciones"
          values={supplement.recommendations}
          readOnly={readOnly}
          onChange={(recommendations) => onChange({ ...supplement, recommendations })}
        />
      </div>
    </section>
  )
}

function FollowUpEditor({
  title,
  values,
  onChange,
  readOnly,
}: {
  title: string
  values: EvaluationFollowUpItem[]
  onChange: (values: EvaluationFollowUpItem[]) => void
  readOnly: boolean
}) {
  function add() {
    if (values.length < 10) onChange([...values, { detail: '', dueDate: null }])
  }
  function update(index: number, patch: Partial<EvaluationFollowUpItem>) {
    onChange(values.map((value, current) => (current === index ? { ...value, ...patch } : value)))
  }
  function remove(index: number) {
    onChange(values.filter((_, current) => current !== index))
  }
  return (
    <div>
      <div className="flex items-center justify-between gap-3">
        <h3 className="font-extrabold">{title}</h3>
        <button
          type="button"
          onClick={add}
          disabled={readOnly || values.length >= 10}
          className="text-brand-800 rounded-lg border border-brand-200 px-3 py-2 text-sm font-bold disabled:cursor-not-allowed disabled:border-slate-300 disabled:bg-slate-200 disabled:text-slate-500"
        >
          + Agregar
        </button>
      </div>
      <div className="mt-3 space-y-3">
        {values.length === 0 && (
          <p className="rounded-xl bg-slate-50 p-4 text-sm text-ink-muted">
            No se han agregado elementos.
          </p>
        )}
        {values.map((value, index) => (
          <div key={index} className="rounded-xl border border-slate-200 p-3">
            <div className="flex justify-between">
              <span className="text-xs font-bold text-brand-700">No. {index + 1}</span>
              <button
                type="button"
                disabled={readOnly}
                onClick={() => remove(index)}
                className="text-xs font-bold text-red-700"
              >
                Eliminar
              </button>
            </div>
            <textarea
              disabled={readOnly}
              maxLength={2000}
              rows={3}
              value={value.detail}
              onChange={(event) => update(index, { detail: event.target.value })}
              placeholder="Detalle"
              className="mt-2 w-full rounded-lg border border-slate-300 p-2 text-sm disabled:cursor-not-allowed disabled:bg-slate-200 disabled:text-slate-600"
            />
            <label className="mt-2 block text-xs font-bold text-ink-muted">
              Fecha de cumplimiento <span className="font-normal">(opcional)</span>
              <input
                disabled={readOnly}
                type="date"
                value={value.dueDate ?? ''}
                onChange={(event) => update(index, { dueDate: event.target.value || null })}
                className="mt-1 min-h-10 w-full rounded-lg border border-slate-300 px-2 font-normal disabled:cursor-not-allowed disabled:bg-slate-200 disabled:text-slate-600"
              />
            </label>
          </div>
        ))}
      </div>
    </div>
  )
}
