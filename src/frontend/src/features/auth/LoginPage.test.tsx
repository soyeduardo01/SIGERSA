import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { alerts } from '../../lib/alerts'
import { login, verifyTwoFactor, type AuthSession } from '../../lib/api'
import { LoginPage } from './LoginPage'

vi.mock('../../lib/api', () => ({ login: vi.fn(), verifyTwoFactor: vi.fn() }))
vi.mock('../../lib/alerts', () => ({
  alerts: { error: vi.fn().mockResolvedValue(undefined) },
}))

const session: AuthSession = {
  accessToken: 'access',
  accessTokenExpiresAt: '2026-09-12T18:00:00Z',
  refreshToken: 'refresh',
  refreshTokenExpiresAt: '2026-09-26T18:00:00Z',
  roles: ['ADMINISTRADOR'],
}

describe('LoginPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(login).mockResolvedValue(session)
    vi.mocked(verifyTwoFactor).mockResolvedValue(session)
  })

  it('enlaza la Dirección con el sitio oficial de DIGEMAPS', () => {
    render(<LoginPage onAuthenticated={vi.fn()} onRecover={vi.fn()} />)

    expect(
      screen.getByRole('link', {
        name: 'Dirección General de Medicamentos, Alimentos y Productos Sanitarios',
      }),
    ).toHaveAttribute('href', 'https://digemaps.gob.do/')
  })

  it('permite abrir el registro público desde el login', () => {
    const onRegister = vi.fn()
    render(<LoginPage onAuthenticated={vi.fn()} onRecover={vi.fn()} onRegister={onRegister} />)

    fireEvent.click(screen.getByRole('button', { name: 'Crear una cuenta' }))
    expect(onRegister).toHaveBeenCalledOnce()
  })

  it('permite mostrar y volver a ocultar la contraseña', () => {
    render(<LoginPage onAuthenticated={vi.fn()} onRecover={vi.fn()} />)
    const password = screen.getByLabelText('Contraseña')

    expect(password).toHaveAttribute('type', 'password')
    fireEvent.click(screen.getByRole('button', { name: 'Mostrar contraseña' }))
    expect(password).toHaveAttribute('type', 'text')
    fireEvent.click(screen.getByRole('button', { name: 'Ocultar contraseña' }))
    expect(password).toHaveAttribute('type', 'password')
  })

  it('envía credenciales y preferencia de sesión', async () => {
    const onAuthenticated = vi.fn()
    render(<LoginPage onAuthenticated={onAuthenticated} onRecover={vi.fn()} />)

    fireEvent.change(screen.getByLabelText('Correo institucional'), {
      target: { value: 'admin@sigersa.local' },
    })
    fireEvent.change(screen.getByLabelText('Contraseña'), {
      target: { value: 'AdminTest-2026!' },
    })
    fireEvent.click(screen.getByLabelText('Mantener sesión iniciada'))
    fireEvent.click(screen.getByRole('button', { name: /Entrar/ }))

    await waitFor(() =>
      expect(login).toHaveBeenCalledWith('admin@sigersa.local', 'AdminTest-2026!', true),
    )
    expect(onAuthenticated).toHaveBeenCalledWith(session)
  })

  it('abre recuperación y muestra errores de autenticación', async () => {
    const onRecover = vi.fn()
    vi.mocked(login).mockRejectedValueOnce(new Error('Credenciales inválidas'))
    render(<LoginPage onAuthenticated={vi.fn()} onRecover={onRecover} />)

    fireEvent.click(screen.getByRole('button', { name: '¿Olvidaste tu contraseña?' }))
    expect(onRecover).toHaveBeenCalledOnce()

    fireEvent.change(screen.getByLabelText('Correo institucional'), {
      target: { value: 'admin@sigersa.local' },
    })
    fireEvent.change(screen.getByLabelText('Contraseña'), { target: { value: 'incorrecta' } })
    fireEvent.click(screen.getByRole('button', { name: /Entrar/ }))

    await waitFor(() =>
      expect(alerts.error).toHaveBeenCalledWith(expect.any(Error), 'No se pudo iniciar sesión', {
        showUnauthorized: true,
      }),
    )
  })

  it('solicita y verifica el código cuando el segundo factor está habilitado', async () => {
    const onAuthenticated = vi.fn()
    vi.mocked(login).mockResolvedValueOnce({
      requiresTwoFactor: true,
      expiresAt: '2026-09-12T18:00:00Z',
    })
    render(<LoginPage onAuthenticated={onAuthenticated} onRecover={vi.fn()} />)

    fireEvent.change(screen.getByLabelText('Correo institucional'), {
      target: { value: 'admin@sigersa.local' },
    })
    fireEvent.change(screen.getByLabelText('Contraseña'), {
      target: { value: 'AdminTest-2026!' },
    })
    fireEvent.click(screen.getByRole('button', { name: /Entrar/ }))

    const otp = await screen.findByLabelText('Código de verificación')
    fireEvent.change(otp, { target: { value: '123456' } })
    fireEvent.click(screen.getByRole('button', { name: 'Verificar código' }))

    await waitFor(() =>
      expect(verifyTwoFactor).toHaveBeenCalledWith('admin@sigersa.local', '123456', false),
    )
    expect(onAuthenticated).toHaveBeenCalledWith(session)
  })
})
