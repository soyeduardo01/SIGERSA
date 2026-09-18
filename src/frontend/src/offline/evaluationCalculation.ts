import type {
  EvaluationCalculation,
  EvaluationCalculationContext,
  EvaluationInspectionPolicy,
  EvaluationSavedAnswer,
} from '../lib/api'

const scoreForRating: Record<string, number | null> = {
  C: 1,
  CUMPLE: 1,
  CP: 0.5,
  CUMPLE_PARCIAL: 0.5,
  IT: 0,
  NO_CUMPLE: 0,
  INCUMPLIMIENTO: 0,
  NA: null,
  'N/A': null,
  NO_APLICA: null,
}

export function calculateEvaluationOffline(
  evaluationId: string,
  answers: EvaluationSavedAnswer[],
  policy: EvaluationInspectionPolicy,
  context: EvaluationCalculationContext,
): EvaluationCalculation {
  const required = new Set(
    policy.mode === 'DENUNCIA_DISCRECIONAL'
      ? answers.map((answer) => answer.sourceItem)
      : policy.requiredSourceItems,
  )
  const applicable = answers.filter((answer) => required.has(answer.sourceItem))
  if (!policy.isReady && policy.mode !== 'DENUNCIA_DISCRECIONAL')
    throw new Error(policy.blockingReason ?? 'El alcance de la inspección no está listo.')
  if (applicable.length < required.size)
    throw new Error(`Faltan ${required.size - applicable.length} ítems obligatorios para calcular.`)

  const scores = applicable.map((answer) => scoreForRating[answer.rating.toUpperCase()])
  if (scores.some((score) => score === undefined))
    throw new Error('Existe una respuesta no válida.')
  const numeric = scores.filter((score): score is number => score !== null)
  if (numeric.length === 0) throw new Error('Todos los ítems aplicables están marcados como N/A.')
  const compliancePercentage =
    (numeric.reduce((sum, score) => sum + score, 0) / numeric.length) * 100

  const factorScores = {
    production: scoreProduction(context.monthlyProduction),
    haccp: scoreHaccp(context.haccpImplemented, context.haccpPercentage),
    bpm:
      compliancePercentage <= 81
        ? 3
        : compliancePercentage <= 89
          ? 2.33
          : compliancePercentage <= 95
            ? 1.67
            : 1,
    inabie: scoreInabie(context.isInabieSupplier, context.inabieDistributionCode),
    rejections:
      context.microbiologicalRejectionsLastFiveYears > 2
        ? 3
        : context.microbiologicalRejectionsLastFiveYears === 2
          ? 2.33
          : context.microbiologicalRejectionsLastFiveYears === 1
            ? 1.67
            : 1,
    sampling: scoreSampling(context.microbiologicalSamplingPlan, context.samplingApplicationCode),
  }
  const establishmentRisk = Object.values(factorScores).some((score) => score === null)
    ? null
    : factorScores.production! * 0.16 +
      factorScores.haccp! * 0.09 +
      factorScores.bpm * 0.56 +
      factorScores.inabie! * 0.05 +
      factorScores.rejections * 0.06 +
      factorScores.sampling! * 0.08
  const totalRisk =
    context.productRisk === null || establishmentRisk === null
      ? null
      : context.productRisk * establishmentRisk
  const riskLevel =
    totalRisk === null
      ? 'NO_CALCULABLE'
      : totalRisk <= 3.6
        ? 'BAJO'
        : totalRisk <= 6.3
          ? 'MEDIO'
          : 'ALTO'
  const frequency =
    riskLevel === 'BAJO'
      ? 'ANUAL'
      : riskLevel === 'MEDIO'
        ? 'SEMESTRAL'
        : riskLevel === 'ALTO'
          ? 'TRIMESTRAL'
          : 'NO_APLICA'
  const critical = applicable.filter((answer) => answer.criticalityCode === 'C').length
  const major = applicable.filter((answer) => answer.criticalityCode === 'M').length
  const minor = applicable.filter((answer) => answer.criticalityCode === 'ME').length
  const approvalEligible = policy.approvalPurpose
    ? compliancePercentage >= 81 && critical === 0 && major < 3
    : null
  const messages = [] as string[]
  if (compliancePercentage < 81)
    messages.push('Notificar las no conformidades detectadas y establecer fechas para corregirlas.')
  if (compliancePercentage <= 60)
    messages.push('Considerar la posibilidad de cerrar el establecimiento.')
  if (critical > 0)
    messages.push(
      'Se detectó una no conformidad crítica: recomendar detener la producción hasta corregirla.',
    )
  if (approvalEligible !== null)
    messages.push(
      approvalEligible
        ? 'Cumple las condiciones de aprobación.'
        : 'No cumple las condiciones para aprobación.',
    )
  const nextInspectionDate =
    frequency === 'NO_APLICA'
      ? null
      : addMonths(frequency === 'ANUAL' ? 12 : frequency === 'SEMESTRAL' ? 6 : 3)

  return {
    evaluationId,
    compliancePercentage,
    productRisk: context.productRisk,
    establishmentRisk,
    totalRisk,
    riskLevel,
    frequency,
    nextInspectionDate,
    decision: {
      band:
        compliancePercentage <= 60
          ? 'HASTA_60'
          : compliancePercentage <= 70
            ? 'MAS_60_HASTA_70'
            : compliancePercentage <= 80
              ? 'MAS_70_HASTA_80'
              : 'MAS_80',
      condition:
        compliancePercentage <= 60
          ? 'Condiciones inaceptables'
          : compliancePercentage <= 70
            ? 'Condiciones deficientes'
            : compliancePercentage <= 80
              ? 'Condiciones regulares'
              : 'Buenas condiciones',
      primaryRecommendation:
        compliancePercentage <= 60
          ? 'Considerar el cierre del establecimiento.'
          : compliancePercentage <= 70
            ? 'Urge corregir las no conformidades.'
            : compliancePercentage <= 80
              ? 'Es necesario realizar correcciones.'
              : 'Realizar las correcciones que correspondan.',
      approvalEligible,
      applicableItems: applicable.length,
      criticalNonconformities: critical,
      majorNonconformities: major,
      minorNonconformities: minor,
      messages,
    },
    breakdown: {
      formula: 'Riesgo total = riesgo microbiológico del producto × riesgo del establecimiento',
      baseTotalRisk: totalRisk,
      effectiveTotalRisk: totalRisk,
      adjustment: null,
      factors: [],
    },
    rowVersion: 0,
  }
}

