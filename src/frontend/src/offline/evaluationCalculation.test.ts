import { describe, expect, it } from 'vitest'
import { calculateEvaluationOffline } from './evaluationCalculation'
import type { EvaluationCalculationContext, EvaluationInspectionPolicy } from '../lib/api'

const context: EvaluationCalculationContext = {
  productRisk: 3,
  monthlyProduction: 2_100_000,
  haccpImplemented: false,
  haccpPercentage: null,
  isInabieSupplier: true,
  inabieDistributionCode: 'NACIONAL',
  microbiologicalRejectionsLastFiveYears: 3,
  microbiologicalSamplingPlan: false,
  samplingApplicationCode: null,
}

const policy: EvaluationInspectionPolicy = {
  mode: 'FICHA_COMPLETA',
  title: 'Ficha completa',
  explanation: '',
  isReady: true,
  blockingReason: null,
  requiredSourceItems: [1, 2],
  excludedSourceItems: [],
  approvalPurpose: 'OTORGAR_PERMISO_SANITARIO',
  previousEvaluationId: null,
  previousInspectionDate: null,
  previousCompliancePercentage: null,
}

describe('calculateEvaluationOffline', () => {
  it('calcula cumplimiento, riesgo alto y frecuencia trimestral sin red', () => {
    const result = calculateEvaluationOffline(
      'evaluation-1',
      [
        {
          sourceItem: 1,
          rating: 'CUMPLE',
          criticalityCode: null,
          observation: null,
          comment: null,
        },
        {
          sourceItem: 2,
          rating: 'NO_CUMPLE',
          criticalityCode: 'M',
          observation: null,
          comment: null,
        },
      ],
      policy,
      context,
    )

    expect(result.compliancePercentage).toBe(50)
    expect(result.totalRisk).toBeGreaterThan(6.3)
    expect(result.riskLevel).toBe('ALTO')
    expect(result.frequency).toBe('TRIMESTRAL')
    expect(result.decision.approvalEligible).toBe(false)
    expect(result.decision.messages.join(' ')).toContain('cerrar')
  })
})
