import { useState, type FormEvent } from 'react'
import { PasswordValidatorUI } from '../../components/feedback/PasswordValidatorUI'
import { OtpInput } from '../../components/forms/OtpInput'
import { requestPasswordRecovery, resetPassword, verifyPasswordRecovery } from '../../lib/api'
import { alerts } from '../../lib/alerts'
import { isPasswordValid } from '../../lib/passwordPolicy'

type Stage = 'request' | 'verify' | 'reset' | 'complete'

export function PasswordRecoveryPage({ onBack }: { onBack: () => void }) {
  const [stage, setStage] = useState<Stage>('request')
  const [email, setEmail] = useState('')
  const [otp, setOtp] = useState('')
  const [resetToken, setResetToken] = useState('')
  const [password, setPassword] = useState('')
  const [confirmation, setConfirmation] = useState('')
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
      setMessage(
        error instanceof Error ? error.message : 'No fue posible completar la recuperación.',
      )
      void alerts.error(error, 'No se pudo completar la recuperación')
    } finally {
      setLoading(false)
    }
  }

  return (
    <main className="bg-surface-page grid min-h-screen place-items-center px-4 py-10">
      <section
        className="w-full max-w-md rounded-card bg-white p-7 shadow-card"
        aria-labelledby="recovery-title"
      >
        <img src="/logo.svg" alt="SIGERSA" className="mx-auto h-16 w-auto" />
        <h1
          id="recovery-title"
          className="mt-5 text-center text-2xl font-extrabold text-ink-strong"
        >
          Recuperar contraseña
        </h1>

        {stage !== 'complete' && (
          <form className="mt-7 space-y-4" onSubmit={submit}>
            {stage === 'request' && (
              <label className="block text-sm font-bold text-ink-body">
                Correo electrónico
                <input
                  required
                  type="email"
                  autoComplete="email"
                  value={email}
                  onChange={(event) => setEmail(event.target.value)}
                  className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
                />
              </label>
            )}
            {stage === 'verify' && <OtpInput value={otp} onChange={setOtp} disabled={loading} />}
            {stage === 'reset' && (
              <>
                <label className="block text-sm font-bold text-ink-body">
                  Nueva contraseña
                  <input
                    required
                    type="password"
                    autoComplete="new-password"
                    minLength={8}
                    value={password}
                    onChange={(event) => setPassword(event.target.value)}
                    className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
                  />
                </label>
                <div className="rounded-xl border border-slate-200 bg-slate-50 p-4">
                  <PasswordValidatorUI passwordValue={password} />
                </div>
                <label className="block text-sm font-bold text-ink-body">
                  Confirmar contraseña
                  <input
                    required
                    type="password"
                    autoComplete="new-password"
                    minLength={8}
                    value={confirmation}
                    onChange={(event) => setConfirmation(event.target.value)}
                    className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
                  />
                </label>
              </>
            )}
            <button
              type="submit"
              disabled={
                loading ||
                (stage === 'verify' && otp.length !== 6) ||
                (stage === 'reset' && (!isPasswordValid(password) || password !== confirmation))
              }
              className="min-h-11 w-full rounded-xl bg-brand-700 px-4 font-bold text-white disabled:opacity-60"
            >
              {loading
                ? 'Procesando…'
                : stage === 'request'
                  ? 'Enviar código'
                  : stage === 'verify'
                    ? 'Validar código'
                    : 'Cambiar contraseña'}
            </button>
          </form>
        )}

        {message && (
          <p className="mt-4 rounded-xl bg-brand-100 p-3 text-sm text-brand-900" role="status">
            {message}
          </p>
        )}
        <button
          type="button"
          onClick={onBack}
          className="mt-4 min-h-11 w-full rounded-xl border border-slate-300 px-4 font-bold text-ink-body"
        >
          Volver al inicio de sesión
        </button>
      </section>
    </main>
  )
}
