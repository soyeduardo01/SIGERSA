import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  apiFetch,
  clearSession,
  getSession,
  login,
  requestPasswordRecovery,
  type AuthSession,
} from './api'

const session: AuthSession = {
  accessToken: 'expired-access-token',
  accessTokenExpiresAt: '2026-01-01T00:00:00Z',
  refreshToken: 'valid-refresh-token',
  refreshTokenExpiresAt: '2027-01-01T00:00:00Z',
  roles: ['ADMINISTRADOR'],
}

describe('apiFetch', () => {
  beforeEach(() => {
    localStorage.clear()
    sessionStorage.clear()
    localStorage.setItem('sigersa.auth.session', JSON.stringify(session))
    vi.restoreAllMocks()
  })

  it('comparte una sola rotación cuando varias solicitudes reciben 401 al mismo tiempo', async () => {
    let refreshCalls = 0
    let protectedCalls = 0
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: string | URL | Request) => {
        const url = String(input)
        if (url.endsWith('/api/v1/auth/refresh')) {
          refreshCalls += 1
          return new Response(
            JSON.stringify({
              ...session,
              accessToken: 'renewed-access-token',
              refreshToken: 'renewed-refresh-token',
            }),
            { status: 200, headers: { 'Content-Type': 'application/json' } },
          )
        }

        protectedCalls += 1
        return new Response(null, { status: protectedCalls <= 2 ? 401 : 200 })
      }),
    )

    const responses = await Promise.all([apiFetch('/api/v1/users'), apiFetch('/api/v1/all-items')])

    expect(responses.every((response) => response.ok)).toBe(true)
    expect(refreshCalls).toBe(1)
    expect(getSession()?.accessToken).toBe('renewed-access-token')
  })

  it('limpia la sesión cuando el refresh token ya no es válido', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response(null, { status: 401 })),
    )

    await apiFetch('/api/v1/users')

    expect(getSession()).toBeNull()
  })

  it('no reutiliza el refresh token si otra solicitud ya renovó la sesión', async () => {
    let refreshCalls = 0
    let firstCalls = 0
    let secondCalls = 0
    let releaseSecond: ((response: Response) => void) | undefined
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: string | URL | Request) => {
        const url = String(input)
        if (url.endsWith('/api/v1/auth/refresh')) {
          refreshCalls += 1
          return new Response(
            JSON.stringify({
              ...session,
              accessToken: 'renewed-access-token',
              refreshToken: 'renewed-refresh-token',
            }),
            { status: 200, headers: { 'Content-Type': 'application/json' } },
          )
        }
        if (url.endsWith('/second')) {
          secondCalls += 1
          if (secondCalls === 1) {
            return new Promise<Response>((resolve) => {
              releaseSecond = resolve
            })
          }
          return new Response(null, { status: 200 })
        }
        firstCalls += 1
        return new Response(null, { status: firstCalls === 1 ? 401 : 200 })
      }),
    )

    const secondRequest = apiFetch('/second')
    const firstResponse = await apiFetch('/first')
    releaseSecond?.(new Response(null, { status: 401 }))
    const secondResponse = await secondRequest

    expect(firstResponse.ok).toBe(true)
    expect(secondResponse.ok).toBe(true)
    expect(refreshCalls).toBe(1)
  })

  it('conserva la sesión solo durante la pestaña cuando no se solicita recordarla', async () => {
    clearSession()
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          new Response(JSON.stringify(session), {
            status: 200,
            headers: { 'Content-Type': 'application/json' },
          }),
      ),
    )

    await login('usuario@example.com', 'Valid-Password-2026!', false)

    expect(sessionStorage.getItem('sigersa.auth.session')).not.toBeNull()
    expect(localStorage.getItem('sigersa.auth.session')).toBeNull()
  })

  it('conserva la sesión entre aperturas cuando se solicita recordarla', async () => {
    clearSession()
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          new Response(JSON.stringify(session), {
            status: 200,
            headers: { 'Content-Type': 'application/json' },
          }),
      ),
    )

    await login('usuario@example.com', 'Valid-Password-2026!', true)

    expect(localStorage.getItem('sigersa.auth.session')).not.toBeNull()
    expect(sessionStorage.getItem('sigersa.auth.session')).toBeNull()
  })

  it('explica el límite temporal aunque la respuesta 429 no incluya contenido', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(null, { status: 429 })))

    await expect(requestPasswordRecovery('usuario@example.com')).rejects.toMatchObject({
      message: 'Se alcanzó el límite temporal de intentos. Espere unos minutos antes de continuar.',
      status: 429,
    })
  })
})
