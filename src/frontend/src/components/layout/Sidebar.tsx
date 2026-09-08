const navigation = [
  { label: 'Resumen', icon: 'M4 13h6V4H4v9Zm10 7h6v-9h-6v9ZM4 20h6v-3H4v3Zm10-13h6V4h-6v3Z' },
  { label: 'Evaluaciones', icon: 'M7 3h10v4h4v14H3V7h4V3Zm2 2v4h6V5H9Zm-2 8h10M7 17h7' },
  {
    label: 'Programación',
    icon: 'M6 2v3m12-3v3M4 9h16M5 5h14a2 2 0 0 1 2 2v13H3V7a2 2 0 0 1 2-2Zm3 8h3v3H8v-3Z',
  },
  {
    label: 'Establecimientos',
    icon: 'M4 21V8l8-5 8 5v13M8 21v-5h8v5M8 10h.01M12 10h.01M16 10h.01',
  },
  { label: 'Fichas BPM', icon: 'M6 3h12a2 2 0 0 1 2 2v16H4V5a2 2 0 0 1 2-2Zm2 5h8M8 12h8M8 16h5' },
  { label: 'Reportes', icon: 'M4 20V10h4v10H4Zm6 0V4h4v16h-4Zm6 0v-7h4v7h-4Z' },
]

interface SidebarProps {
  mobile?: boolean
  onNavigate?: () => void
}

export function Sidebar({ mobile = false, onNavigate }: SidebarProps) {
  return (
    <div className="flex h-full flex-col bg-surface-inverse text-white">
      <div className="flex h-20 items-center gap-3 px-5">
        <span className="grid size-11 place-items-center rounded-xl bg-brand-500 shadow-lg shadow-black/20">
          <svg viewBox="0 0 32 32" className="size-7" aria-hidden="true">
            <path
              fill="currentColor"
              d="M16 3C9.4 3 4 7 4 12c0 3.3 2.4 6.2 6 7.8V25a6 6 0 0 0 12 0v-5.2c3.6-1.6 6-4.5 6-7.8 0-5-5.4-9-12-9Zm0 7 3.5 4.8L16 19.5l-3.5-4.7L16 10Z"
            />
          </svg>
        </span>
        <div>
          <p className="text-xl font-extrabold tracking-tight">SIGERSA</p>
          <p className="text-[0.66rem] font-semibold tracking-[0.14em] text-emerald-100 uppercase">
            Riesgo sanitario
          </p>
        </div>
      </div>

      <nav
        className="flex-1 overflow-y-auto px-3 py-5"
        aria-label={mobile ? 'Navegación móvil' : 'Navegación principal'}
      >
        <p className="px-3 pb-2 text-[0.65rem] font-bold tracking-[0.16em] text-slate-400 uppercase">
          Operaciones
        </p>
        <ul className="space-y-1">
          {navigation.map((item, index) => (
            <li key={item.label}>
              <a
                href={
                  index === 0 ? '#main-content' : `#${item.label.toLowerCase().replace(' ', '-')}`
                }
                className={`flex min-h-11 items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition-colors ${
                  index === 0
                    ? 'bg-white/12 text-white'
                    : 'text-slate-300 hover:bg-white/8 hover:text-white'
                }`}
                aria-current={index === 0 ? 'page' : undefined}
                onClick={onNavigate}
              >
                <svg
                  viewBox="0 0 24 24"
                  className="size-5 shrink-0"
                  fill="none"
                  stroke="currentColor"
                  aria-hidden="true"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth="1.7"
                    d={item.icon}
                  />
                </svg>
                {item.label}
              </a>
            </li>
          ))}
        </ul>
      </nav>

      <div className="border-t border-white/10 p-4">
        <div className="rounded-xl bg-white/6 p-3">
          <p className="text-xs font-semibold text-emerald-100">Centro de ayuda</p>
          <p className="mt-1 text-xs leading-5 text-slate-400">
            Guías de inspección y soporte técnico.
          </p>
        </div>
      </div>
    </div>
  )
}