function scoreProduction(value: number | null) {
  return value === null
    ? null
    : value > 2_000_000
      ? 3
      : value >= 800_000
        ? 2.33
        : value >= 200_000
          ? 1.67
          : 1
}
function scoreHaccp(value: boolean | null, percentage: number | null) {
  return value === null
    ? null
    : !value
      ? 3
      : (percentage ?? 0) <= 25
        ? 2.33
        : (percentage ?? 0) <= 75
          ? 1.67
          : 1
}
function scoreInabie(value: boolean | null, code: string | null) {
  return value === null
    ? null
    : !value
      ? 1
      : code === 'NACIONAL'
        ? 3
        : code === 'REGIONAL'
          ? 2.33
          : code === 'LOCAL'
            ? 1.67
            : null
}
function scoreSampling(value: boolean | null, code: string | null) {
  return value === null
    ? null
    : !value
      ? 3
      : ['MP', 'MATERIAS_PRIMAS'].includes(code ?? '')
        ? 2.33
        : ['AP_PT', 'AREAS_PROCESO_PRODUCTOS_TERMINADOS'].includes(code ?? '')
          ? 1.67
          : ['MP_AP_PT', 'MATERIAS_PRIMAS_AREAS_PROCESO_PRODUCTOS_TERMINADOS'].includes(code ?? '')
            ? 1
            : null
}
function addMonths(months: number) {
  const date = new Date()
  date.setMonth(date.getMonth() + months)
  return date.toISOString().slice(0, 10)
}
