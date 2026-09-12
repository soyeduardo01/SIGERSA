import { useState, type ChangeEvent, type FormEvent } from 'react'
import { PasswordValidatorUI } from '../../components/feedback/PasswordValidatorUI'
import { alerts } from '../../lib/alerts'
import { registerPublicUser, type PublicRegistrationDraft } from '../../lib/api'
import { formatCedula, formatIdentification, formatPhone } from '../../lib/formatters'
import { isPasswordValid } from '../../lib/passwordPolicy'
import { DecorativeLeaf, LoginVisualPanel } from './LoginPage'

const maximumFileSize = 5 * 1024 * 1024
const acceptedTypes = ['application/pdf', 'image/jpeg', 'image/png']

interface RegistrationFormState {
  nombreCompleto: string
  tipoIdentificacion: PublicRegistrationDraft['tipoIdentificacion']
  identificacion: string
  correo: string
  telefono: string
  rol: '' | PublicRegistrationDraft['rol']
  password: string
  termsAccepted: boolean
}

const initialDraft: RegistrationFormState = {
  nombreCompleto: '',
  tipoIdentificacion: 'CEDULA',
  identificacion: '',
  correo: '',
  telefono: '',
  rol: '',
  password: '',
  termsAccepted: false,
}

export function RegistrationPage({ onBack }: { onBack: () => void }) {
  const [draft, setDraft] = useState(initialDraft)
  const [file, setFile] = useState<File | null>(null)
  const [showPassword, setShowPassword] = useState(false)
  const [saving, setSaving] = useState(false)

  function selectFile(selected: File | null) {
    if (!selected) {
      setFile(null)
      return
    }
    if (!acceptedTypes.includes(selected.type)) {
      void alerts.error(
        new Error('La carta debe estar en formato PDF, JPG o PNG.'),
        'Archivo no válido',
      )
      return
    }
    if (selected.size > maximumFileSize) {
      void alerts.error(
        new Error('La carta de autorización no puede superar 5 MB.'),
        'Archivo muy grande',
      )
      return
    }
    setFile(selected)
  }

  async function submit(event: FormEvent) {
    event.preventDefault()
    if (!file || !draft.rol || !isPasswordValid(draft.password)) return
    setSaving(true)
    try {
      await registerPublicUser({ ...draft, rol: draft.rol, authorizationLetter: file })
      await alerts.success(
        'Solicitud recibida',
        'Tu cuenta quedó pendiente de validación. Un administrador revisará los datos y la carta de autorización.',
      )
      onBack()
    } catch (caught) {
      await alerts.error(caught, 'No se pudo enviar la solicitud')
    } finally {
      setSaving(false)
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

      <section className="relative z-10 mx-auto grid min-h-dvh w-full max-w-[1240px] lg:min-h-[min(680px,calc(100vh-2rem))] lg:grid-cols-[45%_55%] lg:overflow-hidden lg:rounded-[1.5rem] lg:bg-white lg:shadow-[0_24px_64px_rgba(18,86,64,0.16)]">
        <LoginVisualPanel />

        <div className="flex min-h-dvh flex-col justify-center px-5 py-4 sm:px-9 lg:min-h-0 lg:px-8 lg:py-3 xl:px-10">
          <div className="mx-auto w-full max-w-2xl">
            <button
              type="button"
              onClick={onBack}
              className="mb-1 ml-auto block text-right text-sm font-bold text-[#087452] hover:underline"
            >
              ← Volver al inicio de sesión
            </button>
            <p className="text-[0.68rem] font-bold tracking-[0.28em] text-slate-500 uppercase">
              Crea tu cuenta
            </p>
            <h1 className="mt-1 text-3xl font-extrabold tracking-tight text-[#125640]">
              Registro de usuario
            </h1>
            <div className="mt-1.5 h-1 w-16 rounded-full bg-[#8bd36d]" aria-hidden="true" />
            <p className="mt-2 text-sm text-slate-500">
              Completa la información para solicitar acceso al sistema.
            </p>

            <form onSubmit={submit} className="mt-3 grid gap-x-5 gap-y-2 pb-3 sm:grid-cols-2">
              <h2 className="text-base font-extrabold text-[#125640] sm:col-span-2">
                Datos del usuario
              </h2>
              <Field label="Nombre completo">
                <input
                  required
                  maxLength={250}
                  autoComplete="name"
                  placeholder="Ej. Juan Pérez García"
                  value={draft.nombreCompleto}
                  onChange={(event) => setDraft({ ...draft, nombreCompleto: event.target.value })}
                  className={inputClass}
                />
              </Field>
              <div className="grid grid-cols-[8.5rem_1fr] gap-2">
                <Field label="Documento">
                  <select
                    value={draft.tipoIdentificacion}
                    onChange={(event) => {
                      const type = event.target.value as 'CEDULA' | 'PASAPORTE'
                      setDraft({
                        ...draft,
                        tipoIdentificacion: type,
                        identificacion: formatIdentification(draft.identificacion, type),
                      })
                    }}
                    className={inputClass}
                  >
                    <option value="CEDULA">Cédula</option>
                    <option value="PASAPORTE">Pasaporte</option>
                  </select>
                </Field>
                <Field label="Número">
                  <input
                    required
                    inputMode={draft.tipoIdentificacion === 'CEDULA' ? 'numeric' : 'text'}
                    maxLength={draft.tipoIdentificacion === 'CEDULA' ? 13 : 100}
                    placeholder={
                      draft.tipoIdentificacion === 'CEDULA' ? '001-1234567-8' : 'Pasaporte'
                    }
                    value={draft.identificacion}
                    onChange={(event) =>
                      setDraft({
                        ...draft,
                        identificacion:
                          draft.tipoIdentificacion === 'CEDULA'
                            ? formatCedula(event.target.value)
                            : event.target.value,
                      })
                    }
                    className={inputClass}
                  />
                </Field>
              </div>
              <Field label="Correo electrónico">
                <input
                  required
                  type="email"
                  autoComplete="email"
                  placeholder="nombre@correo.com"
                  value={draft.correo}
                  onChange={(event) => setDraft({ ...draft, correo: event.target.value })}
                  className={inputClass}
                />
              </Field>
              <Field label="Teléfono">
                <input
                  required
                  inputMode="tel"
                  autoComplete="tel"
                  maxLength={12}
                  placeholder="809-123-4567"
                  value={draft.telefono}
                  onChange={(event) =>
                    setDraft({ ...draft, telefono: formatPhone(event.target.value) })
                  }
                  className={inputClass}
                />
              </Field>
              <Field label="Rol" wide>
                <select
                  required
                  value={draft.rol}
                  onChange={(event) =>
                    setDraft({
                      ...draft,
                      rol: event.target.value as PublicRegistrationDraft['rol'],
                    })
                  }
                  className={inputClass}
                >
                  <option value="">Selecciona un rol</option>
                  <option value="ADMINISTRADOR_EMPRESA">Administrador de Empresa</option>
                  <option value="USUARIO_DELEGADO">Usuario Delegado</option>
                </select>
              </Field>
              <Field label="Contraseña" wide>
                <span className="relative block">
                  <input
                    required
                    type={showPassword ? 'text' : 'password'}
                    autoComplete="new-password"
                    minLength={8}
                    maxLength={128}
                    placeholder="Crea una contraseña segura"
                    value={draft.password}
                    onChange={(event) => setDraft({ ...draft, password: event.target.value })}
                    className={`${inputClass} pr-12`}
                  />
                  <button
                    type="button"
                    aria-label={showPassword ? 'Ocultar contraseña' : 'Mostrar contraseña'}
                    aria-pressed={showPassword}
                    onClick={() => setShowPassword((value) => !value)}
                    className="absolute top-1/2 right-1 grid h-9 w-9 -translate-y-1/2 place-items-center rounded-lg text-slate-500 transition hover:bg-emerald-50 hover:text-[#125640]"
                  >
                    {showPassword ? <EyeIcon /> : <EyeOffIcon />}
                  </button>
                </span>
              </Field>
              {draft.password && (
                <div className="rounded-xl bg-slate-50 p-2 sm:col-span-2">
                  <PasswordValidatorUI passwordValue={draft.password} />
                </div>
              )}

              <h2 className="text-base font-extrabold text-[#125640] sm:col-span-2">Adjuntos</h2>
              <label className="sm:col-span-2">
                <span className="mb-1.5 block text-xs font-bold text-[#153f34]">
                  Carta de autorización *
                </span>
                <span
                  className="flex min-h-20 cursor-pointer flex-col items-center justify-center rounded-xl border border-dashed border-[#16835f] bg-emerald-50/35 px-4 text-center transition hover:bg-emerald-50"
                  onDragOver={(event) => event.preventDefault()}
                  onDrop={(event) => {
                    event.preventDefault()
                    selectFile(event.dataTransfer.files[0] ?? null)
                  }}
                >
                  <strong className="text-sm text-[#125640]">
                    {file ? file.name : 'Arrastra y suelta el archivo aquí'}
                  </strong>
                  <span className="mt-1 text-xs text-slate-500">
                    {file
                      ? `${(file.size / 1024 / 1024).toFixed(2)} MB`
                      : 'o haz clic para seleccionar · PDF, JPG, PNG (máx. 5 MB)'}
                  </span>
                  <input
                    required
                    type="file"
                    accept="application/pdf,image/jpeg,image/png"
                    className="sr-only"
                    onChange={(event: ChangeEvent<HTMLInputElement>) =>
                      selectFile(event.target.files?.[0] ?? null)
                    }
                  />
                </span>
              </label>

              <label className="flex cursor-pointer items-start gap-3 text-xs leading-5 text-slate-600 sm:col-span-2">
                <input
                  required
                  type="checkbox"
                  checked={draft.termsAccepted}
                  onChange={(event) => setDraft({ ...draft, termsAccepted: event.target.checked })}
                  className="mt-0.5 h-5 w-5 rounded accent-[#125640]"
                />
                <span>Acepto los términos y condiciones y la política de privacidad.</span>
              </label>

              <button
                disabled={
                  saving ||
                  !file ||
                  !draft.rol ||
                  !draft.termsAccepted ||
                  !isPasswordValid(draft.password)
                }
                className="min-h-11 rounded-xl bg-[linear-gradient(90deg,#087452,#125640)] px-5 font-bold text-white shadow-[0_10px_24px_rgba(18,86,64,0.2)] transition hover:brightness-110 disabled:cursor-not-allowed disabled:opacity-55 sm:col-span-2"
              >
                {saving ? 'Enviando solicitud…' : 'Crear cuenta  →'}
              </button>
            </form>
          </div>
        </div>
      </section>
    </main>
  )
}

const inputClass =
  'mt-1 min-h-10 w-full rounded-lg border border-slate-300 bg-white px-3 text-sm font-normal text-slate-800 shadow-sm outline-none transition focus:border-[#16835f] focus:ring-3 focus:ring-[#16835f]/12'

function Field({
  label,
  wide,
  children,
}: {
  label: string
  wide?: boolean
  children: React.ReactNode
}) {
  return (
    <label className={`text-xs font-bold text-[#153f34] ${wide ? 'sm:col-span-2' : ''}`}>
      {label} *{children}
    </label>
  )
}

function EyeIcon() {
  return (
    <svg
      className="h-5 w-5"
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
      className="h-5 w-5"
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
