import { useEffect, useState, type FormEvent, type ReactNode } from 'react'
import { useSoundEnabled } from 'react-sounds'
import { TableSkeleton } from '../../components/feedback/Skeletons'
import { useAuth } from '../../contexts/useAuth'
import {
  beginMfaEnrollment,
  changeProfilePassword,
  clearSession,
  completeMfaEnrollment,
  disableMfa,
  getProfile,
  updateProfile,
  updateSessionIdentity,
  type UserProfile,
} from '../../lib/api'
import { alerts } from '../../lib/alerts'
import { formatProfilePhone } from '../../lib/formatters'
import { getSupabaseClient, isSupabaseConfigured } from '../../lib/supabase'

type MfaMode = 'enable' | 'disable'

export function ProfileManagement() {
  const { roleLabel, roles } = useAuth()
  const [soundsEnabled, setSoundsEnabled] = useSoundEnabled()
  const [profile, setProfile] = useState<UserProfile | null>(null)
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [phone, setPhone] = useState('')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [changingPassword, setChangingPassword] = useState(false)
  const [passwordModalOpen, setPasswordModalOpen] = useState(false)
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [mfaMode, setMfaMode] = useState<MfaMode | null>(null)
  const [mfaPassword, setMfaPassword] = useState('')
  const [mfaCode, setMfaCode] = useState('')
  const [mfaFactorId, setMfaFactorId] = useState('')
  const [mfaQr, setMfaQr] = useState('')
  const [mfaSecret, setMfaSecret] = useState('')
  const [mfaBusy, setMfaBusy] = useState(false)
  const [mfaReady, setMfaReady] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    let active = true
    void getProfile()
      .then((value) => {
        if (!active) return
        applyProfile(value)
        setError('')
      })
      .catch((caught) => {
        if (active)
          setError(caught instanceof Error ? caught.message : 'No se pudo cargar el perfil.')
      })
      .finally(() => active && setLoading(false))
    return () => {
      active = false
    }
  }, [])

  function applyProfile(value: UserProfile) {
    setProfile(value)
    setFullName(value.fullName)
    setEmail(value.email)
    setPhone(formatProfilePhone(value.phone ?? ''))
  }

  async function saveProfile(event: FormEvent) {
    event.preventDefault()
    if (!profile) return
    setSaving(true)
    try {
      const updated = await updateProfile({
        fullName: fullName.trim(),
        email: email.trim(),
        phone: phone.trim() || null,
        rowVersion: profile.rowVersion,
      })
      applyProfile(updated)
      updateSessionIdentity(updated.fullName, updated.email)
      await alerts.success('Perfil actualizado', 'Sus datos personales fueron guardados.')
    } catch (caught) {
      await alerts.error(caught, 'No se pudo actualizar el perfil')
    } finally {
      setSaving(false)
    }
  }

  async function savePassword(event: FormEvent) {
    event.preventDefault()
    if (!profile) return
    setChangingPassword(true)
    try {
      await changeProfilePassword({
        currentPassword,
        newPassword,
        confirmPassword,
        rowVersion: profile.rowVersion,
      })
      await alerts.success(
        'Contraseña actualizada',
        'Por seguridad, vuelva a iniciar sesión con su nueva contraseña.',
      )
      clearSession()
      window.location.assign('/')
    } catch (caught) {
      await alerts.error(caught, 'No se pudo cambiar la contraseña')
    } finally {
      setChangingPassword(false)
    }
  }

  function openMfa(mode: MfaMode) {
    setMfaMode(mode)
    setMfaPassword('')
    setMfaCode('')
    setMfaFactorId('')
    setMfaQr('')
    setMfaSecret('')
    setMfaReady(false)
  }

  async function closeMfa() {
    try {
      const supabase = getSupabaseClient()
      if (mfaQr && mfaFactorId) await supabase.auth.mfa.unenroll({ factorId: mfaFactorId })
      await supabase.auth.signOut({ scope: 'local' })
    } catch {
      // Closing the modal must remain available even if the temporary Supabase session expired.
    }
    setMfaMode(null)
  }

  async function prepareMfa(event: FormEvent) {
    event.preventDefault()
    if (!profile || !mfaMode) return
    setMfaBusy(true)
    try {
      if (!isSupabaseConfigured) throw new Error('Supabase no está configurado en esta aplicación.')
      if (mfaMode === 'enable') await beginMfaEnrollment(mfaPassword)
      const supabase = getSupabaseClient()
      const { error: signInError } = await supabase.auth.signInWithPassword({
        email: profile.email,
        password: mfaPassword,
      })
      if (signInError) throw signInError
      const { data: factors, error: factorsError } = await supabase.auth.mfa.listFactors()
      if (factorsError) throw factorsError
      const existing = factors.totp.find((factor) => factor.status === 'verified')
      if (existing) {
        setMfaFactorId(existing.id)
      } else {
        if (mfaMode === 'disable') throw new Error('No se encontró el factor MFA activo.')
        const { data: enrollment, error: enrollmentError } = await supabase.auth.mfa.enroll({
          factorType: 'totp',
          friendlyName: 'SIGERSA',
        })
        if (enrollmentError) throw enrollmentError
        setMfaFactorId(enrollment.id)
        setMfaQr(enrollment.totp.qr_code)
        setMfaSecret(enrollment.totp.secret)
      }
      setMfaReady(true)
    } catch (caught) {
      await alerts.error(caught, 'No se pudo preparar la autenticación en dos pasos')
    } finally {
      setMfaBusy(false)
    }
  }

  async function confirmMfa(event: FormEvent) {
    event.preventDefault()
    if (!profile || !mfaMode || !mfaFactorId) return
    setMfaBusy(true)
    try {
      const supabase = getSupabaseClient()
      const { error: verifyError } = await supabase.auth.mfa.challengeAndVerify({
        factorId: mfaFactorId,
        code: mfaCode,
      })
      if (verifyError) throw verifyError
      const { data: sessionData } = await supabase.auth.getSession()
      const accessToken = sessionData.session?.access_token
      if (!accessToken) throw new Error('Supabase no emitió el comprobante de seguridad.')

      let updated: UserProfile
      if (mfaMode === 'enable') {
        updated = await completeMfaEnrollment(mfaFactorId, accessToken)
      } else {
        updated = await disableMfa({
          currentPassword: mfaPassword,
          factorId: mfaFactorId,
          supabaseAccessToken: accessToken,
        })
        applyProfile(updated)
        const { error: unenrollError } = await supabase.auth.mfa.unenroll({
          factorId: mfaFactorId,
        })
        if (unenrollError) throw unenrollError
      }
      if (mfaMode === 'enable') applyProfile(updated)
      await supabase.auth.signOut({ scope: 'local' })
      setMfaMode(null)
      await alerts.success(
        updated.mfaEnabled
          ? 'Autenticación en dos pasos activada'
          : 'Autenticación en dos pasos desactivada',
      )
    } catch (caught) {
      void getProfile()
        .then(applyProfile)
        .catch(() => undefined)
      await alerts.error(caught, 'No se pudo actualizar la autenticación en dos pasos')
    } finally {
      setMfaBusy(false)
    }
  }

  return (
    <section aria-labelledby="profile-title">
      <div className="rounded-[1.75rem] bg-[radial-gradient(circle_at_75%_15%,rgba(116,183,93,0.22),transparent_35%),linear-gradient(135deg,#f7fffb,#eaf8f1)] p-5 sm:p-7">
        <p className="text-xs font-bold tracking-[0.14em] text-brand-700 uppercase">Cuenta</p>
        <h1 id="profile-title" className="mt-2 text-3xl font-extrabold text-ink-strong">
          Mi perfil
        </h1>
        <p className="mt-2 text-sm text-ink-muted">
          Actualice sus datos personales y proteja el acceso a su cuenta.
        </p>

        {loading && <TableSkeleton rows={4} columns={2} />}
        {error && (
          <div
            role="alert"
            className="mt-6 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800"
          >
            {error}
          </div>
        )}

        {!loading && profile && (
          <div className="mt-6 grid gap-5 xl:grid-cols-[1.08fr_1fr]">
            <form
              onSubmit={(event) => void saveProfile(event)}
              className="rounded-card bg-white/95 p-6 shadow-card backdrop-blur"
            >
              <SectionTitle
                icon={<PersonIcon />}
                title="Información personal"
                subtitle="Mantenga sus datos actualizados para una mejor experiencia."
              />
              <div className="mt-5 flex items-center justify-between gap-4 rounded-xl bg-slate-50 p-4">
                <div>
                  <p className="font-extrabold text-ink-strong">{profile.fullName}</p>
                  <p className="mt-1 text-xs text-ink-muted">
                    Define sus permisos dentro del sistema.
                  </p>
                </div>
                <span className="rounded-full bg-emerald-100 px-3 py-1 text-xs font-extrabold text-emerald-900">
                  {roleLabel}
                </span>
              </div>
              <div className="mt-5 grid gap-4">
                <Field label="Nombre completo">
                  <input
                    required
                    maxLength={250}
                    value={fullName}
                    onChange={(event) => setFullName(event.target.value)}
                    className={inputClass}
                  />
                </Field>
                <Field label="Correo electrónico">
                  <input
                    required
                    type="email"
                    maxLength={320}
                    value={email}
                    onChange={(event) => setEmail(event.target.value)}
                    className={inputClass}
                  />
                </Field>
                <Field label="Teléfono">
                  <input
                    type="tel"
                    maxLength={14}
                    value={phone}
                    onChange={(event) => setPhone(formatProfilePhone(event.target.value))}
                    placeholder="(809) 555-0123"
                    className={inputClass}
                  />
                </Field>
              </div>
              <div className="mt-6 flex flex-wrap justify-end gap-3 border-t border-slate-100 pt-5">
                <button
                  type="button"
                  onClick={() => {
                    setFullName(profile.fullName)
                    setEmail(profile.email)
                    setPhone(formatProfilePhone(profile.phone ?? ''))
                  }}
                  className="min-h-11 rounded-xl border border-slate-300 px-5 font-bold"
                >
                  Cancelar
                </button>
                <button
                  disabled={saving}
                  className="min-h-11 rounded-xl bg-brand-700 px-5 font-bold text-white disabled:opacity-50"
                >
                  {saving ? 'Guardando…' : 'Guardar cambios'}
                </button>
              </div>
            </form>

            <div className="grid content-start gap-5">
              <article className="rounded-card bg-white/95 p-6 shadow-card">
                <div className="flex items-center justify-between gap-4">
                  <SectionTitle
                    icon={<SpeakerIcon />}
                    title="Sonidos de notificación"
                    subtitle="Reproduzca un sonido acorde al resultado de cada alerta."
                  />
                  <button
                    type="button"
                    role="switch"
                    aria-checked={soundsEnabled}
                    aria-label="Sonidos de notificación"
                    onClick={() => setSoundsEnabled(!soundsEnabled)}
                    className={`relative h-8 w-14 shrink-0 rounded-full transition-colors ${soundsEnabled ? 'bg-brand-700' : 'bg-slate-300'}`}
                  >
                    <span
                      aria-hidden="true"
                      className={`absolute top-1 left-1 h-6 w-6 rounded-full bg-white shadow-sm transition-transform ${soundsEnabled ? 'translate-x-6' : 'translate-x-0'}`}
                    />
                  </button>
                </div>
                <p className="mt-4 text-sm font-semibold text-ink-body" aria-live="polite">
                  {soundsEnabled ? 'Sonidos activados' : 'Sonidos desactivados'}
                </p>
                <p className="mt-1 text-xs text-ink-muted">
                  Esta preferencia se guarda automáticamente en este dispositivo.
                </p>
              </article>

              <article className="rounded-card bg-white/95 p-6 shadow-card">
                <SectionTitle
                  icon={<LockIcon />}
                  title="Cambiar contraseña"
                  subtitle="Actualice su clave desde un formulario protegido."
                />
                <div className="mt-5 flex justify-end">
                  <button
                    type="button"
                    onClick={() => setPasswordModalOpen(true)}
                    className="min-h-11 rounded-xl bg-surface-inverse px-5 font-bold text-white"
                  >
                    Cambiar contraseña
                  </button>
                </div>
              </article>

              <article className="rounded-card bg-white/95 p-6 shadow-card">
                <SectionTitle
                  icon={<ShieldIcon />}
                  title="Seguridad de la cuenta"
                  subtitle="Estado actual de su cuenta en SIGERSA."
                />
                <dl className="mt-5 grid gap-4 text-sm">
                  <StatusRow label="Último acceso" value={formatDate(profile.lastAccessAt)} />
                  <StatusRow
                    label="Estado de la cuenta"
                    value={profile.status === 'ACTIVO' ? 'Protegida' : profile.status}
                    badge
                  />
                  <StatusRow
                    label="Autenticación en 2 pasos"
                    value={profile.mfaEnabled ? 'Activa' : 'Inactiva'}
                    badge={profile.mfaEnabled}
                  />
                </dl>
              </article>

              <article className="rounded-card bg-white/95 p-6 shadow-card">
                <div className="flex flex-wrap items-start justify-between gap-3">
                  <SectionTitle
                    icon={<ShieldIcon />}
                    title="Autenticación en 2 pasos"
                    subtitle="Use una aplicación autenticadora para verificar su acceso."
                  />
                  <span
                    className={`rounded-full px-3 py-1 text-xs font-extrabold ${profile.mfaEnabled ? 'bg-emerald-100 text-emerald-900' : 'bg-slate-100 text-slate-600'}`}
                  >
                    {profile.mfaEnabled ? 'Activa' : 'Inactiva'}
                  </span>
                </div>
                <div className="mt-5 rounded-xl border border-slate-200 p-4">
                  <p className="font-bold text-ink-strong">Aplicación autenticadora</p>
                  <p className="mt-1 text-sm text-ink-muted">
                    Compatible con Google Authenticator, Microsoft Authenticator, 1Password y
                    similares.
                  </p>
                </div>
                <button
                  type="button"
                  onClick={() => openMfa(profile.mfaEnabled ? 'disable' : 'enable')}
                  className={`mt-4 min-h-11 w-full rounded-xl px-5 font-bold ${profile.mfaEnabled ? 'border border-red-200 text-red-700' : 'bg-brand-700 text-white'}`}
                >
                  {profile.mfaEnabled ? 'Desactivar MFA' : 'Activar MFA'}
                </button>
              </article>
            </div>

            <div className="rounded-xl bg-emerald-50 p-4 text-sm text-emerald-900 xl:col-start-2">
              <strong>Acceso seguro a SIGERSA.</strong> Sus permisos activos: {roles.join(', ')}.
            </div>
          </div>
        )}
      </div>

      {passwordModalOpen && profile && (
        <PasswordModal
          profile={profile}
          currentPassword={currentPassword}
          newPassword={newPassword}
          confirmPassword={confirmPassword}
          changing={changingPassword}
          onCurrentPassword={setCurrentPassword}
          onNewPassword={setNewPassword}
          onConfirmPassword={setConfirmPassword}
          onClose={() => setPasswordModalOpen(false)}
          onSubmit={savePassword}
        />
      )}
      {mfaMode && (
        <MfaModal
          mode={mfaMode}
          ready={mfaReady}
          busy={mfaBusy}
          password={mfaPassword}
          code={mfaCode}
          qr={mfaQr}
          secret={mfaSecret}
          onPassword={setMfaPassword}
          onCode={setMfaCode}
          onClose={() => void closeMfa()}
          onPrepare={prepareMfa}
          onConfirm={confirmMfa}
        />
      )}
    </section>
  )
}

