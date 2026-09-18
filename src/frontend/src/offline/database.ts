import Dexie, { type EntityTable } from 'dexie'

export type SyncStatus = 'pending' | 'processing' | 'failed'
export type SyncMutationKind =
  | 'answer'
  | 'evidence'
  | 'request'
  | 'case'
  | 'schedule'
  | 'correction'
  | 'supplement'
  | 'calculate'
  | 'finalize'

export interface CachedEvaluation {
  id: string
  ownerUserId: string
  establishmentId: string
  status: string
  riskLevel?: string
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
  criticalityCode?: string
  observation?: string
  comment?: string
}

export interface EvidencePayload {
  evaluationId: string
  sourceItem?: number
  mimeType: string
  fileName: string
  evidenceType: string
  file: Blob
  sha256Hash: string
  latitude?: number
  longitude?: number
  accuracyMeters?: number
}

export interface RequestPayload {
  companyId: string | null
  applicantUserId?: string | null
  establishmentId: string | null
  inspectionReasonId: string
  reasonDetail: string
  establishmentType: string
  observations: string
  idempotencyKey: string
  rowVersion: null
}

export interface CasePayload {
  origin: 'SOLICITUD_EMPRESA' | 'PROGRAMACION' | 'ALERTA_LAPCH' | 'DENUNCIA'
  sourceId: string
  priority: number
  responsibleId: string | null
  analysisDecision: string | null
  decisionReason: string
  idempotencyKey: string
  rowVersion: null
}

export interface SchedulePayload {
  caseId: string
  startsAt: string
  endsAt: string
  priority: number
  observations: string
  changeReason: string
  evaluatorIds: string[]
  idempotencyKey: string
  rowVersion: null
}

export interface CorrectionPayload {
  evaluationId: string
  responsibleType: 'TECNICO' | 'EMPRESA'
  assignedToId: string | null
  coordinatorObservation: string
  dueAt: string
  idempotencyKey: string
  fields: Array<{ sourceItem: number; reason: string }>
}

export interface EvaluationActionPayload {
  evaluationId: string
  supplement?: import('../lib/api').EvaluationSupplement
}

export interface SyncQueueItem {
  idempotencyKey: string
  ownerUserId: string
  kind: SyncMutationKind
  status: SyncStatus
  payload:
    | AnswerPayload
    | EvidencePayload
    | RequestPayload
    | CasePayload
    | SchedulePayload
    | CorrectionPayload
    | EvaluationActionPayload
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
    this.version(2).stores({
      evaluations: '&id, establishmentId, status, riskLevel, updatedAt',
      inspectionTemplates: '&key, id, code, version, status, updatedAt',
      syncQueue:
        '&idempotencyKey, ownerUserId, kind, status, createdAt, nextAttemptAt, [ownerUserId+status+nextAttemptAt]',
    })
    this.version(3).stores({
      evaluations: '&id, ownerUserId, establishmentId, status, riskLevel, updatedAt',
      inspectionTemplates: '&key, id, code, version, status, updatedAt',
      syncQueue:
        '&idempotencyKey, ownerUserId, kind, status, createdAt, nextAttemptAt, [ownerUserId+status+nextAttemptAt]',
    })
  }
}

export const offlineDb = new SigersaOfflineDatabase()
