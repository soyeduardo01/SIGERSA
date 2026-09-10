import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { AuthProvider } from '../../contexts/AuthContext'
import type { AuthSession } from '../../lib/api'
import { RequireAuthentication, RequireRoles } from './ProtectedRoute'

function sessionWithRoles(roles: string[]): AuthSession {
  return {
    accessToken: 'header.payload.signature',
    accessTokenExpiresAt: '2099-01-01T00:00:00Z',
    refreshToken: 'refresh',
    refreshTokenExpiresAt: '2099-01-02T00:00:00Z',
    roles,
  }
}

describe('Protección de rutas', () => {
  it('redirige al login cuando no existe sesión', () => {
    render(
      <MemoryRouter initialEntries={['/privado']}>
        <AuthProvider initialSession={null}>
          <Routes>
            <Route element={<RequireAuthentication />}>
              <Route path="privado" element={<p>Contenido privado</p>} />
            </Route>
            <Route path="login" element={<p>Inicio de sesión</p>} />
          </Routes>
        </AuthProvider>
      </MemoryRouter>,
    )
    expect(screen.getByText('Inicio de sesión')).toBeVisible()
  })

  it('deniega una ruta cuando el rol no está autorizado', () => {
    render(
      <MemoryRouter initialEntries={['/usuarios']}>
        <AuthProvider initialSession={sessionWithRoles(['TECNICO_EVALUADOR'])}>
          <Routes>
            <Route element={<RequireRoles allowedRoles={['ADMINISTRADOR']} />}>
              <Route path="usuarios" element={<p>Usuarios</p>} />
            </Route>
            <Route path="acceso-denegado" element={<p>Acceso restringido</p>} />
          </Routes>
        </AuthProvider>
      </MemoryRouter>,
    )
    expect(screen.getByText('Acceso restringido')).toBeVisible()
    expect(screen.queryByText('Usuarios')).not.toBeInTheDocument()
  })
})
