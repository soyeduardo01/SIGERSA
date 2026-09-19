import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getAllParameters, getEstablishmentOptions, getEvaluations } from '../lib/api'
import { refreshOfflineMirror } from './evaluationBootstrap'
import { prepareEvaluationOffline } from './evaluationCache'
import { replaceCachedEvaluations } from './evaluationListCache'
import { replaceCachedParameters } from './parameterCache'

vi.mock('../lib/api', () => ({
  getAllItems: vi.fn().mockResolvedValue([]),
  getAllParameters: vi.fn(),
  getCaseOptions: vi.fn().mockResolvedValue({}),
  getCorrectionOptions: vi.fn().mockResolvedValue({}),
  getEstablishmentOptions: vi.fn().mockResolvedValue({}),
  getEvaluationOptions: vi.fn().mockResolvedValue({}),
  getEvaluations: vi.fn(),
  getFindingOptions: vi.fn().mockResolvedValue({}),
  getInspectionRequestOptions: vi.fn().mockResolvedValue({}),
  getScheduleOptions: vi.fn().mockResolvedValue({}),
  getSurveillanceOptions: vi.fn().mockResolvedValue({}),
  getUserManagementOptions: vi.fn().mockResolvedValue({}),
}))
vi.mock('./evaluationCache', () => ({ prepareEvaluationOffline: vi.fn() }))
vi.mock('./evaluationListCache', () => ({ replaceCachedEvaluations: vi.fn() }))
vi.mock('./parameterCache', () => ({ replaceCachedParameters: vi.fn() }))

const evaluation = {
  id: 'evaluation-1',
  number: 'EV-1',
  caseId: 'case-1',
  caseNumber: 'CA-1',
  establishmentId: 'establishment-1',
  establishmentName: 'Planta Central',
  evaluatorId: 'user-1',
  evaluatorName: 'Técnico',
  status: 'EN_EJECUCION',
  scheduledStart: null,
  scheduledEnd: null,
  compliancePercentage: null,
  totalRisk: null,
  riskLevel: null,
  frequency: null,
  nextInspectionAt: null,
  answeredItems: 0,
  hasOfficialReport: false,
  canEdit: true,
  rowVersion: 1,
}

describe('espejo local offline', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(getEvaluations)
      .mockResolvedValueOnce({ items: [evaluation], page: 1, pageSize: 100, total: 2 })
      .mockResolvedValueOnce({
        items: [{ ...evaluation, id: 'evaluation-2', number: 'EV-2', canEdit: false }],
        page: 2,
        pageSize: 100,
        total: 2,
      })
    vi.mocked(getAllParameters).mockResolvedValue([
      {
        parametersId: 1,
        keyWord: 'ESTADO_EVALUACION',
        companyCode: null,
        oCode: null,
        cCode: null,
        numericData: 1,
        doubleData: null,
        stringData: 'ASIGNADA',
        booleanData: null,
        dateData: null,
        status: true,
      },
    ])
  })

  it('descarga todas las páginas, todos los parámetros y prepara las fichas editables', async () => {
    await refreshOfflineMirror()

    expect(getEvaluations).toHaveBeenNthCalledWith(1, { page: 1, pageSize: 100 })
    expect(getEvaluations).toHaveBeenNthCalledWith(2, { page: 2, pageSize: 100 })
    expect(getAllParameters).toHaveBeenCalledWith('', true)
    expect(replaceCachedEvaluations).toHaveBeenCalledWith([
      evaluation,
      expect.objectContaining({ id: 'evaluation-2' }),
    ])
    expect(replaceCachedParameters).toHaveBeenCalledWith([
      expect.objectContaining({ keyWord: 'ESTADO_EVALUACION' }),
    ])
    expect(getEstablishmentOptions).toHaveBeenCalledTimes(1)
    expect(prepareEvaluationOffline).toHaveBeenCalledWith('evaluation-1')
    expect(prepareEvaluationOffline).toHaveBeenCalledTimes(1)
  })

  it('actualiza los catálogos aunque todavía existan mutaciones de evaluaciones pendientes', async () => {
    await refreshOfflineMirror(false)

    expect(getAllParameters).toHaveBeenCalledWith('', true)
    expect(replaceCachedParameters).toHaveBeenCalledTimes(1)
    expect(getEvaluations).not.toHaveBeenCalled()
  })

  it('prepara también las fichas asignadas para que puedan iniciarse sin conexión', async () => {
    vi.mocked(getEvaluations)
      .mockReset()
      .mockResolvedValue({
        items: [{ ...evaluation, status: 'ASIGNADA', canEdit: false }],
        page: 1,
        pageSize: 100,
        total: 1,
      })

    await refreshOfflineMirror()

    expect(prepareEvaluationOffline).toHaveBeenCalledWith('evaluation-1')
  })
})