function MfaModal({
  mode,
  ready,
  busy,
  password,
  code,
  qr,
  secret,
  onPassword,
  onCode,
  onClose,
  onPrepare,
  onConfirm,
}: {
  mode: MfaMode
  ready: boolean
  busy: boolean
  password: string
  code: string
  qr: string
  secret: string
  onPassword: (value: string) => void
  onCode: (value: string) => void
  onClose: () => void
  onPrepare: (event: FormEvent) => void
  onConfirm: (event: FormEvent) => void
}) {
  return (
    <Modal
      title={
        mode === 'enable'
          ? 'Activar autenticación en 2 pasos'
          : 'Desactivar autenticación en 2 pasos'
      }
      onClose={onClose}
    >
      {!ready ? (
        <form onSubmit={onPrepare} className="grid gap-4 p-6">
          <p className="text-sm text-ink-muted">
            Confirme su contraseña actual para continuar de forma segura.
          </p>
          <Field label="Contraseña actual">
            <input
              required
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(event) => onPassword(event.target.value)}
              className={inputClass}
            />
          </Field>
          <ModalActions busy={busy} onClose={onClose} submitLabel="Continuar" />
        </form>
      ) : (
        <form onSubmit={onConfirm} className="grid gap-4 p-6">
          {mode === 'enable' && qr && (
            <div className="grid justify-items-center gap-3 rounded-xl bg-slate-50 p-4">
              <p className="text-center text-sm font-semibold">
                Escanee este código con su aplicación autenticadora.
              </p>
              <img
                src={qr}
                alt="Código QR para configurar MFA"
                className="h-48 w-48 rounded-lg bg-white p-2"
              />
              {secret && (
                <p className="text-center text-xs break-all text-ink-muted">
                  Clave manual: <strong>{secret}</strong>
                </p>
              )}
            </div>
          )}
          {mode === 'enable' && !qr && (
            <p className="rounded-xl bg-amber-50 p-4 text-sm text-amber-900">
              Ya existe un factor en Supabase. Ingrese su código actual para volver a vincularlo con
              SIGERSA.
            </p>
          )}
          <Field label="Código de 6 dígitos">
            <input
              required
              inputMode="numeric"
              autoComplete="one-time-code"
              pattern="[0-9]{6}"
              maxLength={6}
              value={code}
              onChange={(event) => onCode(event.target.value.replace(/\D/g, '').slice(0, 6))}
              className={`${inputClass} text-center text-xl font-bold tracking-[0.3em]`}
            />
          </Field>
          <ModalActions
            busy={busy}
            onClose={onClose}
            submitLabel={mode === 'enable' ? 'Activar MFA' : 'Desactivar MFA'}
          />
        </form>
      )}
    </Modal>
  )
}

