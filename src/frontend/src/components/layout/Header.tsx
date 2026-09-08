interface HeaderProps {
  isOnline: boolean
  isSyncing: boolean
  pendingCount: number
  onOpenMenu: () => void
}

export function Header({ isOnline, isSyncing, pendingCount, onOpenMenu }: HeaderProps) {
  const syncLabel = isSyncing
    ? 'Sincronizando'
    : isOnline
      ? pendingCount > 0
        ? `${pendingCount} pendientes`
        : 'Sincronizado'
      : 'Modo sin conexión'

  return (
    <header className="sticky top-0 z-20 border-b border-slate-200/80 bg-white/90 px-4 py-3 backdrop-blur md:px-6 lg:px-8">
      <div className="mx-auto flex max-w-7xl items-center justify-between gap-3">
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
          <div className="min-w-0">
            <p className="truncate text-xs font-semibold tracking-[0.12em] text-brand-700 uppercase">
              Panel operativo
            </p>
            <p className="truncate text-sm font-medium text-ink-strong">
              Dirección de Inspección BPM
            </p>
          </div>
        </div>

        <div className="flex items-center gap-2 sm:gap-3">
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
            <span className="absolute top-2.5 right-2.5 size-2 rounded-full bg-bpm-it ring-2 ring-white" />
          </button>
          <button
            type="button"
            className="flex items-center gap-2 rounded-xl p-1.5 text-left hover:bg-surface-muted"
            aria-label="Abrir menú de usuario"
          >
            <span className="grid size-9 place-items-center rounded-lg bg-brand-100 text-sm font-bold text-brand-700">
              EM
            </span>
            <span className="hidden text-sm font-semibold text-ink-strong md:block">
              Elena Martínez
            </span>
          </button>
        </div>
      </div>
    </header>
  )
}
