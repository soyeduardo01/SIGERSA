import { getSupabaseClient } from '../lib/supabase'
import { apiFetch, confirmEvidenceUpload, requestEvidenceUploadAuthorization } from '../lib/api'
import {
  offlineDb,
  type AnswerPayload,
  type CasePayload,
  type CorrectionPayload,
  type EvidencePayload,
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

export async function getPendingMutationCount() {
  return offlineDb.syncQueue.where('status').anyOf('pending', 'processing', 'failed').count()
}

export function flushSyncQueue() {
  if (!navigator.onLine) return Promise.resolve()

  activeFlush ??= processSyncQueue().finally(() => {
    activeFlush = undefined
  })

  return activeFlush
}

async function processSyncQueue() {
  const now = new Date().toISOString()
  await offlineDb.syncQueue.where('status').equals('processing').modify({
    status: 'pending',
    nextAttemptAt: now,
  })
  const queuedItems = await offlineDb.syncQueue
    .where('status')
    .anyOf('pending', 'failed')
    .filter((item) => item.attempts < maxAttempts && item.nextAttemptAt <= now)
    .sortBy('createdAt')

  for (const item of queuedItems) {
    await processQueueItem(item)
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
    if (item.kind === 'answer') {
      await postJson('/api/v1/respuestas', item.payload, item.idempotencyKey)
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
    } else {
      await postJson('/api/v1/corrections', item.payload, item.idempotencyKey)
    }

    await offlineDb.syncQueue.delete(item.idempotencyKey)
  } catch (error) {
    const attempts = item.attempts + 1
    const retryDelaySeconds = Math.min(2 ** attempts * 15, 15 * 60)
    await offlineDb.syncQueue.update(item.idempotencyKey, {
      status: 'failed',
      attempts,
      nextAttemptAt: new Date(Date.now() + retryDelaySeconds * 1000).toISOString(),
      lastError: error instanceof Error ? error.message : 'Error de sincronización',
    })
  } finally {
    notifyQueueChanged()
  }
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
    throw new Error(`La API respondió con estado ${response.status}.`)
  }
}

function asRecord(value: unknown): Record<string, unknown> {
  return typeof value === 'object' && value !== null ? { ...value } : { value }
}

function notifyQueueChanged() {
  window.dispatchEvent(new Event(queueChangedEvent))
}
