import { useState, type FormEvent, type ReactNode } from 'react'
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
  const [showPassword, setShowPassword] = useState(false)
  const [rememberSession, setRememberSession] = useState(false)
  const [loading, setLoading] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setLoading(true)
    try {
      onAuthenticated(await login(email, password, rememberSession))
    } catch (reason) {
      await alerts.error(reason, 'No se pudo iniciar sesión', { showUnauthorized: true })
    } finally {
      setLoading(false)
    }
  }

  return (
    <main className="relative min-h-dvh overflow-hidden bg-[#edf7f2] p-0 lg:grid lg:place-items-center lg:p-4">
      <div
        className="absolute inset-0 bg-[url('/assets/login-background.png')] bg-cover bg-[72%_center] lg:hidden"
        aria-hidden="true"
      />
      <div
        className="absolute inset-0 bg-[linear-gradient(180deg,rgba(238,250,244,0.96)_0%,rgba(248,252,250,0.91)_48%,rgba(7,88,62,0.85)_100%)] lg:bg-[radial-gradient(circle_at_top_left,rgba(116,183,93,0.18),transparent_38%),#edf7f2]"
        aria-hidden="true"
      />
      <DecorativeLeaf className="absolute -top-24 -left-28 h-72 w-72 rotate-[18deg] text-[#74b75d]/10 lg:h-96 lg:w-96" />
      <DecorativeLeaf className="absolute -right-24 -bottom-32 h-96 w-96 -rotate-[18deg] text-[#125640]/10" />

      <section
        className="relative z-10 mx-auto grid min-h-dvh w-full max-w-[1240px] lg:min-h-[min(680px,calc(100vh-2rem))] lg:grid-cols-[45%_55%] lg:overflow-hidden lg:rounded-[1.5rem] lg:bg-white lg:shadow-[0_24px_64px_rgba(18,86,64,0.16)]"
        aria-labelledby="login-title"
      >
        <aside className="relative hidden overflow-hidden bg-[#064b38] lg:flex lg:flex-col lg:justify-end lg:p-10 xl:p-12">
          <div
            className="absolute inset-0 bg-[url('/assets/login-background.png')] bg-cover bg-center opacity-100"
            aria-hidden="true"
          />
          <div
            className="absolute inset-0 bg-[linear-gradient(135deg,rgba(3,70,51,0.8)_0%,rgba(5,86,62,0.68)_48%,rgba(4,61,47,0.5)_100%)]"
            aria-hidden="true"
          />
          <div
            className="absolute -bottom-44 -left-48 h-[34rem] w-[46rem] rounded-[50%] bg-[#063e31]/65"
            aria-hidden="true"
          />
          <div
            className="absolute top-0 -right-48 h-full w-[34rem] rotate-[-18deg] bg-[#a7d89b]/18"
            aria-hidden="true"
          />

          <div className="relative max-w-[30rem] text-shadow-sm">
            <div className="h-1 w-16 rounded-full bg-[#8bd36d]" aria-hidden="true" />
            <p className="mt-6 max-w-sm text-2xl leading-snug font-light text-white xl:text-[1.75rem]">
              Comprometidos con la salud y la seguridad de todos.
            </p>

            <ul className="mt-8 space-y-4" aria-label="Beneficios de SIGERSA">
              <Benefit icon={<ShieldIcon />} title="Productos seguros" text="para una mejor vida" />
              <Benefit
                icon={<LeafIcon />}
                title="Alimentos de calidad"
                text="para un país más fuerte"
              />
              <Benefit
                icon={<PeopleIcon />}
                title="Un control más eficiente"
                text="al servicio de la ciudadanía"
              />
            </ul>
          </div>

          <a
            href="https://digemaps.gob.do/"
            target="_blank"
            rel="noopener noreferrer"
            className="relative mt-10 cursor-pointer border-t border-white/20 pt-5 text-sm leading-relaxed text-white/75 no-underline transition duration-200 hover:translate-x-1 hover:text-white focus-visible:rounded-md focus-visible:ring-2 focus-visible:ring-white/80 focus-visible:outline-none"
          >
            Dirección General de Medicamentos, Alimentos y Productos Sanitarios
          </a>
        </aside>

        <div className="relative flex min-h-dvh flex-col justify-center px-5 py-8 sm:px-10 lg:min-h-0 lg:bg-white lg:px-12 lg:py-8 xl:px-16">
          <DecorativeLeaf className="absolute -top-20 -right-24 hidden h-80 w-80 rotate-[-25deg] text-[#74b75d]/8 lg:block" />

          <header className="relative mx-auto mb-7 w-full max-w-lg text-center lg:mb-6 lg:text-left">
            <p className="hidden text-xs font-semibold tracking-[0.3em] text-slate-500 uppercase lg:block">
              Bienvenido a
            </p>
            <img
              src="/assets/logo-green.png"
              alt="SIGERSA"
              className="mx-auto h-auto w-[18rem] max-w-[78vw] object-contain lg:mx-0 lg:mt-2 lg:w-[17rem]"
            />
            <p className="mx-auto mt-2 max-w-md text-base leading-relaxed text-slate-600 lg:mx-0 lg:text-sm xl:text-base">
              Sistema Integral de Gestión de Riesgo y Seguridad Alimentaria
            </p>
          </header>

          <div className="relative mx-auto w-full max-w-lg rounded-[1.75rem] border border-white/80 bg-white/95 p-6 shadow-[0_22px_60px_rgba(18,86,64,0.18)] backdrop-blur-sm sm:p-9 lg:rounded-none lg:border-0 lg:bg-transparent lg:p-0 lg:shadow-none lg:backdrop-blur-none">
            <div className="mb-5">
              <h1
                id="login-title"
                className="text-2xl font-extrabold tracking-tight text-[#125640] xl:text-3xl"
              >
                Iniciar sesión
              </h1>
              <p className="mt-1 text-sm text-slate-500 xl:text-base">
                Accede a tu cuenta para continuar.
              </p>
            </div>

            <form className="space-y-4" onSubmit={submit}>
              <label className="block text-sm font-semibold text-[#153f34]">
                Correo institucional
                <span className="relative mt-2 block">
                  <MailIcon className="pointer-events-none absolute top-1/2 left-4 h-6 w-6 -translate-y-1/2 text-[#125640]" />
                  <input
                    required
                    type="email"
                    autoComplete="username"
                    inputMode="email"
                    placeholder="usuario@digemaps.gob.do"
                    value={email}
                    onChange={(event) => setEmail(event.target.value)}
                    className="min-h-12 w-full rounded-xl border border-slate-300 bg-[#f8fafc] pr-4 pl-13 text-base font-normal text-slate-800 shadow-sm transition placeholder:text-slate-400 hover:border-slate-400 focus:border-[#16835f] focus:ring-4 focus:ring-[#16835f]/12 focus:outline-none xl:min-h-13"
                  />
                </span>
              </label>

              <label className="block text-sm font-semibold text-[#153f34]">
                Contraseña
                <span className="relative mt-2 block">
                  <LockIcon className="pointer-events-none absolute top-1/2 left-4 h-6 w-6 -translate-y-1/2 text-[#125640]" />
                  <input
                    required
                    type={showPassword ? 'text' : 'password'}
                    autoComplete="current-password"
                    placeholder="Ingresa tu contraseña"
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
                    className="min-h-12 w-full rounded-xl border border-slate-300 bg-[#f8fafc] pr-13 pl-13 text-base font-normal text-slate-800 shadow-sm transition placeholder:text-slate-400 hover:border-slate-400 focus:border-[#16835f] focus:ring-4 focus:ring-[#16835f]/12 focus:outline-none xl:min-h-13"
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword((visible) => !visible)}
                    className="absolute top-1/2 right-2 grid h-11 w-11 -translate-y-1/2 place-items-center rounded-lg text-slate-500 transition hover:bg-emerald-50 hover:text-[#125640]"
                    aria-label={showPassword ? 'Ocultar contraseña' : 'Mostrar contraseña'}
                    aria-pressed={showPassword}
                  >
                    {showPassword ? <EyeIcon /> : <EyeOffIcon />}
                  </button>
                </span>
              </label>

              <div className="flex flex-col gap-3 text-sm sm:flex-row sm:items-center sm:justify-between">
                <label className="inline-flex min-h-9 cursor-pointer items-center gap-3 text-slate-700">
                  <input
                    type="checkbox"
                    checked={rememberSession}
                    onChange={(event) => setRememberSession(event.target.checked)}
                    className="h-5 w-5 rounded border-slate-400 accent-[#125640]"
                  />
                  Mantener sesión iniciada
                </label>
                <button
                  type="button"
                  onClick={onRecover}
                  className="min-h-9 self-start rounded-lg px-1 font-semibold text-[#087452] transition hover:text-[#125640] hover:underline sm:self-auto"
                >
                  ¿Olvidaste tu contraseña?
                </button>
              </div>

              <button
                type="submit"
                disabled={loading}
                className="group flex min-h-12 w-full items-center justify-center gap-5 rounded-xl bg-[linear-gradient(90deg,#086a4c,#125640)] px-5 text-base font-bold text-white shadow-[0_12px_28px_rgba(18,86,64,0.22)] transition hover:-translate-y-0.5 hover:shadow-[0_16px_34px_rgba(18,86,64,0.3)] hover:brightness-110 disabled:cursor-wait disabled:opacity-65 disabled:hover:translate-y-0 xl:min-h-13"
              >
                <span>{loading ? 'Validando…' : 'Entrar'}</span>
                <ArrowRightIcon className="h-6 w-6 transition-transform group-hover:translate-x-1" />
              </button>
            </form>
          </div>

          <div className="relative mx-auto mt-8 flex w-full max-w-xl items-center justify-center gap-6 text-center text-xs text-[#125640] lg:hidden">
            <CompactBenefit icon={<ShieldIcon />} label="Productos seguros" />
            <CompactBenefit icon={<LeafIcon />} label="Alimentos de calidad" />
            <CompactBenefit icon={<PeopleIcon />} label="Control eficiente" />
          </div>

          <p className="relative mt-8 text-center text-xs font-semibold tracking-[0.2em] text-white/90 uppercase lg:mt-6 lg:text-slate-500">
            Salud · Alimentos · Confianza
          </p>
        </div>
      </section>
    </main>
  )
}

