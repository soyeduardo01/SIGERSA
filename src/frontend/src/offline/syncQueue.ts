import { getSupabaseClient } from '../lib/supabase'
import {
  ApiError,
  apiError,
  apiFetch,
  confirmEvidenceUpload,
  getSession,
  getSessionIdentity,
  requestEvidenceUploadAuthorization,
} from '../lib/api'
import {
  offlineDb,
  type AnswerPayload,
  type CasePayload,
  type CorrectionPayload,
  type EvidencePayload,
  type EvaluationActionPayload,
  type RequestPayload,
  type SchedulePayload,
  type SyncQueueItem,
} from './database'
import { validateAndHashEvidence } from './evidenceFile'

const queueChangedEvent = 'sigersa:sync-queue-changed'
const maxAttempts = 5
let activeFlush: Promise<void> | undefined

export function subscribeToSyncQueue(listener: () => void) {
  window.addEventListener(queueChangedEvent, listener)
  return () => window.removeEventListener(queueChangedEvent, listener)
}

export async function queueAnswer(payload: AnswerPayload, idempotencyKey = crypto.randomUUID()) {
  return enqueueMutation('answer', payload, idempotencyKey)
}

export async function queueEvaluationStart(payload: EvaluationActionPayload) {
  const key = await enqueueMutation('start', payload, `offline-start-${payload.evaluationId}`)
  const cached = await offlineDb.evaluations.get(payload.evaluationId)
  if (cached?.ownerUserId === currentUserId()) {
    await offlineDb.evaluations.update(payload.evaluationId, {
      status: 'EN_EJECUCION',
      snapshot: { ...asRecord(cached.snapshot), status: 'EN_EJECUCION', canEdit: true },
      updatedAt: new Date().toISOString(),
    })
  }
  return key
}

export async function queueEvidence(
  payload: Omit<EvidencePayload, 'sha256Hash'>,
  idempotencyKey = crypto.randomUUID(),
) {
  const sha256Hash = await validateAndHashEvidence(payload.file, payload.mimeType)
  return enqueueMutation('evidence', { ...payload, sha256Hash }, idempotencyKey)
}

export async function queueRequest(payload: RequestPayload) {
  return enqueueMutation('request', payload, payload.idempotencyKey)
}

export async function queueCase(payload: CasePayload) {
  return enqueueMutation('case', payload, payload.idempotencyKey)
}

export async function queueSchedule(payload: SchedulePayload) {
  return enqueueMutation('schedule', payload, payload.idempotencyKey)
}

export async function queueCorrection(payload: CorrectionPayload) {
  return enqueueMutation('correction', payload, payload.idempotencyKey)
}

export async function queueEvaluationSupplement(payload: EvaluationActionPayload) {
  return enqueueMutation('supplement', payload, `offline-supplement-${payload.evaluationId}`)
}

export async function queueEvaluationCalculation(evaluationId: string) {
  return enqueueMutation('calculate', { evaluationId }, `offline-calculate-${evaluationId}`)
}

export async function queueEvaluationFinalization(evaluationId: string) {
  const key = await enqueueMutation(
    'finalize',
    { evaluationId },
    `offline-finalize-${evaluationId}`,
  )
  const cached = await offlineDb.evaluations.get(evaluationId)
  if (cached?.ownerUserId === currentUserId()) {
    await offlineDb.evaluations.update(evaluationId, {
      status: 'FINALIZADA',
      snapshot: { ...asRecord(cached.snapshot), status: 'FINALIZADA' },
      updatedAt: new Date().toISOString(),
    })
  }
  return key
}

export async function getPendingMutationCount() {
  const ownerUserId = currentUserId()
  return offlineDb.syncQueue
    .where('status')
    .anyOf('pending', 'processing', 'failed')
    .filter((item) => item.ownerUserId === ownerUserId)
    .count()
}

export async function getPendingEvaluationEvidences(evaluationId: string) {
  const ownerUserId = currentUserId()
  return offlineDb.syncQueue
    .where('kind')
    .equals('evidence')
    .filter(
      (item) =>
        item.ownerUserId === ownerUserId &&
        'evaluationId' in item.payload &&
        item.payload.evaluationId === evaluationId,
    )
    .sortBy('createdAt')
    .then((items) => items.map((item) => item.payload as EvidencePayload))
}

export interface FlushSyncQueueOptions {
  retryFailed?: boolean
}

