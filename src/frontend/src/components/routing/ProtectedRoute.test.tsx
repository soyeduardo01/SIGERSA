import { render, screen } from '@testing-library/react'
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
      <AuthProvider initialSession={null}>
        <RequireAuthentication fallback={<p>Inicio de sesión</p>}>
          <p>Contenido privado</p>
        </RequireAuthentication>
      </AuthProvider>,
    )
    expect(screen.getByText('Inicio de sesión')).toBeVisible()
  })

  it('deniega una ruta cuando el rol no está autorizado', () => {
    render(
      <AuthProvider initialSession={sessionWithRoles(['TECNICO_EVALUADOR'])}>
        <RequireRoles allowedRoles={['ADMINISTRADOR']} fallback={<p>Acceso restringido</p>}>
          <p>Usuarios</p>
        </RequireRoles>
      </AuthProvider>,
    )
    expect(screen.getByText('Acceso restringido')).toBeVisible()
    expect(screen.queryByText('Usuarios')).not.toBeInTheDocument()
  })
})
