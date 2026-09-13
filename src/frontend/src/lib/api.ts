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

export interface TwoFactorChallenge {
  requiresTwoFactor: true
  expiresAt: string
  provider?: 'EMAIL_OTP' | 'SUPABASE_TOTP'
}

export interface PublicRegistrationDraft {
  nombreCompleto: string
  tipoIdentificacion: 'CEDULA' | 'PASAPORTE'
  identificacion: string
  correo: string
  telefono: string
  rol: 'ADMINISTRADOR_EMPRESA' | 'USUARIO_DELEGADO'
  password: string
  termsAccepted: boolean
  authorizationLetter: File
}

export interface SessionIdentity {
  name: string
  email: string
  initials: string
}

export interface ParameterControl {
  parametersId: number
  keyWord: string
  companyCode: number | null
  oCode: number | null
  cCode: string | null
  numericData: number | null
  doubleData: number | null
  stringData: string | null
  booleanData: boolean | null
  dateData: string | null
  status: boolean
}

export interface ParameterControlDraft {
  keyWord: string
  companyCode: number | null
  oCode: number | null
  cCode: string | null
  numericData: number | null
  doubleData: number | null
  stringData: string | null
  booleanData: boolean | null
  dateData: string | null
}

export interface UserProfile {
  id: string
  fullName: string
  email: string
  phone: string | null
  status: string
  lastAccessAt: string | null
  mfaEnabled: boolean
  rowVersion: number
}

export interface CompanySummary {
  id: string
  legalName: string
  taxId: string
  tradeName: string | null
  economicActivity: string | null
  phone: string | null
  email: string | null
  address: string | null
  municipalityId: string | null
  municipalityName: string | null
  provinceName: string | null
  contacts: CompanyContactDraft[]
  status: string
  rowVersion: number
}

export interface CompaniesPage {
  items: CompanySummary[]
  page: number
  pageSize: number
  total: number
}

export interface CompanyDraft {
  legalName: string
  taxId: string
  tradeName: string | null
  economicActivity: string | null
  phone: string | null
  email: string | null
  address: string | null
  municipalityId: string | null
  contacts: CompanyContactDraft[]
  status: string
  rowVersion: number | null
}

export interface CompanyContactDraft {
  type: 'LEGAL' | 'CALIDAD' | 'PRINCIPAL'
  fullName: string
  identification: string | null
  phone: string | null
  email: string | null
}

export interface EstablishmentSummary {
  id: string
  code: string
  name: string
  companyId: string
  companyName: string
  provinceName: string | null
  municipalityName: string | null
  phone: string | null
  email: string | null
  status: string
  rowVersion: number
}

export interface EstablishmentsPage {
  items: EstablishmentSummary[]
  page: number
  pageSize: number
  total: number
}

export interface EstablishmentOption {
  id: string
  code: string
  name: string
}

export interface MunicipalityOption extends EstablishmentOption {
  provinceId: string
}

export interface SubcategoryOption extends EstablishmentOption {
  categoryId: string
  riskLevel: number | null
}

export interface EstablishmentOptions {
  companies: EstablishmentOption[]
  provinces: EstablishmentOption[]
  municipalities: MunicipalityOption[]
  dpsDas: EstablishmentOption[]
  commercializations: EstablishmentOption[]
  markets: EstablishmentOption[]
  categories: EstablishmentOption[]
  subcategories: SubcategoryOption[]
  statuses: ParameterControl[]
  haccpLevels: ParameterControl[]
  samplingApplications: ParameterControl[]
  inabieDistributions: ParameterControl[]
}

export interface EstablishmentContactDraft {
  type: 'PRINCIPAL' | 'LEGAL'
  fullName: string
  identification: string | null
  phone: string | null
  email: string | null
}

export interface EstablishmentProductDraft {
  subcategoryId: string
  description: string
  monthlyVolume: number | null
  unit: string | null
}

export interface EstablishmentDraft {
  companyId: string
  municipalityId: string | null
  dpsDasId: string | null
  commercializationId: string | null
  code: string
  name: string
  street: string | null
  addressNumber: string | null
  phone: string | null
  email: string | null
  operationsStartDate: string | null
  sanitaryPermitNumber: string | null
  sanitaryPermitExpiresAt: string | null
  productsDescription: string | null
  annualProduction: number | null
  femaleEmployees: number | null
  maleEmployees: number | null
  microbiologicalRejectionsLastFiveYears: number
  haccpImplemented: boolean | null
  haccpPercentage: number | null
  microbiologicalSamplingPlan: boolean | null
  samplingApplicationCode: string | null
  isInabieSupplier: boolean | null
  inabieDistributionCode: string | null
  status: string
  marketIds: string[]
  contacts: EstablishmentContactDraft[]
  products: EstablishmentProductDraft[]
  rowVersion: number | null
}

