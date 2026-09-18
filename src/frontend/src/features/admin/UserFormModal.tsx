import { useState, type FormEvent } from 'react'
import { PasswordValidatorUI } from '../../components/feedback/PasswordValidatorUI'
import type { ManagedUser, ManagedUserDraft, UserManagementOptions } from '../../lib/api'
import {
  formatCedula,
  formatIdentification,
  formatPhone,
  formatStatusLabel,
} from '../../lib/formatters'
import { isPasswordValid } from '../../lib/passwordPolicy'
import { useParameterOptions } from '../../hooks/useParameterOptions'

const maximumAuthorizationSize = 5 * 1024 * 1024
const acceptedAuthorizationTypes = ['application/pdf', 'image/jpeg', 'image/png']

interface UserFormModalProps {
  user: ManagedUser | null
  options: UserManagementOptions
  onClose: () => void
  onSave: (draft: ManagedUserDraft, authorizationLetter: File | null) => Promise<void>
}

function initialDraft(user: ManagedUser | null): ManagedUserDraft {
  return {
    nombreCompleto: user?.nombreCompleto ?? '',
    correo: user?.correo ?? '',
    tipoIdentificacion: user?.tipoIdentificacion ?? '',
    identificacion: formatIdentification(
      user?.identificacion ?? '',
      user?.tipoIdentificacion ?? '',
    ),
    telefono: formatPhone(user?.telefono ?? ''),
    empresaId: user?.empresaId ?? null,
    rol: user?.roles[0] ?? '',
    estado: user?.estado ?? 'ACTIVO',
    temporaryPassword: '',
    versionFila: user?.versionFila ?? null,
  }
}

