import { useState, type FormEvent } from 'react'
import { PasswordValidatorUI } from '../../components/feedback/PasswordValidatorUI'
import { OtpInput } from '../../components/forms/OtpInput'
import { requestPasswordRecovery, resetPassword, verifyPasswordRecovery } from '../../lib/api'
import { alerts } from '../../lib/alerts'
import { isPasswordValid } from '../../lib/passwordPolicy'

type Stage = 'request' | 'verify' | 'reset' | 'complete'

const contentByStage: Record<Stage, { title: string; description: string }> = {
  request: {
    title: 'Recuperar contraseña',
    description:
      'Ingresa tu correo institucional y te enviaremos un código para restablecer tu acceso.',
  },
  verify: {
    title: 'Verifica tu correo',
    description: 'Ingresa el código de seis dígitos que enviamos a tu correo institucional.',
  },
  reset: {
    title: 'Crea una nueva contraseña',
    description: 'Elige una contraseña segura que no hayas utilizado anteriormente.',
  },
  complete: {
    title: 'Contraseña actualizada',
    description: 'Tu acceso fue restablecido correctamente. Ya puedes iniciar sesión.',
  },
}

export function PasswordRecoveryPage({ onBack }: { onBack: () => void }) {
  const [stage, setStage] = useState<Stage>('request')
  const [email, setEmail] = useState('')
  const [otp, setOtp] = useState('')
  const [resetToken, setResetToken] = useState('')
  const [password, setPassword] = useState('')
  const [confirmation, setConfirmation] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [showConfirmation, setShowConfirmation] = useState(false)
  const [message, setMessage] = useState('')
  const [loading, setLoading] = useState(false)

  async function submit(event: FormEvent) {
    event.preventDefault()
    setLoading(true)
    setMessage('')
    try {
      if (stage === 'request') {
        await requestPasswordRecovery(email)
        setStage('verify')
        setMessage('Si la cuenta es elegible, recibirá un código de seis dígitos.')
      } else if (stage === 'verify') {
        const proof = await verifyPasswordRecovery(email, otp)
        setResetToken(proof.resetToken)
        setStage('reset')
        setMessage('Código validado. Defina una nueva contraseña.')
      } else if (stage === 'reset') {
        if (password !== confirmation) throw new Error('Las contraseñas no coinciden.')
        await resetPassword(resetToken, password)
        setResetToken('')
        setPassword('')
        setConfirmation('')
        setStage('complete')
        setMessage('Contraseña actualizada. Ya puede iniciar sesión.')
        void alerts.success(
          'Contraseña actualizada',
          'Ya puede iniciar sesión con su nueva contraseña.',
        )
      }
    } catch (error) {
      setMessage('')
      void alerts.error(error, 'No se pudo completar la recuperación')
    } finally {
      setLoading(false)
    }
  }

  async function resendCode() {
    setLoading(true)
    setMessage('')
    try {
      await requestPasswordRecovery(email)
      setOtp('')
      setMessage('Te enviamos un código nuevo. Revisa también tu carpeta de spam.')
    } catch (error) {
      void alerts.error(error, 'No se pudo reenviar el código')
    } finally {
      setLoading(false)
    }
  }

  const content = contentByStage[stage]

  return (
    <main className="relative flex min-h-dvh items-start justify-center overflow-x-hidden bg-[#f4fbf8] px-4 py-8 sm:px-6 sm:py-12">
      <div
        className="absolute -top-[24rem] -left-[21rem] h-[44rem] w-[54rem] rotate-[-16deg] rounded-[50%] bg-[#c8eee0]/55 sm:-left-[15rem]"
        aria-hidden="true"
      />
      <div
        className="absolute top-[42%] -left-[27rem] h-[46rem] w-[56rem] rotate-[28deg] rounded-[50%] bg-[#d8f4e8]/65"
        aria-hidden="true"
      />
      <div
        className="absolute -right-[28rem] -bottom-[31rem] h-[55rem] w-[68rem] rotate-[-18deg] rounded-[50%] bg-[#80d8bb]/45 sm:-right-[22rem]"
        aria-hidden="true"
      />
      <div
        className="absolute right-[2%] bottom-[-28rem] h-[43rem] w-[53rem] rotate-[-32deg] rounded-[50%] border-[5rem] border-[#bcebdc]/50"
        aria-hidden="true"
      />

      <section
        className="relative z-10 my-auto w-full max-w-[38rem] rounded-[2rem] border border-white/80 bg-white/95 px-5 py-7 shadow-[0_28px_75px_rgba(18,86,64,0.16)] backdrop-blur-sm sm:px-10 sm:py-10 lg:px-12"
        aria-labelledby="recovery-title"
      >
        <header className="text-center">
          <img
            src="/assets/logo-green.png"
            alt="SIGERSA"
            className="mx-auto h-auto w-[13rem] max-w-[65vw] object-contain sm:w-[15rem]"
          />
          {stage === 'complete' && (
            <span className="mx-auto mt-7 grid h-16 w-16 place-items-center rounded-full bg-[#e8f8ef] text-[#16835f]">
              <CheckIcon />
            </span>
          )}
          <h1
            id="recovery-title"
            className="mt-7 text-2xl font-extrabold tracking-tight text-[#101a28] sm:text-3xl"
          >
            {content.title}
          </h1>
          <p className="mx-auto mt-2 max-w-lg text-sm leading-relaxed text-slate-600 sm:text-base">
            {content.description}
          </p>
          {stage === 'verify' && (
            <p className="mt-2 truncate text-sm font-semibold text-[#125640]" title={email}>
              {email}
            </p>
          )}
        </header>

        {stage !== 'complete' && (
          <form className="mt-7 space-y-5" onSubmit={submit}>
            {stage === 'request' && (
              <label className="block text-sm font-semibold text-[#153f34]">
                Correo electrónico
                <span className="relative mt-2 block">
                  <MailIcon />
                  <input
                    required
                    type="email"
                    autoComplete="email"
                    inputMode="email"
                    placeholder="usuario@digemaps.gob.do"
                    value={email}
                    onChange={(event) => setEmail(event.target.value)}
                    className="min-h-14 w-full rounded-xl border border-slate-300 bg-[#f8fafc] pr-4 pl-13 text-base font-normal text-slate-800 shadow-sm transition placeholder:text-slate-400 hover:border-slate-400 focus:border-[#16835f] focus:ring-4 focus:ring-[#16835f]/12 focus:outline-none"
                  />
                </span>
              </label>
            )}

            {stage === 'verify' && (
              <div className="rounded-2xl border border-slate-200 bg-white p-4 sm:p-5">
                <OtpInput value={otp} onChange={setOtp} disabled={loading} />
              </div>
            )}

            {stage === 'reset' && (
              <>
                <PasswordField
                  label="Nueva contraseña"
                  value={password}
                  visible={showPassword}
                  onChange={setPassword}
                  onToggle={() => setShowPassword((visible) => !visible)}
                />
                <div className="rounded-2xl border border-[#dcebe5] bg-[#f6fbf8] p-4">
                  <PasswordValidatorUI passwordValue={password} />
                </div>
                <PasswordField
                  label="Confirmar contraseña"
                  value={confirmation}
                  visible={showConfirmation}
                  onChange={setConfirmation}
                  onToggle={() => setShowConfirmation((visible) => !visible)}
                />
              </>
            )}

            <button
              type="submit"
              disabled={
                loading ||
                (stage === 'verify' && otp.length !== 6) ||
                (stage === 'reset' && (!isPasswordValid(password) || password !== confirmation))
              }
              className="group flex min-h-14 w-full items-center justify-center gap-4 rounded-xl bg-[linear-gradient(90deg,#086a4c,#125640)] px-5 text-base font-bold text-white shadow-[0_12px_26px_rgba(18,86,64,0.2)] transition hover:-translate-y-0.5 hover:brightness-110 disabled:cursor-wait disabled:opacity-60 disabled:hover:translate-y-0"
            >
              {loading && <LoadingSpinner />}
              <span>
                {loading
                  ? stage === 'request'
                    ? 'Enviando código…'
                    : stage === 'verify'
                      ? 'Validando código…'
                      : 'Actualizando contraseña…'
                  : stage === 'request'
                    ? 'Enviar código'
                    : stage === 'verify'
                      ? 'Validar código'
                      : 'Cambiar contraseña'}
              </span>
              {!loading && <ArrowRightIcon />}
            </button>

            {stage === 'verify' && (
              <button
                type="button"
                disabled={loading}
                onClick={resendCode}
                className="min-h-11 w-full rounded-lg px-3 text-sm font-semibold text-[#087452] transition hover:bg-emerald-50 hover:underline disabled:opacity-60"
              >
                ¿No recibiste el código? Reenviar
              </button>
            )}
          </form>
        )}

        {message && (
          <p
            className="mt-5 rounded-xl border border-[#cce8dc] bg-[#eff9f4] px-4 py-3 text-sm leading-relaxed text-[#125640]"
            role="status"
          >
            {message}
          </p>
        )}

        <button
          type="button"
          onClick={onBack}
          className={`${stage === 'complete' ? 'mt-7' : 'mt-4'} min-h-14 w-full rounded-xl border border-slate-300 bg-white px-4 font-bold text-[#126344] transition hover:border-[#16835f] hover:bg-[#f5fbf8]`}
        >
          Volver al inicio de sesión
        </button>

        {stage !== 'complete' && (
          <div className="mt-7 flex items-center gap-4 text-xs leading-relaxed text-slate-600 sm:text-sm">
            <span className="h-px flex-1 bg-slate-200" aria-hidden="true" />
            <span className="grid h-11 w-11 shrink-0 place-items-center rounded-full bg-[#e8f8ef] text-[#16a34a]">
              <ShieldCheckIcon />
            </span>
            <p className="max-w-[17rem]">
              Revisa tu bandeja de entrada y carpeta de spam. El código puede tardar unos minutos.
            </p>
            <span className="hidden h-px flex-1 bg-slate-200 sm:block" aria-hidden="true" />
          </div>
        )}
      </section>
    </main>
  )
}