export interface EstablishmentDetails {
  id: string
  data: EstablishmentDraft
  companyName: string
  provinceId: string | null
}

export interface DashboardSnapshot {
  activeCases: number
  pendingEvaluations: number
  criticalAlerts: number
  averageCompliance: number | null
  myRequests: number
  unreadNotifications: number
  scheduledEvaluations: number
  openComplaints: number
  pendingAssignments: number
  pendingReports: number
  recentEvaluations: Array<{
    id: string
    number: string
    establishmentName: string
    status: string
    compliancePercentage: number | null
    riskLevel: string | null
    updatedAt: string
  }>
  riskDistribution: Array<{ level: string; total: number }>
  upcomingSchedules: Array<{
    id: string
    caseNumber: string
    establishmentName: string
    startsAt: string
    status: string
  }>
}

export interface SurveillanceRecord {
  id: string
  kind: 'ALERTA_LAPCH' | 'DENUNCIA'
  number: string
  occurredAt: string
  companyId: string | null
  companyName: string | null
  establishmentId: string | null
  establishmentName: string | null
  subject: string
  description: string
  priority: number
  channel: string | null
  isAnonymous: boolean
  isConfidential: boolean
  result: string | null
  hasCase: boolean
  rowVersion: number
}

export interface SurveillancePage {
  items: SurveillanceRecord[]
  page: number
  pageSize: number
  total: number
}

export interface SurveillanceOptions {
  companies: Array<{ id: string; name: string; companyId: string | null }>
  establishments: Array<{ id: string; name: string; companyId: string | null }>
  canManage: boolean
}

export interface SurveillanceDraft {
  kind: 'ALERTA_LAPCH' | 'DENUNCIA'
  occurredAt: string
  companyId: string | null
  establishmentId: string | null
  subject: string
  description: string
  priority: number
  channel: string | null
  isAnonymous: boolean
  isConfidential: boolean
  result: string | null
  rowVersion: number | null
}

export interface FindingRecord {
  id: string
  evaluationId: string
  evaluationNumber: string
  establishmentName: string
  sourceItem: number
  itemTitle: string
  criticality: string
  code: string
  description: string
  status: string
  detectedAt: string
  closedAt: string | null
  rowVersion: number
}

export interface FindingsPage {
  items: FindingRecord[]
  page: number
  pageSize: number
  total: number
}

export interface FindingOptions {
  evaluations: Array<{ id: string; name: string; companyId: string | null }>
  criticalities: Array<{ id: string; name: string; companyId: string | null }>
  canCreate: boolean
}

export interface HistoricalEvaluation {
  id: string
  number: string
  caseNumber: string
  companyName: string
  establishmentName: string
  evaluatorName: string
  status: string
  compliancePercentage: number | null
  totalRisk: number | null
  riskLevel: string | null
  frequency: string | null
  createdAt: string
  closedAt: string | null
  reportId: string | null
  reportNumber: string | null
  reportStatus: string | null
  hasOfficialReport: boolean
}

export interface HistoricalEvaluationsPage {
  items: HistoricalEvaluation[]
  page: number
  pageSize: number
  total: number
}

export interface TimelineEvent {
  occurredAt: string
  eventType: string
  title: string
  detail: string | null
  actorName: string | null
}

export interface AuditEventRecord {
  id: string
  occurredAt: string
  action: string
  resourceType: string
  resourceId: string | null
  result: string
  actorName: string | null
  reason: string | null
  correlationId: string | null
}

export interface AuditEventsPage {
  items: AuditEventRecord[]
  page: number
  pageSize: number
  total: number
}

