import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { getPublicRegistrationCompanies, registerPublicUser } from '../../lib/api'
import { RegistrationPage } from './RegistrationPage'

vi.mock('../../lib/api', () => ({
  registerPublicUser: vi.fn(),
  getPublicRegistrationCompanies: vi.fn(),
}))
vi.mock('../../lib/alerts', () => ({
  alerts: {
    success: vi.fn().mockResolvedValue(undefined),
    error: vi.fn().mockResolvedValue(undefined),
  },
}))

describe('RegistrationPage', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.mocked(registerPublicUser).mockResolvedValue({
      id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
      status: 'PENDIENTE_VALIDACION',
    })
    vi.mocked(getPublicRegistrationCompanies).mockResolvedValue([
      { id: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb', name: 'Empresa registrada' },
    ])
  })

  it('solo ofrece roles empresariales y envía la carta obligatoria', async () => {
    const onBack = vi.fn()
    render(<RegistrationPage onBack={onBack} />)

    expect(screen.getByRole('option', { name: 'Administrador de Empresa' })).toBeVisible()
    expect(screen.getByRole('option', { name: 'Usuario Delegado' })).toBeVisible()
    expect(screen.queryByRole('option', { name: 'Administrador' })).not.toBeInTheDocument()

    fireEvent.change(screen.getByLabelText(/Nombre completo/), { target: { value: 'María Pérez' } })
    fireEvent.change(screen.getByLabelText(/Número/), { target: { value: '00100000001' } })
    fireEvent.change(screen.getByLabelText(/Correo electrónico/), {
      target: { value: 'maria@example.com' },
    })
    fireEvent.change(screen.getByLabelText(/Teléfono/), { target: { value: '8095551212' } })
    fireEvent.change(screen.getByLabelText(/Rol/), { target: { value: 'USUARIO_DELEGADO' } })
    const company = await screen.findByLabelText(/Empresa/)
    expect(screen.getByRole('button', { name: /Crear cuenta/ })).toBeDisabled()
    fireEvent.change(company, { target: { value: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb' } })
    fireEvent.change(screen.getByLabelText(/Contraseña/), { target: { value: 'Segura8!' } })
    fireEvent.change(screen.getByLabelText(/Carta de autorización/), {
      target: { files: [new File(['%PDF'], 'carta.pdf', { type: 'application/pdf' })] },
    })
    fireEvent.click(screen.getByLabelText(/Acepto los términos/))
    const createAccount = screen.getByRole('button', { name: /Crear cuenta/ })
    expect(createAccount).toBeEnabled()
    fireEvent.submit(createAccount.closest('form')!)

    await waitFor(() => expect(registerPublicUser).toHaveBeenCalledOnce())
    expect(vi.mocked(registerPublicUser).mock.calls[0]?.[0]).toMatchObject({
      nombreCompleto: 'María Pérez',
      rol: 'USUARIO_DELEGADO',
      empresaId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
      termsAccepted: true,
    })
    expect(onBack).toHaveBeenCalledOnce()
  })

  it('alterna la visibilidad de la contraseña mediante iconos accesibles', () => {
    render(<RegistrationPage onBack={vi.fn()} />)

    const password = screen.getByLabelText(/Contraseña \*/)
    const toggle = screen.getByRole('button', { name: 'Mostrar contraseña' })
    expect(password).toHaveAttribute('type', 'password')
    expect(toggle).toHaveAttribute('aria-pressed', 'false')

    fireEvent.click(toggle)

    expect(password).toHaveAttribute('type', 'text')
    expect(screen.getByRole('button', { name: 'Ocultar contraseña' })).toHaveAttribute(
      'aria-pressed',
      'true',
    )
  })
})
