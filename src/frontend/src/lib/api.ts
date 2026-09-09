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

export async function deleteAllItem(id: number) {
  const response = await apiFetch(`/api/v1/all-items/${id}`, { method: 'DELETE' })
  if (!response.ok) throw await apiError(response)
}

export async function calculateInspectionRisk(ratings: Array<{ item: number; rating: string }>) {
  return sendJson<AllItemScore[]>('/api/v1/risk/all-items/calculate', 'POST', { ratings })
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