export interface ReportFileReference {
  reportId: string
  reportNumber: string
  version: number
  mimeType: string
  fileName: string
  isOfficial: boolean
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

export interface EvaluationSavedAnswer {
  sourceItem: number
  rating: string
  criticalityCode: string | null
  observation: string | null
  comment: string | null
}

export interface EvaluationFollowUpItem {
  detail: string
  dueDate: string | null
}

export interface EvaluationSupplement {
  previousInspectionDate: string | null
  previousQualification: string | null
  currentInspectionDate: string | null
  currentQualification: string | null
  dpsDasOfficer1: string | null
  dpsDasOfficer2: string | null
  digemapsTechnician1: string | null
  digemapsTechnician2: string | null
  correctiveMeasures: EvaluationFollowUpItem[]
  recommendations: EvaluationFollowUpItem[]
  rowVersion: number
}

export interface EvaluationWorkspace {
  items: EvaluationFormItem[]
  answers: EvaluationSavedAnswer[]
  supplement: EvaluationSupplement
  policy: EvaluationInspectionPolicy
}

export interface EvaluationInspectionPolicy {
  mode: 'FICHA_COMPLETA' | 'DESDE_1_1_3' | 'NO_CONFORMIDADES_ANTERIORES' | 'DENUNCIA_DISCRECIONAL'
  title: string
  explanation: string
  isReady: boolean
  blockingReason: string | null
  requiredSourceItems: number[]
  excludedSourceItems: number[]
  approvalPurpose: string | null
  previousEvaluationId: string | null
  previousInspectionDate: string | null
  previousCompliancePercentage: number | null
}

export interface EvaluationDecisionGuidance {
  band: string
  condition: string
  primaryRecommendation: string
  approvalEligible: boolean | null
  applicableItems: number
  criticalNonconformities: number
  majorNonconformities: number
  minorNonconformities: number
  messages: string[]
}

export interface EvaluationCalculation {
  evaluationId: string
  compliancePercentage: number | null
  productRisk: number | null
  establishmentRisk: number | null
  totalRisk: number | null
  riskLevel: 'BAJO' | 'MEDIO' | 'ALTO' | 'NO_CALCULABLE'
  frequency: 'ANUAL' | 'SEMESTRAL' | 'TRIMESTRAL' | 'NO_APLICA'
  decision: EvaluationDecisionGuidance
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
  estado: string
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
  estado: string
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
  documentCount: number
  status: 'BORRADOR' | 'PENDIENTE_ASIGNACION' | 'CANCELADA' | 'RECHAZADA'
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
  establishmentTypes: string[]
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
  alertId: string | null
  complaintId: string | null
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
  sources: Array<{
    id: string
    kind: 'SOLICITUD_EMPRESA' | 'PROGRAMACION' | 'ALERTA_LAPCH' | 'DENUNCIA'
    name: string
    companyId: string
    establishmentId: string
  }>
  responsibleUsers: Array<{ id: string; name: string; companyId: string | null }>
  canManage: boolean
}

export interface CaseDraft {
  origin: 'SOLICITUD_EMPRESA' | 'PROGRAMACION' | 'ALERTA_LAPCH' | 'DENUNCIA'
  sourceId: string
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
  caseOrigin: string
  requestNumber: string | null
  location: string | null
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
  fields: Array<{
    sourceItem: number
    itemTitle: string
    reason: string
    status: string
    previousSnapshotJson: string | null
    newSnapshotJson: string | null
  }>
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
  fields: Array<{ sourceItem: number; reason: string }>
}

export function getSession(): AuthSession | null {
  const raw = localStorage.getItem(sessionKey) ?? sessionStorage.getItem(sessionKey)
  if (!raw) return null
  try {
    return JSON.parse(raw) as AuthSession
  } catch {
    localStorage.removeItem(sessionKey)
    sessionStorage.removeItem(sessionKey)
    return null
  }
}

export function clearSession() {
  localStorage.removeItem(sessionKey)
  sessionStorage.removeItem(sessionKey)
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

export async function login(email: string, password: string, rememberSession = false) {
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

  if (response.status === 202) return (await response.json()) as TwoFactorChallenge

  const session = (await response.json()) as AuthSession
  saveSession(session, rememberSession)
  return session
}

export async function verifyTwoFactor(email: string, otp: string, rememberSession = false) {
  const response = await publicFetch('/api/v1/auth/login/verify-2fa', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, otp }),
  })
  if (response.status === 401) throw new ApiError('El código no es válido.', response.status)
  if (!response.ok) throw await apiError(response)
  const session = (await response.json()) as AuthSession
  saveSession(session, rememberSession)
  return session
}

