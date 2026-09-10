import { useState, type FormEvent } from 'react'
import { alerts } from '../../lib/alerts'
import { login, type AuthSession } from '../../lib/api'

export function LoginPage({
  onAuthenticated,
  onRecover,
}: {
  onAuthenticated: (session: AuthSession) => void
  onRecover: () => void
}) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [loading, setLoading] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setLoading(true)
    try {
      onAuthenticated(await login(email, password))
    } catch (reason) {
      await alerts.error(reason, 'No se pudo iniciar sesión')
    } finally {
      setLoading(false)
    }
  }

  return (
    <main className="bg-surface-page grid min-h-screen place-items-center px-4 py-10">
      <section
        className="w-full max-w-md rounded-card bg-white p-7 shadow-card"
        aria-labelledby="login-title"
      >
        <img src="/logo.svg" alt="SIGERSA" className="mx-auto h-16 w-auto" />
        <h1 id="login-title" className="mt-5 text-center text-2xl font-extrabold text-ink-strong">
          Iniciar sesión
        </h1>
        <p className="mt-2 text-center text-sm text-ink-muted">
          Evaluación basada en riesgo EBR/BPM
        </p>
        <form className="mt-7 space-y-4" onSubmit={submit}>
          <label className="block text-sm font-bold text-ink-body">
            Correo electrónico
            <input
              required
              type="email"
              autoComplete="username"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 text-base font-normal"
            />
          </label>
          <label className="block text-sm font-bold text-ink-body">
            Contraseña
            <input
              required
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 text-base font-normal"
            />
          </label>
          <button
            type="submit"
            disabled={loading}
            className="min-h-11 w-full rounded-xl bg-brand-700 px-4 font-bold text-white disabled:opacity-60"
          >
            {loading ? 'Validando…' : 'Entrar'}
          </button>
        </form>
        <button
          type="button"
          onClick={onRecover}
          className="mt-4 min-h-11 w-full rounded-xl px-4 font-bold text-brand-700 hover:bg-brand-100"
        >
          ¿Olvidó su contraseña?
        </button>
      </section>
    </main>
  )
}
