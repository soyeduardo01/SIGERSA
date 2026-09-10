import { fireEvent, render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { AuthProvider } from '../../contexts/AuthContext'
import type { AuthSession } from '../../lib/api'
import { Sidebar } from './Sidebar'

function sessionWithRoles(roles: string[]): AuthSession {
  return {
    accessToken: 'header.payload.signature',
    accessTokenExpiresAt: '2099-01-01T00:00:00Z',
    refreshToken: 'refresh',
    refreshTokenExpiresAt: '2099-01-02T00:00:00Z',
    roles,
  }
}

describe('Sidebar', () => {
  it('navega entre módulos y marca la ruta activa', () => {
    render(
      <MemoryRouter initialEntries={['/resumen']}>
        <AuthProvider initialSession={sessionWithRoles(['COORDINADOR'])}>
          <Sidebar />
          <Routes>
            <Route path="/resumen" element={<p>Pantalla resumen</p>} />
            <Route path="/evaluaciones" element={<p>Pantalla evaluaciones</p>} />
          </Routes>
        </AuthProvider>
      </MemoryRouter>,
    )

    expect(screen.getByRole('link', { name: 'Resumen' })).toHaveAttribute('aria-current', 'page')
    fireEvent.click(screen.getByRole('link', { name: 'Evaluaciones' }))
    expect(screen.getByText('Pantalla evaluaciones')).toBeVisible()
    expect(screen.getByRole('link', { name: 'Evaluaciones' })).toHaveAttribute(
      'aria-current',
      'page',
    )
  })

  it('muestra gestión de usuarios a administradores globales y de empresa', () => {
    const { unmount } = render(
      <MemoryRouter>
        <AuthProvider initialSession={sessionWithRoles(['TECNICO_EVALUADOR'])}>
          <Sidebar />
        </AuthProvider>
      </MemoryRouter>,
    )
    expect(screen.queryByRole('link', { name: 'Gestión de usuarios' })).not.toBeInTheDocument()

    unmount()
    const administratorView = render(
      <MemoryRouter>
        <AuthProvider initialSession={sessionWithRoles(['ADMINISTRADOR'])}>
          <Sidebar />
        </AuthProvider>
      </MemoryRouter>,
    )
    expect(screen.getByRole('link', { name: 'Gestión de usuarios' })).toBeVisible()

    administratorView.unmount()
    render(
      <MemoryRouter>
        <AuthProvider initialSession={sessionWithRoles(['ADMINISTRADOR_EMPRESA'])}>
          <Sidebar />
        </AuthProvider>
      </MemoryRouter>,
    )
    expect(screen.getByRole('link', { name: 'Gestión de usuarios' })).toBeVisible()
    expect(screen.queryByRole('link', { name: 'Empresas' })).not.toBeInTheDocument()
  })

  it('ofrece perfil y sincronización a todo usuario autenticado', () => {
    render(
      <MemoryRouter>
        <AuthProvider initialSession={sessionWithRoles(['USUARIO_DELEGADO'])}>
          <Sidebar />
        </AuthProvider>
      </MemoryRouter>,
    )

    expect(screen.getByRole('link', { name: 'Mi perfil' })).toBeVisible()
    expect(screen.getByRole('link', { name: 'Notificaciones y sincronización' })).toBeVisible()
  })
})
