import {
  getSession,
  getSessionIdentity,
  type EvaluationSummary,
  type EvaluationsPage,
} from '../lib/api'
import { offlineDb } from './database'

function ownerUserId() {
  const session = getSession()
  return session ? (getSessionIdentity(session).id ?? '') : ''
}

export async function cacheEvaluationPage(page: EvaluationsPage) {
  const owner = ownerUserId()
  if (!owner) return
  await offlineDb.evaluations.bulkPut(
    page.items.map((item) => ({
      id: item.id,
      ownerUserId: owner,
      establishmentId: item.establishmentId,
      status: item.status,
      riskLevel: item.riskLevel ?? undefined,
      snapshot: item,
      updatedAt: new Date().toISOString(),
    })),
  )
}

export async function readCachedEvaluationPage(
  search: string,
  status: string,
  page: number,
  pageSize: number,
) {
  const owner = ownerUserId()
  const normalizedSearch = search.trim().toLocaleLowerCase('es')
  const all = (await offlineDb.evaluations.where('ownerUserId').equals(owner).toArray())
    .map((item) => item.snapshot as EvaluationSummary)
    .filter((item) => !status || item.status === status)
    .filter(
      (item) =>
        !normalizedSearch ||
        [item.number, item.caseNumber, item.establishmentName, item.evaluatorName].some((value) =>
          value.toLocaleLowerCase('es').includes(normalizedSearch),
        ),
    )
  const start = (page - 1) * pageSize
  return { items: all.slice(start, start + pageSize), page, pageSize, total: all.length }
}
