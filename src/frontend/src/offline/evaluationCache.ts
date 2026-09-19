import {
  getEvaluationWorkspace,
  type EvaluationFormItem,
  type EvaluationInspectionPolicy,
  type EvaluationCalculationContext,
  type EvaluationSavedAnswer,
  type EvaluationSavedEvidence,
  type EvaluationSupplement,
  type EvaluationWorkspace,
} from '../lib/api'
import { offlineDb } from './database'

export interface CachedEvaluationWorkspace {
  items: EvaluationFormItem[]
  policy: EvaluationInspectionPolicy
  answers: EvaluationSavedAnswer[]
  evidences: EvaluationSavedEvidence[]
  supplement: EvaluationSupplement
  calculationContext: EvaluationCalculationContext
}

export async function readCachedEvaluationWorkspace(evaluationId: string) {
  const cached = await offlineDb.inspectionTemplates.get(`evaluation-${evaluationId}`)
  if (!cached || Array.isArray(cached.definition)) return null
  return cached.definition as CachedEvaluationWorkspace
}

export async function cacheEvaluationWorkspace(
  evaluationId: string,
  workspace: EvaluationWorkspace,
) {
  const definition: CachedEvaluationWorkspace = {
    items: workspace.items,
    policy: workspace.policy,
    answers: workspace.answers,
    evidences: workspace.evidences ?? [],
    supplement: workspace.supplement,
    calculationContext: workspace.calculationContext,
  }
  await offlineDb.inspectionTemplates.put({
    key: `evaluation-${evaluationId}`,
    id: evaluationId,
    code: 'EVALUATION_SNAPSHOT',
    version: 1,
    status: 'PUBLICADA',
    definition,
    updatedAt: new Date().toISOString(),
  })
  return definition
}

export async function prepareEvaluationOffline(evaluationId: string) {
  if (await hasPendingEvaluationChanges(evaluationId)) {
    const cached = await readCachedEvaluationWorkspace(evaluationId)
    if (cached) return cached
    throw new Error('La evaluación tiene cambios locales pendientes de sincronizar.')
  }
  const workspace = await getEvaluationWorkspace(evaluationId)
  await cacheEvaluationWorkspace(evaluationId, workspace)
  return workspace
}

export async function hasPendingEvaluationChanges(evaluationId: string) {
  return (
    (await offlineDb.syncQueue
      .filter(
        (item) => 'evaluationId' in item.payload && item.payload.evaluationId === evaluationId,
      )
      .count()) > 0
  )
}