function PasswordModal({
  profile,
  currentPassword,
  newPassword,
  confirmPassword,
  changing,
  onCurrentPassword,
  onNewPassword,
  onConfirmPassword,
  onClose,
  onSubmit,
}: {
  profile: UserProfile
  currentPassword: string
  newPassword: string
  confirmPassword: string
  changing: boolean
  onCurrentPassword: (value: string) => void
  onNewPassword: (value: string) => void
  onConfirmPassword: (value: string) => void
  onClose: () => void
  onSubmit: (event: FormEvent) => void
}) {
  return (
    <Modal title="Cambiar contraseña" onClose={onClose}>
      <form onSubmit={onSubmit} className="grid gap-4 p-6">
        <p className="text-sm text-ink-muted">
          Actualizando la clave de {profile.email}. Use mayúscula, minúscula, número y símbolo.
        </p>
        <Field label="Contraseña actual">
          <input
            required
            type="password"
            autoComplete="current-password"
            value={currentPassword}
            onChange={(event) => onCurrentPassword(event.target.value)}
            className={inputClass}
          />
        </Field>
        <Field label="Nueva contraseña">
          <input
            required
            type="password"
            minLength={8}
            maxLength={128}
            autoComplete="new-password"
            value={newPassword}
            onChange={(event) => onNewPassword(event.target.value)}
            className={inputClass}
          />
        </Field>
        <Field label="Confirmar nueva contraseña">
          <input
            required
            type="password"
            minLength={8}
            maxLength={128}
            autoComplete="new-password"
            value={confirmPassword}
            onChange={(event) => onConfirmPassword(event.target.value)}
            className={inputClass}
          />
        </Field>
        <ModalActions busy={changing} onClose={onClose} submitLabel="Actualizar contraseña" />
      </form>
    </Modal>
  )
}

