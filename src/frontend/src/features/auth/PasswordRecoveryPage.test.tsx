import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { PasswordRecoveryPage } from './PasswordRecoveryPage'
import { requestPasswordRecovery, resetPassword, verifyPasswordRecovery } from '../../lib/api'

vi.mock('../../lib/api', () => ({
  requestPasswordRecovery: vi.fn(),
  verifyPasswordRecovery: vi.fn(),
  resetPassword: vi.fn(),
}))
vi.mock('../../lib/alerts', () => ({
  alerts: {
    success: vi.fn().mockResolvedValue(undefined),
    error: vi.fn().mockResolvedValue(undefined),
  },
}))

describe('PasswordRecoveryPage', () => {
  beforeEach(() => {
    vi.mocked(requestPasswordRecovery).mockResolvedValue(undefined)
    vi.mocked(verifyPasswordRecovery).mockResolvedValue({
      resetToken: 'single-use-proof',
      expiresAt: '2026-09-11T19:00:00Z',
    })
    vi.mocked(resetPassword).mockResolvedValue(undefined)
  })

  it('completa solicitud, OTP y cambio de contraseña', async () => {
    render(<PasswordRecoveryPage onBack={vi.fn()} />)

    fireEvent.change(screen.getByLabelText('Correo electrónico'), {
      target: { value: 'usuario@example.com' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Enviar código' }))
    await waitFor(() => expect(requestPasswordRecovery).toHaveBeenCalledWith('usuario@example.com'))

    const firstDigit = await screen.findByLabelText('Dígito 1 de 6')
    fireEvent.paste(firstDigit.parentElement!, {
      clipboardData: { getData: () => '123456' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Validar código' }))
    await waitFor(() =>
      expect(verifyPasswordRecovery).toHaveBeenCalledWith('usuario@example.com', '123456'),
    )

    fireEvent.change(await screen.findByLabelText('Nueva contraseña'), {
      target: { value: 'Nueva-Password-2026!' },
    })
    fireEvent.change(screen.getByLabelText('Confirmar contraseña'), {
      target: { value: 'Nueva-Password-2026!' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Cambiar contraseña' }))

    await waitFor(() =>
      expect(resetPassword).toHaveBeenCalledWith('single-use-proof', 'Nueva-Password-2026!'),
    )
    expect(
      await screen.findByText('Contraseña actualizada. Ya puede iniciar sesión.'),
    ).toBeVisible()
  })

  it('muestra un cargador mientras valida el código OTP', async () => {
    let completeVerification!: (value: { resetToken: string; expiresAt: string }) => void
    vi.mocked(verifyPasswordRecovery).mockImplementation(
      () =>
        new Promise((resolve) => {
          completeVerification = resolve
        }),
    )
    render(<PasswordRecoveryPage onBack={vi.fn()} />)

    fireEvent.change(screen.getByLabelText('Correo electrónico'), {
      target: { value: 'usuario@example.com' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Enviar código' }))
    const firstDigit = await screen.findByLabelText('Dígito 1 de 6')
    fireEvent.paste(firstDigit.parentElement!, {
      clipboardData: { getData: () => '123456' },
    })
    fireEvent.click(screen.getByRole('button', { name: 'Validar código' }))

    expect(screen.getByRole('button', { name: /Validando código/ })).toBeDisabled()
    expect(screen.getByRole('status', { name: 'Procesando solicitud' })).toBeVisible()

    completeVerification({
      resetToken: 'single-use-proof',
      expiresAt: '2026-09-11T19:00:00Z',
    })
    expect(await screen.findByLabelText('Nueva contraseña')).toBeVisible()
  })
})
