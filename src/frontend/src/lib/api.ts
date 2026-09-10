const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')
const sessionKey = 'sigersa.auth.session'
const sessionChangedEvent = 'sigersa.auth.session.changed'
let refreshInFlight: Promise<AuthSession | null> | null = null

export interface AuthSession {
  accessToken: string
  accessTokenExpiresAt: string
  refreshToken: string
  refreshTokenExpiresAt: string
  roles: string[]
  userName?: string
  email?: string
}

export interface SessionIdentity {
  name: string
  email: string
  initials: string
}

export class ApiError extends Error {
  readonly status?: number

  constructor(message: string, status?: number) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

export interface AllItem {
  items: number
  itemsId: string
  description: string
  sectionType: string
  parents: string | null
}

export interface AllItemDraft {
  itemsId: string
  description: string
  sectionType: string
  parents: string | null
}

export interface AllItemScore {
  item: number
  itemsId: string
  score: number | null
  isCalculable: boolean
}

export interface EvaluationFormItem {
  id: string
  parentId: string | null
  sourceItem: number
  code: string
  title: string
  isEvaluable: boolean
  level: number
  order: number
}

export interface EvaluationCalculation {
  evaluationId: string
  compliancePercentage: number | null
  productRisk: number | null
  establishmentRisk: number | null
  totalRisk: number | null
  riskLevel: 'BAJO' | 'MEDIO' | 'ALTO' | 'NO_CALCULABLE'
  frequency: 'ANUAL' | 'SEMESTRAL' | 'TRIMESTRAL' | 'NO_APLICA'
  rowVersion: number
}

export interface EvaluationSummary {
  id: string
  number: string
  caseId: string
  caseNumber: string
  establishmentId: string
  establishmentName: string
  evaluatorId: string
  evaluatorName: string
  status: string
  scheduledStart: string | null
  scheduledEnd: string | null
  compliancePercentage: number | null
  totalRisk: number | null
  riskLevel: string | null
  answeredItems: number
  rowVersion: number
}

export interface EvaluationsPage {
  items: EvaluationSummary[]
  page: number
  pageSize: number
  total: number
}

export interface EvaluationCreateOptions {
  cases: Array<{ id: string; name: string; companyId: string | null }>
  establishments: Array<{ id: string; name: string; companyId: string | null }>
  templates: Array<{ id: string; name: string; companyId: string | null }>
  riskRules: Array<{ id: string; name: string; companyId: string | null }>
  evaluators: Array<{ id: string; name: string; companyId: string | null }>
  canCreate: boolean
}

export interface EvaluationDraft {
  caseId: string
  establishmentId: string
  inspectionTemplateId: string
  evaluatorId: string
  riskRuleVersionId: string
  scheduledStart: string | null
  scheduledEnd: string | null
}

export interface EvidenceUploadAuthorization {
  bucketName: string
  supabasePath: string
  token: string
  signedUrl: string
}

export interface EvidenceSummary {
  id: string
  evaluationId: string
  evaluationNumber: string
  establishmentName: string
  uploadedBy: string
  uploadedByName: string
  originalName: string
  fileSize: number
  mimeType: string
  evidenceType: string
  synchronizationStatus: string
  uploadedAt: string
}

export interface EvidencesPage {
  items: EvidenceSummary[]
  page: number
  pageSize: number
  total: number
}

export interface ManagedUser {
  id: string
  nombreCompleto: string
  correo: string
  tipoIdentificacion: string
  identificacion: string
  telefono: string | null
  roles: string[]
  empresaId: string | null
  empresaNombre: string | null
  estado: 'ACTIVO' | 'SUSPENDIDO'
  activo: boolean
  versionFila: number
}

export interface ManagedUsersPage {
  items: ManagedUser[]
  page: number
  pageSize: number
  total: number
}

export interface UserManagementOptions {
  roles: Array<{ code: string; name: string }>
  companies: Array<{ id: string; name: string }>
  canManage: boolean
}

export interface ManagedUserDraft {
  nombreCompleto: string
  correo: string
  tipoIdentificacion: string
  identificacion: string
  telefono: string
  empresaId: string | null
  rol: string
  estado: 'ACTIVO' | 'SUSPENDIDO'
  temporaryPassword: string
  versionFila: number | null
}

export interface InspectionRequest {
  id: string
  number: string | null
  companyId: string
  companyName: string
  establishmentId: string | null
  establishmentName: string | null
  applicantId: string
  applicantName: string
  inspectionReasonId: string
  inspectionReasonName: string
  reasonDetail: string | null
  establishmentType: string | null
  observations: string | null
  status: 'BORRADOR' | 'ENVIADA' | 'CANCELADA' | 'RECHAZADA'
  createdAt: string
  submittedAt: string | null
  cancelledAt: string | null
  rowVersion: number
}

export interface InspectionRequestsPage {
  items: InspectionRequest[]
  page: number
  pageSize: number
  total: number
}

export interface InspectionRequestOptions {
  companies: Array<{ id: string; name: string; companyId: string | null }>
  establishments: Array<{ id: string; name: string; companyId: string | null }>
  reasons: Array<{ id: string; name: string; companyId: string | null }>
  canManage: boolean
}

export interface InspectionRequestDraft {
  companyId: string | null
  establishmentId: string | null
  inspectionReasonId: string
  reasonDetail: string
  establishmentType: string
  observations: string
  idempotencyKey: string
  rowVersion: number | null
}

export interface InspectionCase {
  id: string
  number: string
  requestId: string | null
  companyId: string
  companyName: string
  establishmentId: string
  establishmentName: string
  origin: string
  status: string
  priority: number
  responsibleId: string | null
  responsibleName: string | null
  analysisDecision: string | null
  decisionReason: string | null
  openedAt: string
  closedAt: string | null
  rowVersion: number
}

export interface CasesPage {
  items: InspectionCase[]
  page: number
  pageSize: number
  total: number
}

export interface CaseOptions {
  requests: Array<{ id: string; name: string; companyId: string | null }>
  responsibleUsers: Array<{ id: string; name: string; companyId: string | null }>
  canManage: boolean
}

export interface CaseDraft {
  requestId: string
  priority: number
  responsibleId: string | null
  analysisDecision: string | null
  decisionReason: string
  idempotencyKey: string
  rowVersion: number | null
}

export interface Schedule {
  id: string
  caseId: string
  caseNumber: string
  companyName: string
  establishmentName: string
  startsAt: string
  endsAt: string
  priority: number
  status: string
  observations: string | null
  changeReason: string | null
  evaluatorIds: string[]
  evaluatorNames: string[]
  rowVersion: number
}
export interface SchedulesPage {
  items: Schedule[]
  page: number
  pageSize: number
  total: number
}
export interface ScheduleOptions {
  cases: Array<{ id: string; name: string }>
  evaluators: Array<{ id: string; name: string }>
  canManage: boolean
}
export interface ScheduleDraft {
  caseId: string
  startsAt: string
  endsAt: string
  priority: number
  observations: string
  changeReason: string
  evaluatorIds: string[]
  idempotencyKey: string
  rowVersion: number | null
}

export interface Correction {
  id: string
  evaluationId: string
  evaluationNumber: string
  establishmentName: string
  revisionNumber: number
  responsibleType: 'TECNICO' | 'EMPRESA'
  assignedToId: string | null
  assignedToName: string | null
  coordinatorObservation: string
  status: string
  dueAt: string
  requestedAt: string
  submittedAt: string | null
  resolvedAt: string | null
  rowVersion: number
}
export interface CorrectionsPage {
  items: Correction[]
  page: number
  pageSize: number
  total: number
}
export interface CorrectionOptions {
  evaluations: Array<{ id: string; name: string }>
  technicians: Array<{ id: string; name: string }>
  canCreate: boolean
}
export interface CorrectionDraft {
  evaluationId: string
  responsibleType: 'TECNICO' | 'EMPRESA'
  assignedToId: string | null
  coordinatorObservation: string
  dueAt: string
  idempotencyKey: string
}

export function getSession(): AuthSession | null {
  const raw = localStorage.getItem(sessionKey)
  if (!raw) return null
  try {
    return JSON.parse(raw) as AuthSession
  } catch {
    localStorage.removeItem(sessionKey)
    return null
  }
}

export function clearSession() {
  localStorage.removeItem(sessionKey)
  window.dispatchEvent(new Event(sessionChangedEvent))
}

export function subscribeSessionChanges(listener: (session: AuthSession | null) => void) {
  const handleChange = () => listener(getSession())
  window.addEventListener(sessionChangedEvent, handleChange)
  return () => window.removeEventListener(sessionChangedEvent, handleChange)
}

export function getSessionIdentity(session: AuthSession): SessionIdentity {
  const claims = readJwtClaims(session.accessToken)
  const name =
    session.userName ||
    stringClaim(claims, ['name', 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name']) ||
    session.email ||
    stringClaim(claims, ['email']) ||
    'Usuario SIGERSA'
  const email = session.email || stringClaim(claims, ['email']) || ''
  const initials =
    name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase())
      .join('') || 'US'
  return { name, email, initials }
}

export async function logout() {
  const session = getSession()
  if (session) {
    await apiFetch(
      '/api/v1/auth/logout',
      {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: session.refreshToken }),
      },
      false,
    )
  }
  clearSession()
}

