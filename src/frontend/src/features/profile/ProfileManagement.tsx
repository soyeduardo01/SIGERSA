import { useEffect, useState, type FormEvent, type ReactNode } from 'react'
import { TableSkeleton } from '../../components/feedback/Skeletons'
import { useAuth } from '../../contexts/useAuth'
import {
  changeProfilePassword,
  clearSession,
  getProfile,
  updateProfile,
  updateSessionIdentity,
  type UserProfile,
} from '../../lib/api'
import { alerts } from '../../lib/alerts'

export function ProfileManagement() {
  const { roleLabel, roles } = useAuth()
  const [profile, setProfile] = useState<UserProfile | null>(null)
  const [fullName, setFullName] = useState('')
  const [email, setEmail] = useState('')
  const [phone, setPhone] = useState('')
  const [loading, setLoading] = useState(true)
  const [saving, setSaving] = useState(false)
  const [changingPassword, setChangingPassword] = useState(false)
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [error, setError] = useState('')

  useEffect(() => {
    let active = true
    void getProfile()
      .then((value) => {
        if (!active) return
        setProfile(value)
        setFullName(value.fullName)
        setEmail(value.email)
        setPhone(value.phone ?? '')
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
      setProfile(updated)
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

  return (
    <section aria-labelledby="profile-title">
      <p className="text-xs font-bold tracking-[0.14em] text-brand-700 uppercase">Cuenta</p>
      <h1 id="profile-title" className="mt-2 text-2xl font-extrabold text-ink-strong">
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
        <div className="mt-6 grid gap-5 xl:grid-cols-2">
          <form
            onSubmit={(event) => void saveProfile(event)}
            className="rounded-card bg-white p-6 shadow-card"
          >
            <h2 className="text-lg font-extrabold text-ink-strong">Información personal</h2>
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
                  maxLength={40}
                  value={phone}
                  onChange={(event) => setPhone(event.target.value)}
                  className={inputClass}
                />
              </Field>
              <div className="rounded-xl bg-slate-50 p-4 text-sm">
                <p className="font-bold text-ink-muted">Rol principal</p>
                <p className="mt-1 text-ink-strong">{roleLabel}</p>
                <div className="mt-3 flex flex-wrap gap-2">
                  {roles.map((role) => (
                    <span
                      key={role}
                      className="text-brand-800 rounded-full bg-brand-50 px-3 py-1 text-xs font-bold"
                    >
                      {role}
                    </span>
                  ))}
                </div>
              </div>
            </div>
            <div className="mt-6 flex justify-end border-t border-slate-100 pt-5">
              <button
                disabled={saving}
                className="min-h-11 rounded-xl bg-brand-700 px-5 font-bold text-white disabled:opacity-50"
              >
                {saving ? 'Guardando…' : 'Guardar cambios'}
              </button>
            </div>
          </form>

          <form
            onSubmit={(event) => void savePassword(event)}
            className="rounded-card bg-white p-6 shadow-card"
          >
            <h2 className="text-lg font-extrabold text-ink-strong">Cambiar contraseña</h2>
            <p className="mt-2 text-sm text-ink-muted">
              Use al menos ocho caracteres con mayúscula, minúscula, número y símbolo.
            </p>
            <div className="mt-5 grid gap-4">
              <Field label="Contraseña actual">
                <input
                  required
                  type="password"
                  autoComplete="current-password"
                  value={currentPassword}
                  onChange={(event) => setCurrentPassword(event.target.value)}
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
                  onChange={(event) => setNewPassword(event.target.value)}
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
                  onChange={(event) => setConfirmPassword(event.target.value)}
                  className={inputClass}
                />
              </Field>
            </div>
            <div className="mt-6 flex justify-end border-t border-slate-100 pt-5">
              <button
                disabled={changingPassword}
                className="min-h-11 rounded-xl bg-surface-inverse px-5 font-bold text-white disabled:opacity-50"
              >
                {changingPassword ? 'Actualizando…' : 'Cambiar contraseña'}
              </button>
            </div>
          </form>
        </div>
      )}
    </section>
  )
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
