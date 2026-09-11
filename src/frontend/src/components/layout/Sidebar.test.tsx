import { render, screen } from '@testing-library/react'
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
      <AuthProvider initialSession={sessionWithRoles(['COORDINADOR'])}>
        <Sidebar currentModule="resumen" />
      </AuthProvider>,
    )

    expect(screen.getByRole('link', { name: 'Resumen' })).toHaveAttribute('aria-current', 'page')
    expect(screen.getByRole('link', { name: 'Resumen' })).toHaveAttribute(
      'href',
      '/modulo.html?module=resumen',
    )
    expect(screen.getByRole('link', { name: 'Evaluaciones' })).toHaveAttribute(
      'href',
      '/modulo.html?module=evaluaciones',
    )
    expect(screen.getByRole('link', { name: 'Inspecciones' })).toHaveAttribute(
      'href',
      '/modulo.html?module=inspecciones',
    )
  })

  it('muestra gestión de usuarios a administradores globales y de empresa', () => {
    const { unmount } = render(
      <AuthProvider initialSession={sessionWithRoles(['TECNICO_EVALUADOR'])}>
        <Sidebar />
      </AuthProvider>,
    )
    expect(screen.queryByRole('link', { name: 'Gestión de usuarios' })).not.toBeInTheDocument()

    unmount()
    const administratorView = render(
      <AuthProvider initialSession={sessionWithRoles(['ADMINISTRADOR'])}>
        <Sidebar />
      </AuthProvider>,
    )
    expect(screen.getByRole('link', { name: 'Gestión de usuarios' })).toBeVisible()

    administratorView.unmount()
    render(
      <AuthProvider initialSession={sessionWithRoles(['ADMINISTRADOR_EMPRESA'])}>
        <Sidebar />
      </AuthProvider>,
    )
    expect(screen.getByRole('link', { name: 'Gestión de usuarios' })).toBeVisible()
    expect(screen.queryByRole('link', { name: 'Empresas' })).not.toBeInTheDocument()
  })

  it('ofrece perfil y sincronización a todo usuario autenticado', () => {
    render(
      <AuthProvider initialSession={sessionWithRoles(['USUARIO_DELEGADO'])}>
        <Sidebar />
      </AuthProvider>,
    )

    expect(screen.getByRole('link', { name: 'Mi perfil' })).toBeVisible()
    expect(screen.getByRole('link', { name: 'Notificaciones y sincronización' })).toBeVisible()
  })
})