function Modal({
  title,
  onClose,
  children,
}: {
  title: string
  onClose: () => void
  children: ReactNode
}) {
  return (
    <div className="sigersa-modal-overlay fixed inset-0 z-50 grid place-items-center p-4">
      <section
        role="dialog"
        aria-modal="true"
        aria-label={title}
        className="sigersa-modal-panel w-full max-w-xl overflow-hidden"
      >
        <div className="flex items-center justify-between border-b border-slate-200 px-6 py-5">
          <h2 className="text-xl font-extrabold text-ink-strong">{title}</h2>
          <button
            type="button"
            onClick={onClose}
            aria-label="Cerrar formulario"
            className="rounded-lg px-3 py-2 text-xl text-slate-500 hover:bg-slate-100"
          >
            ×
          </button>
        </div>
        {children}
      </section>
    </div>
  )
}
function ModalActions({
  busy,
  onClose,
  submitLabel,
}: {
  busy: boolean
  onClose: () => void
  submitLabel: string
}) {
  return (
    <div className="flex justify-end gap-3 border-t border-slate-200 pt-5">
      <button
        type="button"
        onClick={onClose}
        className="min-h-11 rounded-xl border border-slate-300 px-5 font-bold"
      >
        Cancelar
      </button>
      <button
        disabled={busy}
        className="min-h-11 rounded-xl bg-brand-700 px-5 font-bold text-white disabled:opacity-50"
      >
        {busy ? 'Procesando…' : submitLabel}
      </button>
    </div>
  )
}
function SectionTitle({
  icon,
  title,
  subtitle,
}: {
  icon: ReactNode
  title: string
  subtitle: string
}) {
  return (
    <div className="flex gap-3">
      <span className="text-brand-800 grid h-12 w-12 shrink-0 place-items-center rounded-xl bg-emerald-100">
        {icon}
      </span>
      <div>
        <h2 className="text-lg font-extrabold text-ink-strong">{title}</h2>
        <p className="mt-1 text-sm text-ink-muted">{subtitle}</p>
      </div>
    </div>
  )
}
function StatusRow({
  label,
  value,
  badge = false,
}: {
  label: string
  value: string
  badge?: boolean
}) {
  return (
    <div className="flex items-center justify-between gap-4">
      <dt className="font-semibold text-ink-muted">{label}</dt>
      <dd
        className={
          badge
            ? 'rounded-full bg-emerald-100 px-3 py-1 text-xs font-bold text-emerald-900'
            : 'text-right font-semibold text-ink-strong'
        }
      >
        {value}
      </dd>
    </div>
  )
}
function formatDate(value: string | null) {
  return value
    ? new Intl.DateTimeFormat('es-DO', { dateStyle: 'long', timeStyle: 'short' }).format(
        new Date(value),
      )
    : 'Sin accesos registrados'
}
const inputClass =
  'mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-100'