function Benefit({ icon, title, text }: { icon: ReactNode; title: string; text: string }) {
  return (
    <li className="flex items-center gap-4">
      <span className="grid h-12 w-12 shrink-0 place-items-center rounded-full bg-[#16835f]/70 text-white">
        {icon}
      </span>
      <span>
        <strong className="block text-base text-white">{title}</strong>
        <span className="mt-0.5 block text-sm text-white/70">{text}</span>
      </span>
    </li>
  )
}

function CompactBenefit({ icon, label }: { icon: ReactNode; label: string }) {
  return (
    <span className="flex max-w-24 flex-1 flex-col items-center gap-2 text-white">
      <span className="grid h-11 w-11 place-items-center rounded-full bg-white/20 backdrop-blur-sm">
        {icon}
      </span>
      <span className="leading-tight">{label}</span>
    </span>
  )
}

function MailIcon({ className = 'h-6 w-6' }: { className?: string }) {
  return (
    <svg
      className={className}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      aria-hidden="true"
    >
      <rect x="3" y="5" width="18" height="14" rx="2" />
      <path d="m4 7 8 6 8-6" />
    </svg>
  )
}

function LockIcon({ className = 'h-6 w-6' }: { className?: string }) {
  return (
    <svg
      className={className}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      aria-hidden="true"
    >
      <rect x="4" y="10" width="16" height="11" rx="2" />
      <path d="M8 10V7a4 4 0 0 1 8 0v3M12 14v3" />
    </svg>
  )
}

