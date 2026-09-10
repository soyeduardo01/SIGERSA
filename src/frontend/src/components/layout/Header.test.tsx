import { fireEvent, render, screen } from '@testing-library/react'
import { describe, expect, it, vi } from 'vitest'
import { Header } from './Header'

const defaultProps = {
  isOnline: true,
  isSyncing: false,
  pendingCount: 2,
  identity: { name: 'Ana Pérez', email: 'ana@example.com', initials: 'AP' },
  roleLabel: 'Administrador',
  onOpenMenu: vi.fn(),
  onLogout: vi.fn(),
}

describe('Header', () => {
  it('no muestra un buscador global en el layout', () => {
    render(<Header {...defaultProps} />)
    expect(screen.queryByRole('searchbox')).not.toBeInTheDocument()
  })

  it('abre la bandeja de notificaciones', () => {
    render(<Header {...defaultProps} />)
    fireEvent.click(screen.getByRole('button', { name: 'Notificaciones' }))
    expect(screen.getByLabelText('Bandeja de notificaciones')).toBeVisible()
    expect(screen.getByText(/Hay 2 registros pendientes/)).toBeVisible()
  })

  it('muestra perfil, rol y cierre de sesión', () => {
    const onLogout = vi.fn()
    render(<Header {...defaultProps} onLogout={onLogout} />)
    fireEvent.click(screen.getByRole('button', { name: 'Abrir menú de usuario' }))
    expect(screen.getByLabelText('Menú de usuario')).toBeVisible()
    expect(screen.getAllByText('Administrador')).toHaveLength(2)
    fireEvent.click(screen.getByRole('button', { name: 'Cerrar sesión' }))
    expect(onLogout).toHaveBeenCalledOnce()
  })
})
