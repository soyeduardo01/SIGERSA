import { useEffect, useState, type FormEvent } from 'react'
import { TableSkeleton } from '../../components/feedback/Skeletons'
import {
  createManagedUser,
  downloadUserAuthorizationLetter,
  getManagedUsers,
  getUserManagementOptions,
  setManagedUserSuspension,
  uploadUserAuthorizationLetter,
  updateManagedUser,
  type ManagedUser,
  type ManagedUserDraft,
  type ManagedUsersPage,
  type UserManagementOptions,
} from '../../lib/api'
import { alerts } from '../../lib/alerts'
import { formatIdentification, formatPhone, formatStatusLabel } from '../../lib/formatters'
import { renderAuthorizationPreview } from './authorizationPreview'
import { UserFormModal } from './UserFormModal'
import { useParameterOptions } from '../../hooks/useParameterOptions'

const emptyPage: ManagedUsersPage = { items: [], page: 1, pageSize: 10, total: 0 }

export function UsersManagement() {
  const userStates = useParameterOptions('ESTADO_USUARIO_GESTION')
  const [result, setResult] = useState<ManagedUsersPage>(emptyPage)
  const [options, setOptions] = useState<UserManagementOptions | null>(null)
  const [searchInput, setSearchInput] = useState('')
  const [search, setSearch] = useState('')
  const [role, setRole] = useState('')
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<ManagedUser | null>(null)

  useEffect(() => {
    let active = true
    void getUserManagementOptions()
      .then((value) => active && setOptions(value))
      .catch(async (caught) => {
        if (!active) return
        setError(caught instanceof Error ? caught.message : 'No se pudieron cargar los permisos.')
        await alerts.error(caught, 'No se pudo preparar Gestión de usuarios')
      })
    return () => {
      active = false
    }
  }, [])

  useEffect(() => {
    let active = true
    void getManagedUsers({ search, role, status, page, pageSize: 10 })
      .then((value) => {
        if (active) {
          setResult(value)
          setError('')
        }
      })
      .catch((caught) => {
        if (active)
          setError(caught instanceof Error ? caught.message : 'No fue posible cargar los usuarios.')
      })
      .finally(() => active && setLoading(false))
    return () => {
      active = false
    }
  }, [page, role, search, status])

  async function reload() {
    setLoading(true)
    try {
      setResult(await getManagedUsers({ search, role, status, page, pageSize: 10 }))
      setError('')
    } catch (caught) {
      setError(caught instanceof Error ? caught.message : 'No fue posible cargar los usuarios.')
      throw caught
    } finally {
      setLoading(false)
    }
  }

  function applySearch(event: FormEvent) {
    event.preventDefault()
    setLoading(true)
    setPage(1)
    setSearch(searchInput.trim())
  }

  async function save(draft: ManagedUserDraft, authorizationLetter: File | null) {
    try {
      let userId = editing?.id
      if (editing) await updateManagedUser(editing.id, draft)
      else userId = (await createManagedUser(draft)).id
      if (authorizationLetter && userId)
        await uploadUserAuthorizationLetter(userId, authorizationLetter)
      setModalOpen(false)
      await reload()
      await alerts.success(
        editing ? 'Usuario actualizado' : 'Usuario creado',
        'Los datos y permisos quedaron guardados correctamente.',
      )
    } catch (caught) {
      await alerts.error(caught, 'No se pudo guardar el usuario')
      throw caught
    }
  }

  async function toggleSuspension(user: ManagedUser) {
    const suspended = user.estado !== 'SUSPENDIDO'
    const confirmed = await alerts.confirm({
      title: suspended ? '¿Bloquear este usuario?' : '¿Activar este usuario?',
      text: suspended
        ? `${user.nombreCompleto} perderá acceso hasta que su cuenta sea activada.`
        : `${user.nombreCompleto} podrá volver a iniciar sesión.`,
      confirmText: suspended ? 'Bloquear usuario' : 'Activar usuario',
    })
    if (!confirmed) return
    try {
      await setManagedUserSuspension(user.id, suspended, user.versionFila)
      await reload()
      await alerts.success(suspended ? 'Usuario bloqueado' : 'Usuario activado')
    } catch (caught) {
      await alerts.error(caught, 'No se pudo cambiar el estado')
    }
  }

  async function viewAuthorizationLetter(user: ManagedUser) {
    const preview = window.open('', '_blank')
    if (preview) preview.opener = null
    try {
      const { blob, fileName } = await downloadUserAuthorizationLetter(user.id)
      const url = URL.createObjectURL(blob)
      if (preview) renderAuthorizationPreview(preview, url, blob.type, fileName)
      else {
        const link = document.createElement('a')
        link.href = url
        link.download = fileName
        link.rel = 'noopener noreferrer'
        document.body.appendChild(link)
        link.click()
        link.remove()
      }
      window.setTimeout(() => URL.revokeObjectURL(url), 60_000)
    } catch (caught) {
      preview?.close()
      await alerts.error(caught, 'No se pudo abrir la carta de autorización')
    }
  }

  const approvalLabel = (value: string) => {
    if (value === 'ACTIVO') return 'Aprobado'
    if (value === 'PENDIENTE_VALIDACION') return 'Pendiente Validación'
    if (value === 'RECHAZADO') return 'Rechazado'
    return formatStatusLabel(value)
  }

  const pages = Math.max(1, Math.ceil(result.total / result.pageSize))

  return (
    <section aria-labelledby="users-title">
      <div className="flex flex-col justify-between gap-4 md:flex-row md:items-end">
        <div>
          <p className="text-xs font-bold tracking-[0.14em] text-brand-700 uppercase">
            Administración
          </p>
          <h1 id="users-title" className="mt-2 text-2xl font-extrabold text-ink-strong">
            Gestión de usuarios
          </h1>
          <p className="mt-2 text-sm text-ink-muted">
            Consulte cuentas, ámbitos, estados y permisos conforme a su rol.
          </p>
        </div>
        {options?.canManage && (
          <button
            type="button"
            onClick={() => {
              setEditing(null)
              setModalOpen(true)
            }}
            className="hover:bg-brand-800 min-h-11 rounded-xl bg-brand-700 px-5 font-bold text-white shadow-sm"
          >
            + Nuevo usuario
          </button>
        )}
      </div>

      <div className="mt-6 rounded-card bg-white p-5 shadow-card">
        <form
          onSubmit={applySearch}
          className="grid gap-3 lg:grid-cols-[minmax(14rem,1fr)_14rem_12rem_auto]"
        >
          <label className="text-sm font-bold text-ink-body">
            Buscar
            <input
              value={searchInput}
              onChange={(event) => setSearchInput(event.target.value)}
              placeholder="Nombre, correo o identificación"
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
            />
          </label>
          <label className="text-sm font-bold text-ink-body">
            Rol
            <select
              value={role}
              onChange={(event) => {
                setLoading(true)
                setRole(event.target.value)
                setPage(1)
              }}
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
            >
              <option value="">Todos los roles</option>
              {options?.roles.map((item) => (
                <option key={item.code} value={item.code}>
                  {item.name}
                </option>
              ))}
            </select>
          </label>
          <label className="text-sm font-bold text-ink-body">
            Estado
            <select
              value={status}
              onChange={(event) => {
                setLoading(true)
                setStatus(event.target.value)
                setPage(1)
              }}
              className="mt-1.5 min-h-11 w-full rounded-xl border border-slate-300 px-3 font-normal"
            >
              <option value="">Todos</option>
              {userStates.options.map((item) => (
                <option key={item.parametersId} value={item.stringData ?? ''}>
                  {approvalLabel(item.stringData ?? '')}
                </option>
              ))}
            </select>
          </label>
          <button className="text-brand-800 mt-auto min-h-11 rounded-xl border border-brand-700 px-5 font-bold">
            Aplicar
          </button>
        </form>

        {error && (
          <div
            role="alert"
            className="mt-5 rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800"
          >
            {error}
          </div>
        )}

        <div className="mt-5 overflow-x-auto">
          <table className="w-full min-w-[850px] border-collapse text-left text-sm">
            <thead>
              <tr className="border-b border-slate-200 text-xs tracking-wide text-ink-muted uppercase">
                <th className="px-3 py-3">Nombre</th>
                <th className="px-3 py-3">Correo</th>
                <th className="px-3 py-3">Rol</th>
                <th className="px-3 py-3">Ámbito/Empresa</th>
                <th className="px-3 py-3">Estado</th>
                <th className="px-3 py-3 text-right">Acciones</th>
              </tr>
            </thead>
            <tbody>
              {result.items.map((user) => (
                <tr key={user.id} className="border-b border-slate-100 align-top">
                  <td className="px-3 py-4 font-bold text-ink-strong">
                    {user.nombreCompleto}
                    <span className="mt-1 block text-xs font-normal text-ink-muted">
                      {user.tipoIdentificacion}:{' '}
                      {formatIdentification(user.identificacion, user.tipoIdentificacion)}
                    </span>
                  </td>
                  <td className="px-3 py-4 text-ink-body">
                    {user.correo}
                    {user.telefono && (
                      <span className="mt-1 block text-xs text-ink-muted">
                        {formatPhone(user.telefono)}
                      </span>
                    )}
                  </td>
                  <td className="px-3 py-4 text-ink-body">
                    {user.roleNames?.join(', ') || user.roles.join(', ') || 'Sin rol'}
                  </td>
                  <td className="px-3 py-4 text-ink-body">
                    {user.empresaNombre ??
                      (user.roles.some((item) =>
                        ['ADMINISTRADOR_EMPRESA', 'USUARIO_DELEGADO'].includes(item),
                      )
                        ? 'Pendiente de asignación'
                        : 'Institucional')}
                  </td>
                  <td className="px-3 py-4">
                    <span
                      className={`rounded-full px-2.5 py-1 text-xs font-bold ${user.estado === 'ACTIVO' ? 'bg-emerald-100 text-emerald-800' : user.estado === 'RECHAZADO' ? 'bg-red-100 text-red-800' : 'bg-amber-100 text-amber-900'}`}
                    >
                      {approvalLabel(user.estado)}
                    </span>
                  </td>
                  <td className="px-3 py-4 text-right">
                    {options?.canManage ? (
                      <div className="flex justify-end gap-2">
                        {user.roles.some((item) =>
                          ['ADMINISTRADOR_EMPRESA', 'USUARIO_DELEGADO'].includes(item),
                        ) && (
                          <button
                            type="button"
                            onClick={() => void viewAuthorizationLetter(user)}
                            className="text-brand-800 rounded-lg border border-emerald-300 px-3 py-2 font-bold"
                          >
                            Ver carta
                          </button>
                        )}
                        <button
                          type="button"
                          onClick={() => {
                            setEditing(user)
                            setModalOpen(true)
                          }}
                          className="rounded-lg border border-slate-300 px-3 py-2 font-bold text-ink-body"
                        >
                          {user.estado === 'PENDIENTE_VALIDACION' ? 'Revisar' : 'Editar'}
                        </button>
                        <button
                          type="button"
                          onClick={() => void toggleSuspension(user)}
                          className="rounded-lg border border-slate-300 px-3 py-2 font-bold text-ink-body"
                        >
                          {user.estado === 'SUSPENDIDO' ? 'Activar' : 'Bloquear'}
                        </button>
                      </div>
                    ) : (
                      <span className="text-xs text-ink-muted">Solo lectura</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {!loading && result.items.length === 0 && !error && (
            <div className="p-10 text-center text-sm text-ink-muted">
              No hay usuarios que coincidan con los filtros seleccionados.
            </div>
          )}
          {loading && <TableSkeleton rows={5} columns={6} />}
        </div>

        <div className="mt-5 flex items-center justify-between border-t border-slate-200 pt-4 text-sm">
          <span className="text-ink-muted">{result.total} usuario(s)</span>
          <div className="flex items-center gap-3">
            <button
              type="button"
              disabled={page <= 1 || loading}
              onClick={() => {
                setLoading(true)
                setPage((value) => Math.max(1, value - 1))
              }}
              className="rounded-lg border border-slate-300 px-3 py-2 font-bold disabled:opacity-40"
            >
              Anterior
            </button>
            <span>
              Página {page} de {pages}
            </span>
            <button
              type="button"
              disabled={page >= pages || loading}
              onClick={() => {
                setLoading(true)
                setPage((value) => Math.min(pages, value + 1))
              }}
              className="rounded-lg border border-slate-300 px-3 py-2 font-bold disabled:opacity-40"
            >
              Siguiente
            </button>
          </div>
        </div>
      </div>

      {modalOpen && options && (
        <UserFormModal
          user={editing}
          options={options}
          onClose={() => setModalOpen(false)}
          onSave={save}
        />
      )}
    </section>
  )
}