export async function verifySupabaseMfa(
  email: string,
  supabaseAccessToken: string,
  rememberSession = false,
) {
  const response = await publicFetch('/api/v1/auth/login/verify-supabase-mfa', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, supabaseAccessToken }),
  })
  if (response.status === 401)
    throw new ApiError('El código de la aplicación autenticadora no es válido.', response.status)
  if (!response.ok) throw await apiError(response)
  const session = (await response.json()) as AuthSession
  saveSession(session, rememberSession)
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

export async function registerPublicUser(draft: PublicRegistrationDraft) {
  const form = new FormData()
  form.append('nombreCompleto', draft.nombreCompleto)
  form.append('tipoIdentificacion', draft.tipoIdentificacion)
  form.append('identificacion', draft.identificacion)
  form.append('correo', draft.correo)
  form.append('telefono', draft.telefono)
  form.append('rol', draft.rol)
  form.append('password', draft.password)
  form.append('termsAccepted', String(draft.termsAccepted))
  form.append('authorizationLetter', draft.authorizationLetter)
  const response = await publicFetch('/api/v1/registrations', { method: 'POST', body: form })
  if (!response.ok) throw await apiError(response)
  return (await response.json()) as { id: string; status: 'PENDIENTE_VALIDACION' }
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

export async function getParameters(keyWord: string, companyCode?: number) {
  const query = new URLSearchParams()
  if (companyCode !== undefined) query.set('companyCode', String(companyCode))
  const suffix = query.size > 0 ? `?${query}` : ''
  return getJson<ParameterControl[]>(`/api/v1/parameters/${encodeURIComponent(keyWord)}${suffix}`)
}

export async function getAllParameters(search = '') {
  const query = new URLSearchParams()
  if (search) query.set('search', search)
  const suffix = query.size ? `?${query}` : ''
  return getJson<ParameterControl[]>(`/api/v1/parameters${suffix}`)
}

export async function createParameter(draft: ParameterControlDraft) {
  return sendJson<{ parametersId: number }>('/api/v1/parameters', 'POST', draft)
}

export async function updateParameter(id: number, draft: ParameterControlDraft) {
  return sendJson<void>(`/api/v1/parameters/${id}`, 'PUT', draft)
}

export async function deleteParameter(id: number) {
  const response = await apiFetch(`/api/v1/parameters/${id}`, { method: 'DELETE' })
  if (!response.ok) throw await apiError(response)
}

export async function activateParameter(id: number) {
  const response = await apiFetch(`/api/v1/parameters/${id}/activate`, { method: 'POST' })
  if (!response.ok) throw await apiError(response)
}

export async function getProfile() {
  return getJson<UserProfile>('/api/v1/profile')
}

export async function getEstablishments(filters: {
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
  return getJson<EstablishmentsPage>(`/api/v1/establishments?${query}`)
}

export async function getCompanies(filters: {
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
  return getJson<CompaniesPage>(`/api/v1/companies?${query}`)
}

export async function createCompany(draft: CompanyDraft) {
  return sendJson<{ id: string }>('/api/v1/companies', 'POST', draft)
}

export async function updateCompany(id: string, draft: CompanyDraft) {
  return sendJson<void>(`/api/v1/companies/${id}`, 'PUT', draft)
}

export async function getEstablishment(id: string) {
  return getJson<EstablishmentDetails>(`/api/v1/establishments/${id}`)
}

export async function getEstablishmentOptions() {
  return getJson<EstablishmentOptions>('/api/v1/establishments/options')
}

export async function createEstablishment(draft: EstablishmentDraft) {
  return sendJson<{ id: string }>('/api/v1/establishments', 'POST', draft)
}

export async function updateEstablishment(id: string, draft: EstablishmentDraft) {
  return sendJson<void>(`/api/v1/establishments/${id}`, 'PUT', draft)
}

export async function updateProfile(input: {
  fullName: string
  email: string
  phone: string | null
  rowVersion: number
}) {
  return sendJson<UserProfile>('/api/v1/profile', 'PUT', input)
}

export async function changeProfilePassword(input: {
  currentPassword: string
  newPassword: string
  confirmPassword: string
  rowVersion: number
}) {
  return sendJson<void>('/api/v1/profile/password', 'PUT', input)
}

export async function beginMfaEnrollment(currentPassword: string) {
  return sendJson<{ email: string }>('/api/v1/profile/mfa/enrollment', 'POST', {
    currentPassword,
  })
}

export async function completeMfaEnrollment(factorId: string, supabaseAccessToken: string) {
  return sendJson<UserProfile>('/api/v1/profile/mfa/enrollment/complete', 'POST', {
    factorId,
    supabaseAccessToken,
  })
}

export async function disableMfa(input: {
  currentPassword: string
  factorId: string
  supabaseAccessToken: string
}) {
  return sendJson<UserProfile>('/api/v1/profile/mfa/disable', 'POST', input)
}

export function updateSessionIdentity(userName: string, email: string) {
  const session = getSession()
  if (!session) return
  saveSession({ ...session, userName, email })
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

export async function getEvaluationWorkspace(evaluationId: string) {
  return getJson<EvaluationWorkspace>(`/api/v1/evaluations/${evaluationId}/workspace`)
}

export async function saveEvaluationSupplement(
  evaluationId: string,
  supplement: EvaluationSupplement,
) {
  return sendJson<EvaluationSupplement>(
    `/api/v1/evaluations/${evaluationId}/supplement`,
    'PUT',
    supplement,
  )
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

export async function startEvaluation(
  evaluationId: string,
  rowVersion: number,
  location?: { latitude: number; longitude: number; accuracyMeters: number },
) {
  return sendJson<number>(`/api/v1/evaluations/${evaluationId}/start`, 'POST', {
    rowVersion,
    latitude: location?.latitude ?? null,
    longitude: location?.longitude ?? null,
    accuracyMeters: location?.accuracyMeters ?? null,
  })
}

export async function finalizeEvaluation(evaluationId: string, productRisk: number) {
  return sendJson<EvaluationCalculation>(`/api/v1/evaluations/${evaluationId}/finalize`, 'POST', {
    productRisk,
  })
}

export async function transitionEvaluation(
  evaluationId: string,
  action: 'submit' | 'review' | 'approve' | 'close',
  rowVersion: number,
) {
  return sendJson<number>(`/api/v1/evaluations/${evaluationId}/${action}`, 'POST', { rowVersion })
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
  sourceItem?: number
  latitude?: number
  longitude?: number
  accuracyMeters?: number
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

export async function uploadUserAuthorizationLetter(id: string, file: File) {
  const form = new FormData()
  form.append('file', file)
  const response = await apiFetch(`/api/v1/users/${id}/authorization-letter`, {
    method: 'POST',
    body: form,
  })
  if (!response.ok) throw await apiError(response)
  return (await response.json()) as { id: string }
}

export async function downloadUserAuthorizationLetter(id: string) {
  const response = await apiFetch(`/api/v1/users/${id}/authorization-letter/content`)
  if (!response.ok) throw await apiError(response)
  const blob = await response.blob()
  return {
    blob,
    fileName: downloadFileName(response.headers.get('Content-Disposition'), blob.type),
  }
}

function downloadFileName(contentDisposition: string | null, mimeType: string) {
  const encodedName = /filename\*=UTF-8''([^;]+)/i.exec(contentDisposition ?? '')?.[1]
  if (encodedName) {
    try {
      return decodeURIComponent(encodedName.trim().replace(/^"|"$/g, ''))
    } catch {
      // Continue with the regular filename or a MIME-based fallback.
    }
  }

  const regularName = /filename="?([^";]+)"?/i.exec(contentDisposition ?? '')?.[1]?.trim()
  if (regularName) return regularName

  const extension =
    mimeType === 'application/pdf'
      ? '.pdf'
      : mimeType === 'image/png'
        ? '.png'
        : mimeType === 'image/jpeg'
          ? '.jpg'
          : ''
  return `carta-autorizacion${extension}`
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

export async function uploadInspectionRequestDocument(
  id: string,
  file: File,
  documentType = 'SOPORTE_BPM',
) {
  const form = new FormData()
  form.append('file', file)
  form.append('documentType', documentType)
  form.append('required', 'true')
  const response = await apiFetch(`/api/v1/requests/${id}/documents`, {
    method: 'POST',
    body: form,
  })
  if (!response.ok) throw await apiError(response)
  return (await response.json()) as { id: string }
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

export async function getDashboard() {
  return getJson<DashboardSnapshot>('/api/v1/dashboard')
}

export async function getSurveillance(filters: {
  search?: string
  kind?: string
  result?: string
  pageSize?: number
}) {
  const query = new URLSearchParams()
  if (filters.search) query.set('search', filters.search)
  if (filters.kind) query.set('kind', filters.kind)
  if (filters.result) query.set('result', filters.result)
  query.set('pageSize', String(filters.pageSize ?? 50))
  return getJson<SurveillancePage>(`/api/v1/surveillance?${query}`)
}

export async function getSurveillanceOptions() {
  return getJson<SurveillanceOptions>('/api/v1/surveillance/options')
}

export async function createSurveillance(draft: SurveillanceDraft) {
  return sendJson<{ id: string }>('/api/v1/surveillance', 'POST', draft)
}

export async function updateSurveillance(id: string, draft: SurveillanceDraft) {
  return sendJson<void>(`/api/v1/surveillance/${id}`, 'PUT', draft)
}

export async function getFindings(filters: {
  search?: string
  status?: string
  pageSize?: number
}) {
  const query = new URLSearchParams()
  if (filters.search) query.set('search', filters.search)
  if (filters.status) query.set('status', filters.status)
  query.set('pageSize', String(filters.pageSize ?? 50))
  return getJson<FindingsPage>(`/api/v1/findings?${query}`)
}

export async function getFindingOptions() {
  return getJson<FindingOptions>('/api/v1/findings/options')
}

export async function createFinding(input: {
  evaluationId: string
  sourceItem: number
  criticalityId: string
  description: string
}) {
  return sendJson<{ id: string }>('/api/v1/findings', 'POST', input)
}

export async function closeFinding(id: string, rowVersion: number, reason: string) {
  return sendJson<void>(`/api/v1/findings/${id}/close`, 'POST', { rowVersion, reason })
}

export async function getEvaluationHistory(filters: {
  search?: string
  status?: string
  from?: string
  to?: string
  pageSize?: number
}) {
  const query = new URLSearchParams()
  if (filters.search) query.set('search', filters.search)
  if (filters.status) query.set('status', filters.status)
  if (filters.from) query.set('from', filters.from)
  if (filters.to) query.set('to', filters.to)
  query.set('pageSize', String(filters.pageSize ?? 50))
  return getJson<HistoricalEvaluationsPage>(`/api/v1/history/evaluations?${query}`)
}

export async function getEvaluationTimeline(evaluationId: string) {
  return getJson<TimelineEvent[]>(`/api/v1/history/evaluations/${evaluationId}/timeline`)
}

export async function getAuditEvents(filters: {
  search?: string
  result?: string
  from?: string
  to?: string
  pageSize?: number
}) {
  const query = new URLSearchParams()
  if (filters.search) query.set('search', filters.search)
  if (filters.result) query.set('result', filters.result)
  if (filters.from) query.set('from', filters.from)
  if (filters.to) query.set('to', filters.to)
  query.set('pageSize', String(filters.pageSize ?? 50))
  return getJson<AuditEventsPage>(`/api/v1/audit-events?${query}`)
}

export async function generateEvaluationReport(evaluationId: string, official: boolean) {
  return sendJson<ReportFileReference>(
    `/api/v1/reports/evaluations/${evaluationId}/generate`,
    'POST',
    { official },
  )
}

export async function downloadEvaluationReport(reportId: string) {
  const response = await apiFetch(`/api/v1/reports/${reportId}/content`)
  if (!response.ok) throw await apiError(response)
  return response.blob()
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

export async function apiError(response: Response) {
  const fallback =
    response.status === 429
      ? 'Se alcanzó el límite temporal de intentos. Espere unos minutos antes de continuar.'
      : response.status === 503
        ? 'El servicio de correo no está disponible. Inténtelo nuevamente más tarde.'
        : `La API respondió ${response.status}.`

  try {
    const problem = (await response.json()) as { detail?: string; title?: string }
    return new ApiError(problem.detail || problem.title || fallback, response.status)
  } catch {
    return new ApiError(fallback, response.status)
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

function saveSession(session: AuthSession, persistent?: boolean) {
  const usePersistentStorage = persistent ?? localStorage.getItem(sessionKey) !== null
  const targetStorage = usePersistentStorage ? localStorage : sessionStorage
  const otherStorage = usePersistentStorage ? sessionStorage : localStorage
  otherStorage.removeItem(sessionKey)
  targetStorage.setItem(sessionKey, JSON.stringify(session))
  window.dispatchEvent(new Event(sessionChangedEvent))
}
