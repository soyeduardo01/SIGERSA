const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')
const sessionKey = 'sigersa.auth.session'

export interface AuthSession {
  accessToken: string
  accessTokenExpiresAt: string
  refreshToken: string
  refreshTokenExpiresAt: string
  roles: string[]
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

export interface EvidenceUploadAuthorization {
  bucketName: string
  supabasePath: string
  token: string
  signedUrl: string
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
  const response = await fetch(`${apiBaseUrl}/api/v1/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  })
  if (!response.ok) throw new Error('El correo o la contraseña no son válidos.')
  const session = (await response.json()) as AuthSession
  saveSession(session)
  return session
}

export async function requestPasswordRecovery(email: string) {
  const response = await fetch(`${apiBaseUrl}/api/v1/auth/password-recovery/request`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email }),
  })
  if (!response.ok) throw await apiError(response)
}

export async function verifyPasswordRecovery(email: string, otp: string) {
  const response = await fetch(`${apiBaseUrl}/api/v1/auth/password-recovery/verify`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, otp }),
  })
  if (!response.ok) throw await apiError(response)
  return (await response.json()) as { resetToken: string; expiresAt: string }
}

export async function resetPassword(resetToken: string, newPassword: string) {
  const response = await fetch(`${apiBaseUrl}/api/v1/auth/password-recovery/reset`, {
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

  const refreshed = await refreshSession(session.refreshToken)
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

async function apiError(response: Response) {
  try {
    const problem = (await response.json()) as { detail?: string; title?: string }
    return new Error(problem.detail || problem.title || `La API respondió ${response.status}.`)
  } catch {
    return new Error(`La API respondió ${response.status}.`)
  }
}

function saveSession(session: AuthSession) {
  localStorage.setItem(sessionKey, JSON.stringify(session))
}
