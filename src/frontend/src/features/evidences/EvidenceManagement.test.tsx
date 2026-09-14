import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { EvidenceManagement } from './EvidenceManagement'

const mocks = vi.hoisted(() => ({
  downloadEvidence: vi.fn(),
  createObjectURL: vi.fn(() => 'blob:preview-internal'),
  revokeObjectURL: vi.fn(),
}))

const evidence = {
  id: 'evidence-1',
  evaluationId: 'evaluation-1',
  evaluationNumber: 'EV-001',
  establishmentName: 'Planta de prueba',
  uploadedBy: 'user-1',
  uploadedByName: 'Técnico de prueba',
  originalName: 'evidencia.jpg',
  fileSize: 1024,
  mimeType: 'image/jpeg',
  evidenceType: 'FOTOGRAFIA',
  synchronizationStatus: 'SINCRONIZADA',
  uploadedAt: '2026-09-15T12:00:00Z',
}

vi.mock('../../contexts/useAuth', () => ({
  useAuth: () => ({ roles: ['COORDINADOR'] }),
}))

vi.mock('../../lib/api', () => ({
  getEvidences: vi.fn(async () => ({ items: [evidence], page: 1, pageSize: 20, total: 1 })),
  getEvaluations: vi.fn(async () => ({ items: [], page: 1, pageSize: 100, total: 0 })),
  downloadEvidence: mocks.downloadEvidence,
}))

vi.mock('../../lib/alerts', () => ({
  alerts: { success: vi.fn(), error: vi.fn() },
}))

vi.mock('../../offline/syncQueue', () => ({
  flushSyncQueue: vi.fn(),
  queueEvidence: vi.fn(),
}))

vi.mock('../../hooks/useParameterOptions', () => ({
  useParameterOptions: () => ({ options: [], loading: false }),
}))

describe('EvidenceManagement', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mocks.downloadEvidence.mockResolvedValue(new Blob(['image'], { type: 'image/jpeg' }))
    vi.stubGlobal('URL', {
      ...URL,
      createObjectURL: mocks.createObjectURL,
      revokeObjectURL: mocks.revokeObjectURL,
    })
  })

  it('muestra la imagen protegida en una vista previa sin exponer la URL técnica', async () => {
    render(<EvidenceManagement />)

    fireEvent.click(await screen.findByRole('button', { name: 'Ver' }))

    const image = await screen.findByRole('img', { name: 'Evidencia evidencia.jpg' })
    expect(image).toHaveAttribute('src', 'blob:preview-internal')
    expect(screen.getByRole('heading', { name: 'evidencia.jpg' })).toBeInTheDocument()
    expect(screen.queryByText('blob:preview-internal')).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Cerrar vista previa' }))
    await waitFor(() => expect(mocks.revokeObjectURL).toHaveBeenCalledWith('blob:preview-internal'))
  })
})