export function flushSyncQueue(options: FlushSyncQueueOptions = {}) {
  if (!navigator.onLine) return Promise.resolve()

  activeFlush ??= processSyncQueue(options).finally(() => {
    activeFlush = undefined
  })

  return activeFlush
}

export async function discardSyncMutation(idempotencyKey: string) {
  const item = await offlineDb.syncQueue.get(idempotencyKey)
  if (item?.ownerUserId === currentUserId()) await offlineDb.syncQueue.delete(idempotencyKey)
  notifyQueueChanged()
}

export async function retrySyncMutation(idempotencyKey: string) {
  const item = await offlineDb.syncQueue.get(idempotencyKey)
  if (!item || item.ownerUserId !== currentUserId()) return
  await offlineDb.syncQueue.update(idempotencyKey, {
    status: 'pending',
    attempts: 0,
    nextAttemptAt: new Date().toISOString(),
    lastError: undefined,
  })
  notifyQueueChanged()
  await flushSyncQueue()
}

export async function retryEvaluationMutations(evaluationId: string) {
  const now = new Date().toISOString()
  await offlineDb.syncQueue
    .where('status')
    .equals('failed')
    .filter(
      (item) =>
        (item.kind === 'answer' || item.kind === 'evidence') &&
        'evaluationId' in item.payload &&
        item.payload.evaluationId === evaluationId,
    )
    .modify({ status: 'pending', attempts: 0, nextAttemptAt: now, lastError: undefined })
  notifyQueueChanged()
}

async function processSyncQueue({ retryFailed = false }: FlushSyncQueueOptions) {
  const ownerUserId = currentUserId()
  const now = new Date().toISOString()
  if (retryFailed) {
    await offlineDb.syncQueue
      .where('status')
      .equals('failed')
      .filter((item) => item.ownerUserId === ownerUserId)
      .modify({ status: 'pending', attempts: 0, nextAttemptAt: now, lastError: undefined })
  }
  await offlineDb.syncQueue
    .where('status')
    .equals('processing')
    .filter((item) => item.ownerUserId === ownerUserId)
    .modify({ status: 'pending', nextAttemptAt: now })
  const queuedItems = await offlineDb.syncQueue
    .where('status')
    .anyOf('pending', 'failed')
    .filter(
      (item) =>
        item.ownerUserId === ownerUserId &&
        item.attempts < maxAttempts &&
        item.nextAttemptAt <= now,
    )
    .sortBy('createdAt')

  for (const item of queuedItems) {
    if (!(await processQueueItem(item))) break
  }
}

async function enqueueMutation(
  kind: SyncQueueItem['kind'],
  payload: SyncQueueItem['payload'],
  idempotencyKey: string,
) {
  const createdAt = new Date().toISOString()
  const item: SyncQueueItem = {
    idempotencyKey,
    ownerUserId: currentUserId(),
    kind,
    status: 'pending',
    payload,
    attempts: 0,
    createdAt,
    nextAttemptAt: createdAt,
  }

  await offlineDb.transaction('rw', offlineDb.syncQueue, async () => {
    const existing = await offlineDb.syncQueue.get(idempotencyKey)
    if (!existing) await offlineDb.syncQueue.add(item)
    else if (kind === 'supplement' || kind === 'calculate' || kind === 'finalize')
      await offlineDb.syncQueue.put(item)
  })
  notifyQueueChanged()

  if (navigator.onLine) {
    void flushSyncQueue()
  }

  return idempotencyKey
}

