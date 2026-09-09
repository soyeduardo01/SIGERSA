import Dexie, { type EntityTable } from 'dexie'

export type SyncStatus = 'pending' | 'processing' | 'failed'
export type SyncMutationKind = 'answer' | 'evidence'

export interface CachedEvaluation {
  id: string
  establishmentId: string
  status: string
  riskLevel?: 'BAJO' | 'MEDIO' | 'ALTO'
  snapshot: unknown
  updatedAt: string
}

export interface CachedInspectionTemplate {
  key: string
  id: string
  code: string
  version: number
  status: 'PUBLICADA' | 'RETIRADA' | 'ARCHIVADA'
  definition: unknown
  updatedAt: string
}

export interface AnswerPayload {
  evaluationId: string
  itemId: string
  value: string | number | boolean | Record<string, unknown> | null
}

export interface EvidencePayload {
  evaluationId: string
  mimeType: string
  fileName: string
  evidenceType: string
  file: Blob
  sha256Hash: string
}

export interface SyncQueueItem {
  idempotencyKey: string
  kind: SyncMutationKind
  status: SyncStatus
  payload: AnswerPayload | EvidencePayload
  attempts: number
  createdAt: string
  nextAttemptAt: string
  lastError?: string
  storageUploaded?: boolean
  authorizedBucketName?: string
  authorizedSupabasePath?: string
}

class SigersaOfflineDatabase extends Dexie {
  evaluations!: EntityTable<CachedEvaluation, 'id'>
  inspectionTemplates!: EntityTable<CachedInspectionTemplate, 'key'>
  syncQueue!: EntityTable<SyncQueueItem, 'idempotencyKey'>

  constructor() {
    super('SIGERSA_OFFLINE')

    this.version(1).stores({
      evaluations: '&id, establishmentId, status, riskLevel, updatedAt',
      inspectionTemplates: '&key, id, code, version, status, updatedAt',
      syncQueue: '&idempotencyKey, kind, status, createdAt, nextAttemptAt, [status+nextAttemptAt]',
    })
  }
}

export const offlineDb = new SigersaOfflineDatabase()
