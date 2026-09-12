import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { ProfileManagement } from './ProfileManagement'

vi.mock('../../contexts/useAuth', () => ({
  useAuth: () => ({ roleLabel: 'Administrador', roles: ['ADMINISTRADOR'] }),
}))

vi.mock('../../lib/api', () => ({
  getProfile: vi.fn(async () => ({
    id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
    fullName: 'Ana Administradora',
    email: 'ana@example.com',
    phone: null,
    status: 'ACTIVO',
    lastAccessAt: null,
    mfaEnabled: false,
    rowVersion: 1,
  })),
  updateProfile: vi.fn(),
  changeProfilePassword: vi.fn(),
  beginMfaEnrollment: vi.fn(),
  completeMfaEnrollment: vi.fn(),
  disableMfa: vi.fn(),
  clearSession: vi.fn(),
  updateSessionIdentity: vi.fn(),
}))

vi.mock('../../lib/alerts', () => ({
  alerts: { success: vi.fn(), error: vi.fn() },
}))

describe('ProfileManagement', () => {
  it('mantiene ocultos los campos de contraseña hasta pulsar el botón', async () => {
    render(<ProfileManagement />)

    expect(await screen.findByDisplayValue('Ana Administradora')).toBeVisible()
    expect(screen.queryByLabelText('Contraseña actual')).not.toBeInTheDocument()

    fireEvent.click(screen.getByRole('button', { name: 'Cambiar contraseña' }))

    const dialog = screen.getByRole('dialog', { name: 'Cambiar contraseña' })
    expect(dialog).toBeVisible()
    expect(screen.getByLabelText('Contraseña actual')).toBeVisible()
    expect(screen.getByLabelText('Nueva contraseña')).toBeVisible()
    expect(screen.getByLabelText('Confirmar nueva contraseña')).toBeVisible()
  })
})