function PasswordField({
  label,
  value,
  visible,
  onChange,
  onToggle,
}: {
  label: string
  value: string
  visible: boolean
  onChange: (value: string) => void
  onToggle: () => void
}) {
  return (
    <label className="block text-sm font-semibold text-[#153f34]">
      {label}
      <span className="relative mt-2 block">
        <LockIcon />
        <input
          required
          type={visible ? 'text' : 'password'}
          autoComplete="new-password"
          minLength={8}
          value={value}
          onChange={(event) => onChange(event.target.value)}
          className="min-h-14 w-full rounded-xl border border-slate-300 bg-[#f8fafc] pr-13 pl-13 text-base font-normal text-slate-800 shadow-sm transition hover:border-slate-400 focus:border-[#16835f] focus:ring-4 focus:ring-[#16835f]/12 focus:outline-none"
        />
        <button
          type="button"
          onClick={onToggle}
          className="absolute top-1/2 right-2 grid h-11 w-11 -translate-y-1/2 place-items-center rounded-lg text-slate-500 transition hover:bg-emerald-50 hover:text-[#125640]"
          aria-label={visible ? `Ocultar ${label.toLowerCase()}` : `Mostrar ${label.toLowerCase()}`}
          aria-pressed={visible}
        >
          {visible ? <EyeIcon /> : <EyeOffIcon />}
        </button>
      </span>
    </label>
  )
}

function MailIcon() {
  return (
    <svg
      className="pointer-events-none absolute top-1/2 left-4 h-6 w-6 -translate-y-1/2 text-[#125640]"
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

function LockIcon() {
  return (
    <svg
      className="pointer-events-none absolute top-1/2 left-4 h-6 w-6 -translate-y-1/2 text-[#125640]"
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

function ArrowRightIcon() {
  return (
    <svg
      className="h-6 w-6 transition-transform group-hover:translate-x-1"
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

function LoadingSpinner() {
  return (
    <span
      className="h-5 w-5 animate-spin rounded-full border-2 border-white/35 border-t-white"
      role="status"
      aria-label="Procesando solicitud"
    />
  )
}

function ShieldCheckIcon() {
  return (
    <svg
      className="h-6 w-6"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      aria-hidden="true"
    >
      <path d="M12 3 5 6v5c0 4.8 2.9 8.5 7 10 4.1-1.5 7-5.2 7-10V6l-7-3Z" />
      <path d="m9 12 2 2 4-4" />
    </svg>
  )
}

function CheckIcon() {
  return (
    <svg
      className="h-9 w-9"
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      aria-hidden="true"
    >
      <path d="m5 12 4 4L19 6" />
    </svg>
  )
}