async function processQueueItem(item: SyncQueueItem) {
  await offlineDb.syncQueue.update(item.idempotencyKey, { status: 'processing' })
  notifyQueueChanged()

  try {
    if (item.kind === 'start') {
      const action = item.payload as EvaluationActionPayload
      await postJson(
        `/api/v1/evaluations/${action.evaluationId}/start`,
        {
          rowVersion: action.rowVersion,
          latitude: action.latitude ?? null,
          longitude: action.longitude ?? null,
          accuracyMeters: action.accuracyMeters ?? null,
        },
        item.idempotencyKey,
      )
    } else if (item.kind === 'answer') {
      const answer = item.payload as AnswerPayload
      await postJson(
        `/api/v1/evaluations/${answer.evaluationId}/answers`,
        answer,
        item.idempotencyKey,
      )
    } else if (item.kind === 'evidence') {
      const evidence = item.payload as EvidencePayload
      if (!item.storageUploaded) {
        const authorization = await requestEvidenceUploadAuthorization({
          evaluationId: evidence.evaluationId,
          idempotencyKey: item.idempotencyKey,
          originalName: evidence.fileName,
          mimeType: evidence.mimeType,
          fileSize: evidence.file.size,
        })
        const { error } = await getSupabaseClient()
          .storage.from(authorization.bucketName)
          .uploadToSignedUrl(authorization.supabasePath, authorization.token, evidence.file, {
            cacheControl: '3600',
            contentType: evidence.mimeType,
          })

        if (error && !isDuplicateObjectError(error)) throw error

        await offlineDb.syncQueue.update(item.idempotencyKey, {
          storageUploaded: true,
          authorizedBucketName: authorization.bucketName,
          authorizedSupabasePath: authorization.supabasePath,
        })
        item.authorizedBucketName = authorization.bucketName
        item.authorizedSupabasePath = authorization.supabasePath
      }

      if (!item.authorizedSupabasePath) {
        throw new Error('La autorización de almacenamiento no tiene una ruta válida.')
      }
      await confirmEvidenceUpload({
        evaluationId: evidence.evaluationId,
        idempotencyKey: item.idempotencyKey,
        supabasePath: item.authorizedSupabasePath,
        originalName: evidence.fileName,
        fileSize: evidence.file.size,
        mimeType: evidence.mimeType,
        sha256Hash: evidence.sha256Hash,
        evidenceType: evidence.evidenceType,
        sourceItem: evidence.sourceItem,
        latitude: evidence.latitude,
        longitude: evidence.longitude,
        accuracyMeters: evidence.accuracyMeters,
      })
    } else if (item.kind === 'request') {
      await postJson('/api/v1/requests', item.payload, item.idempotencyKey)
    } else if (item.kind === 'case') {
      await postJson('/api/v1/cases', item.payload, item.idempotencyKey)
    } else if (item.kind === 'schedule') {
      await postJson('/api/v1/schedules', item.payload, item.idempotencyKey)
    } else if (item.kind === 'correction') {
      await postJson('/api/v1/corrections', item.payload, item.idempotencyKey)
    } else {
      const action = item.payload as EvaluationActionPayload
      if (item.kind === 'supplement') {
        await putJson(`/api/v1/evaluations/${action.evaluationId}/supplement`, action.supplement)
      } else {
        await postJson(
          `/api/v1/evaluations/${action.evaluationId}/${item.kind}`,
          {},
          item.idempotencyKey,
        )
      }
    }

    await offlineDb.syncQueue.delete(item.idempotencyKey)
    return true
  } catch (error) {
    const permanent = error instanceof ApiError && [400, 403, 404, 409].includes(error.status ?? 0)
    const attempts = permanent ? maxAttempts : item.attempts + 1
    const retryDelaySeconds = Math.min(2 ** attempts * 15, 15 * 60)
    await offlineDb.syncQueue.update(item.idempotencyKey, {
      status: 'failed',
      attempts,
      nextAttemptAt: new Date(Date.now() + retryDelaySeconds * 1000).toISOString(),
      lastError: error instanceof Error ? error.message : 'Error de sincronización',
    })
    return false
  } finally {
    notifyQueueChanged()
  }
}

async function putJson(path: string, body: unknown) {
  const response = await apiFetch(path, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!response.ok) throw await apiError(response)
}

function isDuplicateObjectError(error: { message?: string; statusCode?: string | number }) {
  return (
    error.statusCode === 409 || error.statusCode === '409' || /duplicate/i.test(error.message ?? '')
  )
}

async function postJson(path: string, body: unknown, idempotencyKey: string) {
  const response = await apiFetch(path, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Idempotency-Key': idempotencyKey,
    },
    body: JSON.stringify({ ...asRecord(body), idempotency_key: idempotencyKey }),
  })

  if (!response.ok) {
    throw await apiError(response)
  }
}

function asRecord(value: unknown): Record<string, unknown> {
  return typeof value === 'object' && value !== null ? { ...value } : { value }
}

function notifyQueueChanged() {
  window.dispatchEvent(new Event(queueChangedEvent))
}

function currentUserId() {
  const session = getSession()
  if (!session) throw new Error('Se requiere una sesión activa para sincronizar cambios locales.')
  const userId = getSessionIdentity(session).id
  if (!userId) throw new Error('La sesión activa no contiene un identificador de usuario válido.')
  return userId
}
