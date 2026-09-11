import { fireEvent, render, screen, within } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { CompaniesManagement } from './CompaniesManagement'

vi.mock('../../lib/api', () => ({
  getParameters: vi.fn(async () => [{ parametersId: 1, stringData: 'ACTIVA', numericData: 1 }]),
  getEstablishmentOptions: vi.fn(async () => ({ provinces: [], municipalities: [] })),
  getCompanies: vi.fn(async () => ({
    items: [
      {
        id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        legalName: 'Empresa registrada',
        taxId: '101010101',
        tradeName: null,
        economicActivity: null,
        phone: null,
        email: null,
        status: 'ACTIVA',
        rowVersion: 1,
      },
    ],
    page: 1,
    pageSize: 10,
    total: 1,
  })),
  createCompany: vi.fn(),
  updateCompany: vi.fn(),
}))

vi.mock('../../lib/alerts', () => ({
  alerts: { success: vi.fn(), error: vi.fn() },
}))

describe('CompaniesManagement', () => {
  it('muestra empresas persistidas y abre el formulario de registro', async () => {
    render(<CompaniesManagement />)

    expect(await screen.findByText('Empresa registrada')).toBeVisible()
    fireEvent.click(screen.getByRole('button', { name: '+ Nueva empresa' }))

    const dialog = screen.getByRole('dialog', { name: 'Registrar empresa' })
    expect(dialog).toBeVisible()
    expect(within(dialog).getByLabelText('Estado')).toHaveValue('ACTIVA')
  })
})