function Field({ label, children }: { label: string; children: ReactNode }) {
  return (
    <label className="text-sm font-bold text-ink-body">
      {label}
      {children}
    </label>
  )
}
function PersonIcon() {
  return (
    <svg
      viewBox="0 0 24 24"
      className="h-6 w-6"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      aria-hidden="true"
    >
      <circle cx="12" cy="8" r="4" />
      <path d="M4 21a8 8 0 0 1 16 0" />
    </svg>
  )
}
function LockIcon() {
  return (
    <svg
      viewBox="0 0 24 24"
      className="h-6 w-6"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      aria-hidden="true"
    >
      <rect x="5" y="10" width="14" height="11" rx="2" />
      <path d="M8 10V7a4 4 0 0 1 8 0v3" />
    </svg>
  )
}
function ShieldIcon() {
  return (
    <svg
      viewBox="0 0 24 24"
      className="h-6 w-6"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      aria-hidden="true"
    >
      <path d="M12 3 20 6v5c0 5-3.4 8.6-8 10-4.6-1.4-8-5-8-10V6l8-3Z" />
      <path d="m9 12 2 2 4-5" />
    </svg>
  )
}
function SpeakerIcon() {
  return (
    <svg
      viewBox="0 0 24 24"
      className="h-6 w-6"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      aria-hidden="true"
    >
      <path d="M5 9v6h4l5 4V5L9 9H5Z" />
      <path d="M17 9.5a4 4 0 0 1 0 5M19.5 7a7.5 7.5 0 0 1 0 10" />
    </svg>
  )
}
