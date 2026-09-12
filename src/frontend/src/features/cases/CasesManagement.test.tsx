import { fireEvent, render, screen, within } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { CasesManagement } from './CasesManagement'

vi.mock('../../lib/api', () => ({
  getCases: vi.fn(async () => ({ items: [], page: 1, pageSize: 50, total: 0 })),
  getCaseOptions: vi.fn(async () => ({
    sources: [
      {
        id: 'request-1',
        kind: 'SOLICITUD_EMPRESA',
        name: 'Solicitud SOL-001',
        companyId: 'company-1',
        establishmentId: 'establishment-1',
      },
      {
        id: 'establishment-1',
        kind: 'PROGRAMACION',
        name: 'Programación Planta Norte',
        companyId: 'company-1',
        establishmentId: 'establishment-1',
      },
      {
        id: 'alert-1',
        kind: 'ALERTA_LAPCH',
        name: 'Alerta LAPCH A-001',
        companyId: 'company-1',
        establishmentId: 'establishment-1',
      },
      {
        id: 'complaint-1',
        kind: 'DENUNCIA',
        name: 'Denuncia D-001',
        companyId: 'company-1',
        establishmentId: 'establishment-1',
      },
    ],
    responsibleUsers: [],
    canManage: true,
  })),
  getParameters: vi.fn(async () => []),
  createCase: vi.fn(),
  updateCase: vi.fn(),
  closeCase: vi.fn(),
}))

vi.mock('../../lib/alerts', () => ({
  alerts: { success: vi.fn(), error: vi.fn(), confirm: vi.fn(), textInput: vi.fn() },
}))

vi.mock('../../offline/syncQueue', () => ({ queueCase: vi.fn() }))

describe('CasesManagement', () => {
  it('filtra los registros asociados cuando cambia el origen', async () => {
    render(<CasesManagement />)
    fireEvent.click(await screen.findByRole('button', { name: '+ Nuevo caso' }))

    const origin = screen.getByLabelText('Origen del caso')
    const associated = screen.getByLabelText('Registro asociado')
    expect(within(associated).getByRole('option', { name: 'Solicitud SOL-001' })).toBeVisible()
    expect(within(associated).queryByRole('option', { name: 'Denuncia D-001' })).toBeNull()

    fireEvent.change(origin, { target: { value: 'DENUNCIA' } })

    expect(within(associated).getByRole('option', { name: 'Denuncia D-001' })).toBeVisible()
    expect(within(associated).queryByRole('option', { name: 'Solicitud SOL-001' })).toBeNull()
  })
})