export function UserFormModal({ user, options, onClose, onSave }: UserFormModalProps) {
  const [draft, setDraft] = useState(() => initialDraft(user))
  const [saving, setSaving] = useState(false)
  const [authorizationLetter, setAuthorizationLetter] = useState<File | null>(null)
  const [authorizationError, setAuthorizationError] = useState('')
  const identificationTypes = useParameterOptions('TIPO_IDENTIFICACION')
  const userStates = useParameterOptions('ESTADO_USUARIO_GESTION')

  async function submit(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    try {
      await onSave(draft, authorizationLetter)
    } finally {
      setSaving(false)
    }
  }

  const enterpriseRole = ['ADMINISTRADOR_EMPRESA', 'USUARIO_DELEGADO'].includes(draft.rol)
  const companyRequired = enterpriseRole && draft.estado === 'ACTIVO'
  const passwordReady = user
    ? !draft.temporaryPassword || isPasswordValid(draft.temporaryPassword)
    : isPasswordValid(draft.temporaryPassword)

  function selectAuthorizationLetter(file: File | null) {
    setAuthorizationError('')
    if (!file) {
      setAuthorizationLetter(null)
      return
    }
    if (!acceptedAuthorizationTypes.includes(file.type)) {
      setAuthorizationLetter(null)
      setAuthorizationError('Solo se permiten archivos PDF, JPG, JPEG o PNG.')
      return
    }
    if (file.size > maximumAuthorizationSize) {
      setAuthorizationLetter(null)
      setAuthorizationError('El archivo no puede superar 5 MB.')
      return
    }
    setAuthorizationLetter(file)
  }

  return (
    <div
      className="sigersa-modal-overlay fixed inset-0 z-50 flex items-center justify-center p-4"
      role="presentation"
    >
      <div
        className="sigersa-modal-panel w-full max-w-3xl"
        role="dialog"
        aria-modal="true"
        aria-labelledby="user-modal-title"
      >
        <div className="flex items-start justify-between border-b border-slate-200 px-6 py-5">
          <div>
            <p className="text-xs font-bold tracking-[0.14em] text-brand-700 uppercase">
              Gestión de acceso
            </p>
            <h2 id="user-modal-title" className="mt-1 text-xl font-extrabold text-ink-strong">
              {user ? 'Editar usuario' : 'Nuevo usuario'}
            </h2>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg px-3 py-2 text-xl text-slate-500 hover:bg-slate-100"
            aria-label="Cerrar formulario"
          >
            ×
          </button>
        </div>

        <form onSubmit={submit} className="grid gap-4 p-6 md:grid-cols-2">
          <label className="text-sm font-bold text-ink-body md:col-span-2">
            Nombre completo
            <input
              required
              maxLength={250}
              value={draft.nombreCompleto}
              onChange={(event) => setDraft({ ...draft, nombreCompleto: event.target.value })}
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
            />
          </label>
          <label className="text-sm font-bold text-ink-body">
            Correo electrónico
            <input
              required
              type="email"
              value={draft.correo}
              onChange={(event) => setDraft({ ...draft, correo: event.target.value })}
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
            />
          </label>
          <label className="text-sm font-bold text-ink-body">
            Teléfono
            <input
              inputMode="tel"
              pattern="(809|829|849)-[0-9]{3}-[0-9]{4}"
              title="Use 10 dígitos y un prefijo 809, 829 o 849."
              maxLength={12}
              placeholder="000-000-0000"
              value={draft.telefono}
              onChange={(event) =>
                setDraft({ ...draft, telefono: formatPhone(event.target.value) })
              }
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
            />
          </label>
          <label className="text-sm font-bold text-ink-body">
            Tipo de identificación
            <select
              required
              value={draft.tipoIdentificacion}
              onChange={(event) => {
                const type = event.target.value
                setDraft({
                  ...draft,
                  tipoIdentificacion: type,
                  identificacion: formatIdentification(draft.identificacion, type),
                })
              }}
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
            >
              <option value="">Seleccione</option>
              {identificationTypes.options.map((option) => (
                <option key={option.parametersId} value={option.stringData ?? ''}>
                  {formatStatusLabel(option.stringData ?? '')}
                </option>
              ))}
            </select>
          </label>
          <label className="text-sm font-bold text-ink-body">
            Identificación
            <input
              required
              inputMode={draft.tipoIdentificacion === 'CEDULA' ? 'numeric' : 'text'}
              maxLength={draft.tipoIdentificacion === 'CEDULA' ? 13 : 100}
              placeholder={draft.tipoIdentificacion === 'CEDULA' ? '000-0000000-0' : undefined}
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
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
            />
          </label>
          <label className="text-sm font-bold text-ink-body md:col-span-2">
            Rol
            <select
              required
              value={draft.rol}
              onChange={(event) => setDraft({ ...draft, rol: event.target.value })}
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
            >
              <option value="">Seleccione un rol</option>
              {options.roles.map((role) => (
                <option key={role.code} value={role.code}>
                  {role.name}
                </option>
              ))}
            </select>
          </label>
          <label className="text-sm font-bold text-ink-body">
            Empresa o ámbito
            <select
              required={companyRequired}
              value={draft.empresaId ?? ''}
              onChange={(event) => setDraft({ ...draft, empresaId: event.target.value || null })}
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
            >
              <option value="">Pendiente de asignar empresa</option>
              {options.companies.map((company) => (
                <option key={company.id} value={company.id}>
                  {company.name}
                </option>
              ))}
            </select>
          </label>
          <label className="text-sm font-bold text-ink-body">
            Estado
            <select
              required
              value={draft.estado}
              onChange={(event) =>
                setDraft({ ...draft, estado: event.target.value as ManagedUserDraft['estado'] })
              }
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
            >
              <option value="">Seleccione</option>
              {userStates.options
                .filter((option) =>
                  (user
                    ? ['PENDIENTE_VALIDACION', 'ACTIVO', 'RECHAZADO']
                    : ['ACTIVO', 'RECHAZADO']
                  ).includes(option.stringData ?? ''),
                )
                .map((option) => (
                  <option key={option.parametersId} value={option.stringData ?? ''}>
                    {option.stringData === 'ACTIVO'
                      ? 'Aprobado'
                      : option.stringData === 'PENDIENTE_VALIDACION'
                        ? 'Pendiente Validación'
                        : option.stringData === 'RECHAZADO'
                          ? 'Rechazado'
                          : formatStatusLabel(option.stringData ?? '')}
                  </option>
                ))}
            </select>
          </label>
          <label className="text-sm font-bold text-ink-body">
            {user ? 'Nueva contraseña temporal (opcional)' : 'Contraseña temporal'}
            <input
              required={!user}
              type="password"
              minLength={8}
              maxLength={128}
              value={draft.temporaryPassword}
              onChange={(event) => setDraft({ ...draft, temporaryPassword: event.target.value })}
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
            />
          </label>
          <div className="rounded-xl border border-slate-200 bg-slate-50 p-4 md:col-span-2">
            <PasswordValidatorUI passwordValue={draft.temporaryPassword} />
            {user && !draft.temporaryPassword && (
              <p className="mt-3 text-xs text-ink-muted">
                Deje el campo vacío para conservar la contraseña actual.
              </p>
            )}
          </div>
          {enterpriseRole && (
            <label className="text-sm font-bold text-ink-body md:col-span-2">
              Carta de autorización {user ? '(opcional para reemplazar)' : ''}
              <input
                required={!user}
                type="file"
                accept=".pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png"
                onChange={(event) => {
                  selectAuthorizationLetter(event.target.files?.[0] ?? null)
                  if (
                    event.target.files?.[0] &&
                    (!acceptedAuthorizationTypes.includes(event.target.files[0].type) ||
                      event.target.files[0].size > maximumAuthorizationSize)
                  ) {
                    event.target.value = ''
                  }
                }}
                className="mt-1.5 block w-full rounded-xl border border-slate-300 bg-white p-2 font-normal"
              />
              <span className="mt-1 block text-xs font-normal text-ink-muted">
                PDF, JPG, JPEG o PNG; máximo 5 MB.
              </span>
              {authorizationError && (
                <span className="mt-1 block text-xs font-semibold text-red-700" role="alert">
                  {authorizationError}
                </span>
              )}
            </label>
          )}
          <div className="flex justify-end gap-3 border-t border-slate-200 pt-5 md:col-span-2">
            <button
              type="button"
              onClick={onClose}
              className="min-h-11 rounded-xl border border-slate-300 px-5 font-bold text-ink-body"
            >
              Cancelar
            </button>
            <button
              disabled={
                saving || !passwordReady || (enterpriseRole && !user && !authorizationLetter)
              }
              className="min-h-11 rounded-xl bg-brand-700 px-5 font-bold text-white disabled:opacity-60"
            >
              {saving ? 'Guardando…' : user ? 'Guardar cambios' : 'Crear usuario'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}
