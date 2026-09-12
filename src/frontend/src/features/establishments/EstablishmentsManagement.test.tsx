import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { EstablishmentsManagement } from './EstablishmentsManagement'

vi.mock('../../lib/api', () => ({
  getEstablishments: vi.fn(async () => ({ items: [], page: 1, pageSize: 10, total: 0 })),
  getEstablishmentOptions: vi.fn(async () => ({
    companies: [{ id: 'company-1', code: '101010101', name: 'Empresa de prueba' }],
    provinces: [{ id: 'p1', code: '01', name: 'Distrito Nacional' }],
    municipalities: [],
    dpsDas: [],
    commercializations: [
      { id: 'market-local', code: 'LOCAL', name: 'Local' },
      { id: 'market-all', code: 'TODOS', name: 'Todos los mercados' },
    ],
    markets: [
      { id: 'audience-1', code: 'INFANTIL', name: 'Infantil' },
      { id: 'audience-2', code: 'NINOS_MENORES', name: 'Niños menores' },
      { id: 'audience-3', code: 'ADULTOS', name: 'Adultos' },
      { id: 'audience-4', code: 'MUJERES_EMBARAZADAS', name: 'Mujeres embarazadas' },
      { id: 'audience-5', code: 'ADULTOS_MAYORES', name: 'Adultos mayores' },
      { id: 'audience-6', code: 'TODOS_SEGMENTOS', name: 'Todos los segmentos' },
    ],
    categories: [],
    subcategories: [],
    statuses: [{ parametersId: 1, stringData: 'ACTIVO', numericData: 1 }],
    haccpLevels: [
      { parametersId: 2, numericData: 25, stringData: 'En el 25% de las líneas de producción' },
      { parametersId: 3, numericData: 75, stringData: 'En el 75% de las líneas de producción' },
      { parametersId: 4, numericData: 100, stringData: 'En todas las líneas de producción' },
    ],
    samplingApplications: [
      { parametersId: 5, cCode: 'MP', stringData: 'Solo para las materias primas' },
      { parametersId: 6, cCode: 'AP_PT', stringData: 'Solo para las áreas de proceso y productos terminados' },
      { parametersId: 7, cCode: 'MP_AP_PT', stringData: 'Para las materias primas, las áreas de proceso y productos terminados' },
    ],
    inabieDistributions: [
      { parametersId: 8, cCode: 'NACIONAL', stringData: 'A nivel nacional' },
      { parametersId: 9, cCode: 'REGIONAL', stringData: 'A nivel regional' },
      { parametersId: 10, cCode: 'LOCAL', stringData: 'A nivel local' },
    ],
  })),
  getEstablishment: vi.fn(),
  createEstablishment: vi.fn(),
  updateEstablishment: vi.fn(),
}))

vi.mock('../../lib/alerts', () => ({
  alerts: { success: vi.fn(), error: vi.fn() },
}))

describe('EstablishmentsManagement', () => {
  it('habilita el registro y abre el modal cuando cargan los catálogos', async () => {
    render(<EstablishmentsManagement />)

    const button = await screen.findByRole('button', { name: '+ Nuevo establecimiento' })
    await waitFor(() => expect(button).toBeEnabled())
    fireEvent.click(button)

    expect(screen.getByRole('dialog', { name: 'Registrar establecimiento' })).toBeVisible()
    expect(screen.queryByLabelText('Código')).not.toBeInTheDocument()
    expect(screen.getByText(/Los campos marcados con/)).toBeInTheDocument()

    const phone = screen.getByLabelText('Teléfono')
    fireEvent.change(phone, { target: { value: '(809) 555-1234 adicional' } })
    expect(phone).toHaveValue('809-555-1234')

    fireEvent.click(screen.getByRole('tab', { name: 'Producción y mercado' }))
    const commercialization = screen.getByLabelText('Comercialización')
    const targetMarket = screen.getByLabelText('Mercado objetivo')
    expect(commercialization).toHaveTextContent('Todos los mercados')
    expect(targetMarket).toHaveTextContent('Infantil')
    expect(targetMarket).toHaveTextContent('Niños menores')
    expect(targetMarket).toHaveTextContent('Adultos')
    expect(targetMarket).toHaveTextContent('Mujeres embarazadas')
    expect(targetMarket).toHaveTextContent('Adultos mayores')
    expect(targetMarket).toHaveTextContent('Todos los segmentos')

    fireEvent.click(screen.getByRole('tab', { name: 'Controles y estado' }))
    const switches = screen.getAllByRole('switch')
    switches.forEach((control) => fireEvent.click(control))
    expect(screen.getByText('En el 25% de las líneas de producción')).toBeInTheDocument()
    expect(screen.getByText('En todas las líneas de producción')).toBeInTheDocument()
    expect(screen.getByText('Solo para las materias primas')).toBeInTheDocument()
    expect(screen.getByText('Para las materias primas, las áreas de proceso y productos terminados')).toBeInTheDocument()
    expect(screen.getByText('A nivel nacional')).toBeInTheDocument()
    expect(screen.getByText('A nivel regional')).toBeInTheDocument()
    expect(screen.getByText('A nivel local')).toBeInTheDocument()

    fireEvent.click(screen.getByRole('tab', { name: 'Contactos' }))
    const identifications = screen.getAllByLabelText('Cédula de identidad')
    const contactPhones = screen.getAllByLabelText('Teléfono celular')
    fireEvent.change(identifications[0], { target: { value: '0010000000199' } })
    fireEvent.change(contactPhones[0], { target: { value: '809555123455' } })
    expect(identifications[0]).toHaveValue('001-0000000-1')
    expect(contactPhones[0]).toHaveValue('809-555-1234')
  })
})
