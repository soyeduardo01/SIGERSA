import {
  getAllItems,
  getAllParameters,
  getCaseOptions,
  getCorrectionOptions,
  getEstablishmentOptions,
  getEvaluationOptions,
  getEvaluations,
  getFindingOptions,
  getInspectionRequestOptions,
  getScheduleOptions,
  getSurveillanceOptions,
  getUserManagementOptions,
  type EvaluationSummary,
} from '../lib/api'
import { prepareEvaluationOffline } from './evaluationCache'
import { replaceCachedEvaluations } from './evaluationListCache'
import { replaceCachedParameters } from './parameterCache'

let activeRefresh: Promise<void> | undefined

/**
 * Refreshes the complete local evaluation index and the workspaces that an
 * evaluator can edit. It deliberately runs in the background: IndexedDB stays
 * the startup source and a slow/unavailable network never blocks the UI.
 */
export function refreshOfflineEvaluations() {
  if (!navigator.onLine) return Promise.resolve()
  activeRefresh ??= refresh().finally(() => {
    activeRefresh = undefined
  })
  return activeRefresh
}

export async function refreshOfflineMirror(includeEvaluations = true) {
  if (!navigator.onLine) return
  await Promise.allSettled([
    includeEvaluations ? refreshOfflineEvaluations() : Promise.resolve(),
    refreshOfflineCatalogs(),
  ])
}

async function refreshOfflineCatalogs() {
  await Promise.allSettled([
    getAllParameters('', true).then(replaceCachedParameters),
    getAllItems(),
    getEstablishmentOptions(),
    getEvaluationOptions(),
    getUserManagementOptions(),
    getInspectionRequestOptions(),
    getCaseOptions(),
    getScheduleOptions(),
    getCorrectionOptions(),
    getSurveillanceOptions(),
    getFindingOptions(),
  ])
}

async function refresh() {
  const pageSize = 100
  const evaluations: EvaluationSummary[] = []
  let pageNumber = 1
  let total = 0

  do {
    const page = await getEvaluations({ page: pageNumber, pageSize })
    if (page.items.length === 0) break
    evaluations.push(...page.items)
    total = page.total
    pageNumber += 1
  } while (evaluations.length < total)

  await replaceCachedEvaluations(evaluations)

  const editable = evaluations.filter(
    (evaluation) =>
      evaluation.status === 'ASIGNADA' ||
      (evaluation.status === 'EN_EJECUCION' && evaluation.canEdit),
  )
  await Promise.allSettled(editable.map((evaluation) => prepareEvaluationOffline(evaluation.id)))
}
