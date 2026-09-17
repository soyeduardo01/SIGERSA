import type {
  EvaluationFormItem,
  EvaluationInspectionPolicy,
  EvaluationSavedEvidence,
} from '../../lib/api'

export interface EvaluationChapter {
  id: string
  code: string
  title: string
  items: EvaluationFormItem[]
}

const evaluationIdPattern =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i

export function isValidEvaluationId(value: string) {
  return evaluationIdPattern.test(value)
}

export function evidenceStatusBySlot(
  evidences: readonly EvaluationSavedEvidence[] | null | undefined,
) {
  const status: Record<string, string> = {}
  const slotBySource = new Map<number, number>()
  for (const evidence of evidences ?? []) {
    const slot = slotBySource.get(evidence.sourceItem) ?? 0
    if (slot >= 3) continue
    status[`${evidence.sourceItem}-${slot}`] = 'Archivo guardado'
    slotBySource.set(evidence.sourceItem, slot + 1)
  }
  return status
}

export const qualificationOptions = [
  { value: 'CONSIDERAR_CIERRE', label: '≤ 60% · Considerar cierre' },
  { value: 'URGE_CORREGIR', label: '> 60% – 70% · Urge corregir' },
  { value: 'NECESARIO_CORREGIR', label: '> 70% – 80% · Necesario hacer correcciones' },
  { value: 'ALGUNAS_CORRECCIONES', label: '> 80% · Hacer algunas correcciones' },
  { value: 'DETENER_PRODUCCION', label: 'Detener producción y corregir no conformidades' },
  { value: 'CORREGIR_NO_CONFORMIDADES', label: 'Corregir no conformidades' },
  { value: 'OTORGAR_CERTIFICACION_BPM', label: '> 81% · Otorgar certificación BPM' },
  { value: 'RENOVAR_PERMISO_SANITARIO', label: '> 81% · Renovar permiso sanitario' },
  { value: 'OTORGAR_PERMISO_SANITARIO', label: '> 81% · Otorgar permiso sanitario' },
] as const

export const scoringCriteria = [
  { range: '≤ 60%', condition: 'Condiciones inaceptables', action: 'Considerar cierre' },
  { range: '> 60% – 70%', condition: 'Condiciones deficientes', action: 'Urge corregir' },
  {
    range: '> 70% – 80%',
    condition: 'Condiciones regulares',
    action: 'Necesario hacer correcciones',
  },
  { range: '> 80%', condition: 'Buenas condiciones', action: 'Hacer algunas correcciones' },
] as const

export const inspectionCriteria = [
  'Cuando la inspección corresponda a una solicitud, renovación del Permiso Sanitario o certificación BPM, se completa la ficha y se aprueba con una puntuación final igual o superior a 81 %, siempre que no exista una no conformidad crítica ni tres no conformidades mayores.',
  'Si la inspección es programada y el establecimiento tiene Permiso Sanitario, la ficha puede iniciarse desde el punto 1.1.3, prestando especial atención a los aspectos críticos. La calificación se determina con los resultados obtenidos.',
  'Si la inspección es de seguimiento o control, se toman en cuenta las no conformidades detectadas en la inspección anterior y se califica según los resultados obtenidos.',
  'Si la inspección se origina por una denuncia, el inspector puede completar la ficha a su discreción, prestando especial atención a los aspectos denunciados.',
  'Si la puntuación final es inferior a 81 %, se notifican las no conformidades detectadas y se establecen fechas para corregirlas.',
  'Si la puntuación final es igual o inferior a 60 %, se considera la posibilidad de cerrar el establecimiento.',
  'Si se detecta una no conformidad crítica, se recomienda detener la producción hasta corregirla.',
] as const

export function groupItemsByChapter(items: EvaluationFormItem[]): EvaluationChapter[] {
  if (items.length === 0) return []
  const headings = items.filter((item) => !item.isEvaluable)
  const headingLevels = [...new Set(headings.map((item) => item.level))].sort((a, b) => a - b)
  const chapterLevel =
    headingLevels.find((level) => headings.filter((item) => item.level === level).length > 1) ??
    headingLevels[0]
  const chapterHeadings = headings.filter((item) => item.level === chapterLevel)

  if (chapterHeadings.length === 0) {
    return [{ id: 'general', code: 'Ficha', title: 'Ficha de evaluación', items }]
  }

  return chapterHeadings.map((heading, index) => {
    const nextOrder = chapterHeadings[index + 1]?.order ?? Number.POSITIVE_INFINITY
    return {
      id: heading.id,
      code: heading.code || `Capítulo ${index + 1}`,
      title: heading.title,
      items: items.filter(
        (item) => (index === 0 || item.order >= heading.order) && item.order < nextOrder,
      ),
    }
  })
}

export function itemsForInspectionPolicy(
  items: EvaluationFormItem[],
  policy: EvaluationInspectionPolicy | null,
) {
  if (!policy || policy.mode === 'FICHA_COMPLETA' || policy.mode === 'DENUNCIA_DISCRECIONAL')
    return items

  const requiredSources = new Set(policy.requiredSourceItems)
  const includedIds = new Set(
    items
      .filter((item) => item.isEvaluable && requiredSources.has(item.sourceItem))
      .map((item) => item.id),
  )
  const byId = new Map(items.map((item) => [item.id, item]))
  for (const itemId of [...includedIds]) {
    let parentId = byId.get(itemId)?.parentId ?? null
    while (parentId) {
      includedIds.add(parentId)
      parentId = byId.get(parentId)?.parentId ?? null
    }
  }
  return items.filter((item) => includedIds.has(item.id))
}

export function qualificationForPercentage(percentage: number | null) {
  if (percentage === null) return null
  if (percentage <= 60) return 'CONSIDERAR_CIERRE'
  if (percentage <= 70) return 'URGE_CORREGIR'
  if (percentage <= 80) return 'NECESARIO_CORREGIR'
  return 'ALGUNAS_CORRECCIONES'
}