function EyeIcon() {
  return (
    <svg
      className="h-6 w-6"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      aria-hidden="true"
    >
      <path d="M2.5 12s3.5-6 9.5-6 9.5 6 9.5 6-3.5 6-9.5 6-9.5-6-9.5-6Z" />
      <circle cx="12" cy="12" r="2.5" />
    </svg>
  )
}

function EyeOffIcon() {
  return (
    <svg
      className="h-6 w-6"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      aria-hidden="true"
    >
      <path d="m3 3 18 18M10.6 6.2A10 10 0 0 1 12 6c6 0 9.5 6 9.5 6a15 15 0 0 1-2.3 3M6.2 6.2C3.8 8 2.5 12 2.5 12s3.5 6 9.5 6a9.7 9.7 0 0 0 3-.5M9.9 9.9a3 3 0 0 0 4.2 4.2" />
    </svg>
  )
}

function ArrowRightIcon({ className = 'h-6 w-6' }: { className?: string }) {
  return (
    <svg
      className={className}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      aria-hidden="true"
    >
      <path d="M5 12h14M14 7l5 5-5 5" />
    </svg>
  )
}

function ShieldIcon() {
  return (
    <svg
      className="h-7 w-7"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      aria-hidden="true"
    >
      <path d="M12 3 5 6v5c0 4.8 2.9 8.5 7 10 4.1-1.5 7-5.2 7-10V6l-7-3Z" />
      <path d="M12 3v18" />
    </svg>
  )
}

function LeafIcon() {
  return (
    <svg
      className="h-7 w-7"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      aria-hidden="true"
    >
      <path d="M20 4C11 4 5 8.5 5 15c0 2.5 1.6 4 4 4 6.5 0 11-6 11-15Z" />
      <path d="M4 21c3-5 7-8 12-11" />
    </svg>
  )
}

function PeopleIcon() {
  return (
    <svg
      className="h-7 w-7"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      aria-hidden="true"
    >
      <circle cx="12" cy="8" r="3" />
      <circle cx="5" cy="10" r="2" />
      <circle cx="19" cy="10" r="2" />
      <path d="M7 20v-2a5 5 0 0 1 10 0v2M2 20v-1a4 4 0 0 1 5-3.9M22 20v-1a4 4 0 0 0-5-3.9" />
    </svg>
  )
}

function DecorativeLeaf({ className }: { className: string }) {
  return (
    <svg className={className} viewBox="0 0 200 200" fill="currentColor" aria-hidden="true">
      <path d="M176 20C83 23 27 71 29 133c1 28 20 47 49 47 65 0 100-69 98-160Z" />
      <path
        d="M19 190c35-66 77-108 130-139"
        fill="none"
        stroke="currentColor"
        strokeWidth="9"
        strokeLinecap="round"
      />
    </svg>
  )
}
