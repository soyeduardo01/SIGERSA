import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { RequestsManagement } from './RequestsManagement'

const mocks = vi.hoisted(() => ({
  getInspectionRequests: vi.fn(),
  transitionInspectionRequest: vi.fn(),
  confirm: vi.fn(async () => true),
}))

const request = {
  id: 'request-1',
  number: null,
  companyId: 'company-1',
  companyName: 'Empresa de prueba',
  establishmentId: 'establishment-1',
  establishmentName: 'Planta Norte',
  applicantId: 'user-1',
  applicantName: 'Solicitante',
  inspectionReasonId: 'reason-1',
  inspectionReasonName: 'Solicitud de permiso',
  reasonDetail: null,
  establishmentType: 'Planta procesadora de alimentos',
  observations: null,
  documentCount: 1,
  status: 'BORRADOR' as const,
  createdAt: '2026-09-14T12:00:00Z',
  submittedAt: null,
  cancelledAt: null,
  rowVersion: 1,
}

vi.mock('../../lib/api', () => ({
  getInspectionRequestOptions: vi.fn(async () => ({
    companies: [{ id: 'company-1', name: 'Empresa de prueba', companyId: null }],
    establishments: [
      { id: 'establishment-1', name: 'Planta Norte', companyId: 'company-1' },
    ],
    reasons: [{ id: 'reason-1', name: 'Solicitud de permiso', companyId: null }],
    establishmentTypes: ['Planta procesadora de alimentos', 'Restaurante'],
    canManage: true,
  })),
  getInspectionRequests: mocks.getInspectionRequests,
  transitionInspectionRequest: mocks.transitionInspectionRequest,
  createInspectionRequest: vi.fn(),
  updateInspectionRequest: vi.fn(),
  uploadInspectionRequestDocument: vi.fn(),
}))

vi.mock('../../lib/alerts', () => ({
  alerts: {
    confirm: mocks.confirm,
    success: vi.fn(),
    error: vi.fn(),
  },
}))

vi.mock('../../offline/syncQueue', () => ({ queueRequest: vi.fn() }))

describe('RequestsManagement', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.getInspectionRequests.mockResolvedValue({
      items: [request],
      page: 1,
      pageSize: 10,
      total: 1,
    })
  })

  it('consulta la versión más reciente antes de enviar la solicitud', async () => {
    mocks.getInspectionRequests
      .mockResolvedValueOnce({ items: [request], page: 1, pageSize: 10, total: 1 })
      .mockResolvedValueOnce({
        items: [{ ...request, rowVersion: 2 }],
        page: 1,
        pageSize: 10,
        total: 1,
      })

    render(<RequestsManagement />)
    fireEvent.click(await screen.findByRole('button', { name: 'Enviar' }))

    await waitFor(() =>
      expect(mocks.transitionInspectionRequest).toHaveBeenCalledWith('request-1', 'submit', 2),
    )
  })

  it('rechaza documentos mayores de 5 MB antes de subirlos', async () => {
    render(<RequestsManagement />)
    fireEvent.click(await screen.findByRole('button', { name: '+ Nueva solicitud' }))

    const input = screen.getByLabelText(/Documentación de soporte obligatoria/)
    const oversized = new File(['contenido'], 'soporte.pdf', { type: 'application/pdf' })
    Object.defineProperty(oversized, 'size', { value: 5 * 1024 * 1024 + 1 })
    fireEvent.change(input, { target: { files: [oversized] } })

    expect(screen.getByRole('alert')).toHaveTextContent('no puede superar 5 MB')
    expect(input).toHaveValue('')
  })
})
