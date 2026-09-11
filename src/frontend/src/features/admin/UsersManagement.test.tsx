import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { UsersManagement } from './UsersManagement'

const mocks = vi.hoisted(() => ({
  canManage: true,
  create: vi.fn(),
}))

vi.mock('../../lib/api', () => ({
  getParameters: vi.fn(async (keyWord: string) =>
    keyWord === 'TIPO_IDENTIFICACION'
      ? [
          { parametersId: 1, stringData: 'CEDULA', numericData: 1 },
          { parametersId: 2, stringData: 'PASAPORTE', numericData: 2 },
        ]
      : [
          { parametersId: 5, stringData: 'PENDIENTE_VALIDACION', numericData: 0 },
          { parametersId: 3, stringData: 'ACTIVO', numericData: 1 },
          { parametersId: 4, stringData: 'RECHAZADO', numericData: 2 },
        ],
  ),
  getUserManagementOptions: vi.fn(async () => ({
    canManage: mocks.canManage,
    roles: [{ code: 'ADMINISTRADOR', name: 'Administrador' }],
    companies: [{ id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', name: 'Empresa registrada' }],
  })),
  getManagedUsers: vi.fn(async () => ({
    page: 1,
    pageSize: 10,
    total: 2,
    items: [
      {
        id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        nombreCompleto: 'Ana Pérez',
        correo: 'ana@example.com',
        tipoIdentificacion: 'CEDULA',
        identificacion: '00100000001',
        telefono: null,
        roles: ['ADMINISTRADOR'],
        empresaId: null,
        empresaNombre: null,
        estado: 'ACTIVO',
        activo: true,
        versionFila: 1,
      },
      {
        id: 'cccccccc-cccc-cccc-cccc-cccccccccccc',
        nombreCompleto: 'María Solicitante',
        correo: 'maria@example.com',
        tipoIdentificacion: 'CEDULA',
        identificacion: '00100000002',
        telefono: '8095551212',
        roles: ['USUARIO_DELEGADO'],
        empresaId: null,
        empresaNombre: null,
        estado: 'PENDIENTE_VALIDACION',
        activo: true,
        versionFila: 1,
      },
    ],
  })),
  createManagedUser: mocks.create,
  updateManagedUser: vi.fn(),
  setManagedUserSuspension: vi.fn(),
  downloadUserAuthorizationLetter: vi.fn(),
}))

vi.mock('../../lib/alerts', () => ({
  alerts: {
    success: vi.fn(),
    error: vi.fn(),
    confirm: vi.fn(async () => true),
  },
}))

describe('UsersManagement', () => {
  beforeEach(() => {
    mocks.canManage = true
    mocks.create.mockReset()
  })

  it('muestra la tabla y abre el formulario de creación para un administrador', async () => {
    render(<UsersManagement />)

    expect(await screen.findByText('Ana Pérez')).toBeVisible()
    expect(screen.getByText('ana@example.com')).toBeVisible()
    expect(screen.getAllByText('Pendiente Validación').some((item) => item.tagName === 'SPAN')).toBe(true)
    expect(screen.getByText('Pendiente de asignación')).toBeVisible()
    expect(screen.getByRole('button', { name: 'Ver carta' })).toBeVisible()
    expect(screen.getByRole('button', { name: 'Revisar' })).toBeVisible()
    fireEvent.click(await screen.findByRole('button', { name: '+ Nuevo usuario' }))
    const dialog = screen.getByRole('dialog', { name: 'Nuevo usuario' })
    expect(dialog).toBeVisible()
    expect(within(dialog).getByLabelText('Rol')).toBeVisible()
    expect(within(dialog).getByRole('option', { name: 'Empresa registrada' })).toBeInTheDocument()
    const save = within(dialog).getByRole('button', { name: 'Crear usuario' })
    expect(save).toBeDisabled()
    fireEvent.change(within(dialog).getByLabelText('Contraseña temporal'), {
      target: { value: 'Segura8!' },
    })
    expect(save).toBeEnabled()
  })

  it('mantiene al coordinador en modo de solo lectura', async () => {
    mocks.canManage = false
    render(<UsersManagement />)

    await waitFor(() => expect(screen.getByText('Ana Pérez')).toBeVisible())
    expect(screen.queryByRole('button', { name: '+ Nuevo usuario' })).not.toBeInTheDocument()
    expect(screen.getAllByText('Solo lectura')).toHaveLength(2)
  })
})