export async function login(email: string, password: string) {
  let response: Response
  try {
    response = await fetch(`${apiBaseUrl}/api/v1/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password }),
    })
  } catch {
    throw new ApiError(
      'No fue posible comunicarse con el servidor. Verifique su conexión e inténtelo nuevamente.',
    )
  }

  if (response.status === 401)
    throw new ApiError('El correo o la contraseña no son válidos.', response.status)
  if (response.status === 429)
    throw new ApiError(
      'Se realizaron demasiados intentos. Espere un momento antes de reintentar.',
      response.status,
    )
  if (!response.ok)
    throw new ApiError(
      'No fue posible iniciar sesión en este momento. Inténtelo nuevamente.',
      response.status,
    )

  const session = (await response.json()) as AuthSession
  saveSession(session)
  return session
}

export async function requestPasswordRecovery(email: string) {
  const response = await publicFetch('/api/v1/auth/password-recovery/request', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email }),
  })
  if (!response.ok) throw await apiError(response)
}

export async function verifyPasswordRecovery(email: string, otp: string) {
  const response = await publicFetch('/api/v1/auth/password-recovery/verify', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, otp }),
  })
  if (!response.ok) throw await apiError(response)
  return (await response.json()) as { resetToken: string; expiresAt: string }
}

export async function resetPassword(resetToken: string, newPassword: string) {
  const response = await publicFetch('/api/v1/auth/password-recovery/reset', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      Authorization: `Bearer ${resetToken}`,
    },
    body: JSON.stringify({ newPassword }),
  })
  if (!response.ok) throw await apiError(response)
}

export async function apiFetch(path: string, init: RequestInit = {}, retry = true) {
  const session = getSession()
  const headers = new Headers(init.headers)
  if (session) headers.set('Authorization', `Bearer ${session.accessToken}`)
  const response = await fetch(`${apiBaseUrl}${path}`, { ...init, headers })
  if (response.status !== 401 || !retry || !session?.refreshToken) return response

  const latestSession = getSession()
  if (latestSession?.accessToken && latestSession.accessToken !== session.accessToken) {
    return apiFetch(path, init, false)
  }

  const refreshed = await refreshSessionOnce(session.refreshToken)
  if (!refreshed) return response
  return apiFetch(path, init, false)
}

export async function getAllItems() {
  return getJson<AllItem[]>('/api/v1/all-items')
}

export async function createAllItem(draft: AllItemDraft) {
  return sendJson<AllItem>('/api/v1/all-items', 'POST', draft)
}

export async function updateAllItem(id: number, draft: AllItemDraft) {
  await sendJson<void>(`/api/v1/all-items/${id}`, 'PUT', draft)
}

export async function deleteAllItem(
  id: number,
  childStrategy?: 'SUBTREE' | 'REPARENT',
  reparentToItems?: number,
) {
  const query = new URLSearchParams()
  if (childStrategy) query.set('childStrategy', childStrategy)
  if (reparentToItems !== undefined) query.set('reparentToItems', String(reparentToItems))
  const suffix = query.size > 0 ? `?${query}` : ''
  const response = await apiFetch(`/api/v1/all-items/${id}${suffix}`, { method: 'DELETE' })
  if (!response.ok) throw await apiError(response)
}

export async function reorderAllItems(orderedItems: number[]) {
  return sendJson<AllItem[]>('/api/v1/all-items/order', 'PUT', { orderedItems })
}

export async function calculateInspectionRisk(ratings: Array<{ item: number; rating: string }>) {
  return sendJson<AllItemScore[]>('/api/v1/risk/all-items/calculate', 'POST', { ratings })
}

export async function getEvaluationForm(evaluationId: string) {
  return getJson<EvaluationFormItem[]>(`/api/v1/evaluations/${evaluationId}/form`)
}

export async function getEvaluations(filters: {
  search?: string
  status?: string
  page?: number
  pageSize?: number
}) {
  const query = new URLSearchParams()
  if (filters.search) query.set('search', filters.search)
  if (filters.status) query.set('status', filters.status)
  query.set('page', String(filters.page ?? 1))
  query.set('pageSize', String(filters.pageSize ?? 20))
  return getJson<EvaluationsPage>(`/api/v1/evaluations?${query}`)
}

export async function getEvaluationOptions() {
  return getJson<EvaluationCreateOptions>('/api/v1/evaluations/options')
}

export async function createEvaluation(draft: EvaluationDraft) {
  return sendJson<{ id: string; number: string; rowVersion: number }>(
    '/api/v1/evaluations',
    'POST',
    draft,
  )
}

export async function calculateEvaluation(evaluationId: string, productRisk: number) {
  return sendJson<EvaluationCalculation>(`/api/v1/evaluations/${evaluationId}/calculate`, 'POST', {
    productRisk,
  })
}

export async function requestEvidenceUploadAuthorization(input: {
  evaluationId: string
  idempotencyKey: string
  originalName: string
  mimeType: string
  fileSize: number
}) {
  return sendJson<EvidenceUploadAuthorization>(
    '/api/v1/evidences/upload-authorization',
    'POST',
    input,
  )
}

export async function confirmEvidenceUpload(input: {
  evaluationId: string
  idempotencyKey: string
  supabasePath: string
  originalName: string
  mimeType: string
  evidenceType: string
  fileSize: number
  sha256Hash: string
}) {
  return sendJson<{ id: string }>('/api/v1/evidences/confirm', 'POST', input)
}

export async function getEvidences(filters: {
  search?: string
  evidenceType?: string
  page?: number
  pageSize?: number
}) {
  const query = new URLSearchParams()
  if (filters.search) query.set('search', filters.search)
  if (filters.evidenceType) query.set('evidenceType', filters.evidenceType)
  query.set('page', String(filters.page ?? 1))
  query.set('pageSize', String(filters.pageSize ?? 20))
  return getJson<EvidencesPage>(`/api/v1/evidences?${query}`)
}

export async function downloadEvidence(id: string) {
  const response = await apiFetch(`/api/v1/evidences/${id}/content`)
  if (!response.ok) throw await apiError(response)
  return response.blob()
}

export async function getManagedUsers(filters: {
  search?: string
  role?: string
  status?: string
  page?: number
  pageSize?: number
}) {
  const query = new URLSearchParams()
  if (filters.search) query.set('search', filters.search)
  if (filters.role) query.set('role', filters.role)
  if (filters.status) query.set('status', filters.status)
  query.set('page', String(filters.page ?? 1))
  query.set('pageSize', String(filters.pageSize ?? 10))
  return getJson<ManagedUsersPage>(`/api/v1/users?${query}`)
}

export async function getUserManagementOptions() {
  return getJson<UserManagementOptions>('/api/v1/users/options')
}

export async function createManagedUser(draft: ManagedUserDraft) {
  return sendJson<{ id: string }>('/api/v1/users', 'POST', draft)
}

export async function updateManagedUser(id: string, draft: ManagedUserDraft) {
  return sendJson<void>(`/api/v1/users/${id}`, 'PUT', draft)
}

export async function setManagedUserSuspension(
  id: string,
  suspended: boolean,
  versionFila: number,
) {
  return sendJson<void>(`/api/v1/users/${id}/suspension`, 'POST', {
    suspended,
    versionFila,
  })
}

export async function getInspectionRequests(filters: {
  search?: string
  status?: string
  page?: number
  pageSize?: number
}) {
  const query = new URLSearchParams()
  if (filters.search) query.set('search', filters.search)
  if (filters.status) query.set('status', filters.status)
  query.set('page', String(filters.page ?? 1))
  query.set('pageSize', String(filters.pageSize ?? 10))
  return getJson<InspectionRequestsPage>(`/api/v1/requests?${query}`)
}

export async function getInspectionRequestOptions() {
  return getJson<InspectionRequestOptions>('/api/v1/requests/options')
}

export async function createInspectionRequest(draft: InspectionRequestDraft) {
  const response = await apiFetch('/api/v1/requests', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Idempotency-Key': draft.idempotencyKey,
    },
    body: JSON.stringify(draft),
  })
  if (!response.ok) throw await apiError(response)
  return (await response.json()) as { id: string }
}

export async function updateInspectionRequest(id: string, draft: InspectionRequestDraft) {
  return sendJson<void>(`/api/v1/requests/${id}`, 'PUT', draft)
}

export async function transitionInspectionRequest(
  id: string,
  transition: 'submit' | 'cancel',
  rowVersion: number,
) {
  return sendJson<void>(`/api/v1/requests/${id}/${transition}`, 'POST', { rowVersion })
}

export async function getCases(filters: {
  search?: string
  status?: string
  page?: number
  pageSize?: number
}) {
  const query = new URLSearchParams()
  if (filters.search) query.set('search', filters.search)
  if (filters.status) query.set('status', filters.status)
  query.set('page', String(filters.page ?? 1))
  query.set('pageSize', String(filters.pageSize ?? 10))
  return getJson<CasesPage>(`/api/v1/cases?${query}`)
}

export async function getCaseOptions() {
  return getJson<CaseOptions>('/api/v1/cases/options')
}

export async function createCase(draft: CaseDraft) {
  const response = await apiFetch('/api/v1/cases', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'Idempotency-Key': draft.idempotencyKey },
    body: JSON.stringify(draft),
  })
  if (!response.ok) throw await apiError(response)
  return (await response.json()) as { id: string }
}

export async function updateCase(id: string, draft: CaseDraft) {
  return sendJson<void>(`/api/v1/cases/${id}`, 'PUT', draft)
}

export async function closeCase(id: string, rowVersion: number, reason: string) {
  return sendJson<void>(`/api/v1/cases/${id}/close`, 'POST', { rowVersion, reason })
}

export async function getSchedules(filters: {
  search?: string
  status?: string
  pageSize?: number
}) {
  const query = new URLSearchParams()
  if (filters.search) query.set('search', filters.search)
  if (filters.status) query.set('status', filters.status)
  query.set('pageSize', String(filters.pageSize ?? 50))
  return getJson<SchedulesPage>(`/api/v1/schedules?${query}`)
}
export async function getScheduleOptions() {
  return getJson<ScheduleOptions>('/api/v1/schedules/options')
}
export async function createSchedule(draft: ScheduleDraft) {
  const response = await apiFetch('/api/v1/schedules', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'Idempotency-Key': draft.idempotencyKey },
    body: JSON.stringify(draft),
  })
  if (!response.ok) throw await apiError(response)
  return (await response.json()) as { id: string }
}
export async function updateSchedule(id: string, draft: ScheduleDraft) {
  return sendJson<void>(`/api/v1/schedules/${id}`, 'PUT', draft)
}
export async function cancelSchedule(id: string, rowVersion: number, reason: string) {
  return sendJson<void>(`/api/v1/schedules/${id}/cancel`, 'POST', { rowVersion, reason })
}

export async function getCorrections(filters: {
  search?: string
  status?: string
  pageSize?: number
}) {
  const query = new URLSearchParams()
  if (filters.search) query.set('search', filters.search)
  if (filters.status) query.set('status', filters.status)
  query.set('pageSize', String(filters.pageSize ?? 50))
  return getJson<CorrectionsPage>(`/api/v1/corrections?${query}`)
}
export async function getCorrectionOptions() {
  return getJson<CorrectionOptions>('/api/v1/corrections/options')
}
export async function createCorrection(draft: CorrectionDraft) {
  const response = await apiFetch('/api/v1/corrections', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'Idempotency-Key': draft.idempotencyKey },
    body: JSON.stringify(draft),
  })
  if (!response.ok) throw await apiError(response)
  return (await response.json()) as { id: string }
}
export async function transitionCorrection(
  id: string,
  action: 'submit' | 'accept' | 'reject',
  rowVersion: number,
) {
  return sendJson<void>(`/api/v1/corrections/${id}/${action}`, 'POST', { rowVersion })
}

async function getJson<T>(path: string) {
  const response = await apiFetch(path)
  if (!response.ok) throw await apiError(response)
  return (await response.json()) as T
}

async function sendJson<T>(path: string, method: string, body: unknown) {
  const response = await apiFetch(path, {
    method,
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!response.ok) throw await apiError(response)
  if (response.status === 204) return undefined as T
  return (await response.json()) as T
}

async function refreshSession(refreshToken: string) {
  const response = await fetch(`${apiBaseUrl}/api/v1/auth/refresh`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken }),
  })
  if (!response.ok) {
    clearSession()
    return null
  }
  const session = (await response.json()) as AuthSession
  saveSession(session)
  return session
}

function refreshSessionOnce(refreshToken: string) {
  if (!refreshInFlight) {
    refreshInFlight = refreshSession(refreshToken).finally(() => {
      refreshInFlight = null
    })
  }
  return refreshInFlight
}

async function apiError(response: Response) {
  try {
    const problem = (await response.json()) as { detail?: string; title?: string }
    const fallback =
      response.status === 429
        ? 'Se alcanzó el límite de intentos. Espere unos minutos antes de continuar.'
        : response.status === 503
          ? 'El servicio de correo no está disponible. Inténtelo nuevamente más tarde.'
          : `La API respondió ${response.status}.`
    return new ApiError(problem.detail || problem.title || fallback, response.status)
  } catch {
    return new ApiError(`La API respondió ${response.status}.`, response.status)
  }
}

async function publicFetch(path: string, init: RequestInit) {
  try {
    return await fetch(`${apiBaseUrl}${path}`, init)
  } catch {
    throw new ApiError(
      'No fue posible comunicarse con el servidor. Verifique su conexión e inténtelo nuevamente.',
    )
  }
}

function readJwtClaims(token: string): Record<string, unknown> {
  try {
    const payload = token.split('.')[1]
    if (!payload) return {}
    const base64 = payload.replace(/-/g, '+').replace(/_/g, '/')
    return JSON.parse(decodeURIComponent(escape(atob(base64)))) as Record<string, unknown>
  } catch {
    return {}
  }
}

function stringClaim(claims: Record<string, unknown>, keys: string[]) {
  for (const key of keys) if (typeof claims[key] === 'string') return claims[key]
  return ''
}

function saveSession(session: AuthSession) {
  localStorage.setItem(sessionKey, JSON.stringify(session))
  window.dispatchEvent(new Event(sessionChangedEvent))
}
