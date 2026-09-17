import { useEffect, useRef, useState } from 'react'
import type { SessionIdentity } from '../../lib/api'
import { moduleHref } from '../../lib/navigation'
import { InstallPwaButton } from '../pwa/InstallPwaButton'

interface HeaderProps {
  isOnline: boolean
  isSyncing: boolean
  pendingCount: number
  identity: SessionIdentity
  roleLabel: string
  onOpenMenu: () => void
  onLogout: () => void
}

export function Header({
  isOnline,
  isSyncing,
  pendingCount,
  identity,
  roleLabel,
  onOpenMenu,
  onLogout,
}: HeaderProps) {
  const [profileOpen, setProfileOpen] = useState(false)
  const [notificationsOpen, setNotificationsOpen] = useState(false)
  const controlsRef = useRef<HTMLDivElement>(null)
  const syncLabel = isSyncing
    ? 'Sincronizando'
    : isOnline
      ? pendingCount > 0
        ? `${pendingCount} pendientes`
        : 'Sincronizado'
      : 'Modo sin conexión'

  useEffect(() => {
    const closeMenus = (event: MouseEvent) => {
      if (!controlsRef.current?.contains(event.target as Node)) {
        setProfileOpen(false)
        setNotificationsOpen(false)
      }
    }
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setProfileOpen(false)
        setNotificationsOpen(false)
      }
    }
    document.addEventListener('mousedown', closeMenus)
    document.addEventListener('keydown', closeOnEscape)
    return () => {
      document.removeEventListener('mousedown', closeMenus)
      document.removeEventListener('keydown', closeOnEscape)
    }
  }, [])

  return (
    <header className="sticky top-0 z-20 h-[76px] shrink-0 border-b border-slate-200/80 bg-white/95 px-4 backdrop-blur md:px-6 lg:px-7">
      <div className="mx-auto flex h-full max-w-[1540px] items-center justify-between gap-3">
        <div className="flex min-w-0 items-center gap-3">
          <button
            type="button"
            className="inline-flex size-11 shrink-0 items-center justify-center rounded-xl border border-slate-200 bg-white text-ink-strong shadow-sm hover:bg-surface-muted lg:hidden"
            aria-label="Abrir navegación"
            aria-controls="mobile-navigation"
            onClick={onOpenMenu}
          >
            <svg
              viewBox="0 0 24 24"
              className="size-5"
              fill="none"
              stroke="currentColor"
              aria-hidden="true"
            >
              <path strokeLinecap="round" strokeWidth="2" d="M4 7h16M4 12h16M4 17h16" />
            </svg>
          </button>
          <span className="hidden size-10 shrink-0 place-items-center rounded-xl border border-brand-100 bg-brand-50 text-brand-700 xl:grid">
            <svg
              viewBox="0 0 24 24"
              className="size-5"
              fill="none"
              stroke="currentColor"
              aria-hidden="true"
            >
              <path
                strokeWidth="1.8"
                d="M4 21V8l8-4 8 4v13M8 21v-5h8v5M8 10h.01M12 10h.01M16 10h.01"
              />
            </svg>
          </span>
          <div className="hidden min-w-0 xl:block">
            <p className="truncate text-sm font-bold text-ink-strong">
              Dirección de Inspección BPM
            </p>
            <p className="truncate text-xs text-ink-muted">Panel operativo del sistema SIGERSA</p>
          </div>
        </div>

        <div ref={controlsRef} className="relative ml-auto flex items-center gap-2 sm:gap-3">
          <InstallPwaButton className="hidden min-h-10 rounded-xl border border-brand-700 px-3 text-xs font-bold text-brand-800 hover:bg-brand-50 md:inline-flex md:items-center" />
          <div
            className="hidden items-center gap-2 rounded-full bg-surface-muted px-3 py-2 text-xs font-semibold text-ink-body sm:flex"
            role="status"
            aria-live="polite"
          >
            <span
              className={`size-2 rounded-full ${isOnline ? 'bg-risk-low' : 'bg-risk-medium'}`}
              aria-hidden="true"
            />
            {syncLabel}
          </div>
          <button
            type="button"
            className="relative inline-flex size-11 items-center justify-center rounded-xl text-ink-body hover:bg-surface-muted"
            aria-label="Notificaciones"
            aria-expanded={notificationsOpen}
            onClick={() => {
              setNotificationsOpen((open) => !open)
              setProfileOpen(false)
            }}
          >
            <svg
              viewBox="0 0 24 24"
              className="size-5"
              fill="none"
              stroke="currentColor"
              aria-hidden="true"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth="1.8"
                d="M15 17H9m9-2V11a6 6 0 1 0-12 0v4l-2 2h16l-2-2Zm-8 2a2 2 0 0 0 4 0"
              />
            </svg>
            {pendingCount > 0 && (
              <span className="absolute top-1.5 right-1.5 grid min-w-4 place-items-center rounded-full bg-bpm-it px-1 text-[0.6rem] font-bold text-white ring-2 ring-white">
                {pendingCount > 9 ? '9+' : pendingCount}
              </span>
            )}
          </button>
          <button
            type="button"
            className="flex items-center gap-2 rounded-xl p-1.5 text-left hover:bg-surface-muted"
            aria-label="Abrir menú de usuario"
            aria-expanded={profileOpen}
            onClick={() => {
              setProfileOpen((open) => !open)
              setNotificationsOpen(false)
            }}
          >
            <span className="grid size-9 place-items-center rounded-lg bg-brand-100 text-sm font-bold text-brand-700">
              {identity.initials}
            </span>
            <span className="hidden max-w-44 min-w-0 md:block">
              <span className="block truncate text-sm font-semibold text-ink-strong">
                {identity.name}
              </span>
              <span className="block truncate text-[0.65rem] text-ink-muted">{roleLabel}</span>
            </span>
            <span className="hidden text-xs text-ink-muted md:block" aria-hidden="true">
              ▾
            </span>
          </button>
          {notificationsOpen && (
            <section
              className="absolute top-14 right-12 w-[min(22rem,calc(100vw-2rem))] rounded-xl border border-slate-200 bg-white p-4 shadow-xl"
              aria-label="Bandeja de notificaciones"
            >
              <div className="flex items-center justify-between">
                <h2 className="font-extrabold text-ink-strong">Notificaciones</h2>
                <span className="text-xs font-semibold text-ink-muted">
                  {pendingCount} pendientes
                </span>
              </div>
              <div className="mt-3 rounded-lg bg-surface-muted p-3 text-sm text-ink-body">
                {pendingCount > 0
                  ? `Hay ${pendingCount} registro${pendingCount === 1 ? '' : 's'} pendiente${pendingCount === 1 ? '' : 's'} de sincronización.`
                  : isOnline
                    ? 'Todo está al día. No hay notificaciones nuevas.'
                    : 'Trabaja sin conexión. Los cambios se enviarán cuando vuelva la conexión.'}
              </div>
            </section>
          )}
          {profileOpen && (
            <section
              className="absolute top-14 right-0 w-64 rounded-xl border border-slate-200 bg-white p-2 shadow-xl"
              aria-label="Menú de usuario"
            >
              <div className="border-b border-slate-100 px-3 py-2">
                <p className="truncate text-sm font-bold text-ink-strong">{identity.name}</p>
                {identity.email && (
                  <p className="truncate text-xs text-ink-muted">{identity.email}</p>
                )}
                <span className="text-brand-800 mt-2 inline-flex rounded-full bg-brand-100 px-2.5 py-1 text-xs font-bold">
                  {roleLabel}
                </span>
              </div>
              <a
                href={moduleHref('perfil')}
                onClick={() => setProfileOpen(false)}
                className="mt-1 flex min-h-11 items-center rounded-lg px-3 text-sm font-semibold text-ink-body hover:bg-surface-muted"
              >
                Ver perfil
              </a>
              <button
                type="button"
                onClick={onLogout}
                className="flex min-h-11 w-full items-center rounded-lg px-3 text-left text-sm font-bold text-red-700 hover:bg-red-50"
              >
                Cerrar sesión
              </button>
            </section>
          )}
        </div>
      </div>
    </header>
  )
}
