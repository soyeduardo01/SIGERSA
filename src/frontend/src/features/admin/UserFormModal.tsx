import { useState, type FormEvent } from 'react'
import { PasswordValidatorUI } from '../../components/feedback/PasswordValidatorUI'
import type { ManagedUser, ManagedUserDraft, UserManagementOptions } from '../../lib/api'
import { formatCedula, formatIdentification, formatPhone } from '../../lib/formatters'
import { isPasswordValid } from '../../lib/passwordPolicy'

interface UserFormModalProps {
  user: ManagedUser | null
  options: UserManagementOptions
  onClose: () => void
  onSave: (draft: ManagedUserDraft) => Promise<void>
}

function initialDraft(user: ManagedUser | null): ManagedUserDraft {
  return {
    nombreCompleto: user?.nombreCompleto ?? '',
    correo: user?.correo ?? '',
    tipoIdentificacion: user?.tipoIdentificacion ?? 'CEDULA',
    identificacion: formatIdentification(
      user?.identificacion ?? '',
      user?.tipoIdentificacion ?? 'CEDULA',
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

  async function submit(event: FormEvent) {
    event.preventDefault()
    setSaving(true)
    try {
      await onSave(draft)
    } finally {
      setSaving(false)
    }
  }

  const enterpriseRole = ['ADMINISTRADOR_EMPRESA', 'USUARIO_DELEGADO'].includes(draft.rol)
  const passwordReady = user
    ? !draft.temporaryPassword || isPasswordValid(draft.temporaryPassword)
    : isPasswordValid(draft.temporaryPassword)

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/55 p-4"
      role="dialog"
      aria-modal="true"
      aria-labelledby="user-modal-title"
    >
      <div className="max-h-[92vh] w-full max-w-3xl overflow-y-auto rounded-card bg-white shadow-2xl">
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
              <option value="CEDULA">Cédula</option>
              <option value="PASAPORTE">Pasaporte</option>
              <option value="RNC">RNC</option>
              <option value="OTRO">Otro</option>
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
              required={enterpriseRole}
              value={draft.empresaId ?? ''}
              onChange={(event) => setDraft({ ...draft, empresaId: event.target.value || null })}
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
            >
              <option value="">Ámbito institucional</option>
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
              value={draft.estado}
              onChange={(event) =>
                setDraft({ ...draft, estado: event.target.value as ManagedUserDraft['estado'] })
              }
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
            >
              <option value="ACTIVO">Activo</option>
              <option value="SUSPENDIDO">Suspendido</option>
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
          <div className="flex justify-end gap-3 border-t border-slate-200 pt-5 md:col-span-2">
            <button
              type="button"
              onClick={onClose}
              className="min-h-11 rounded-xl border border-slate-300 px-5 font-bold text-ink-body"
            >
              Cancelar
            </button>
            <button
              disabled={saving || !passwordReady}
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
