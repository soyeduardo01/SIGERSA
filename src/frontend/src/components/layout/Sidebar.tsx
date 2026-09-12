import { useAuth } from '../../contexts/useAuth'
import { moduleHref } from '../../lib/navigation'
import { canAccessModule, type AppModule } from '../../lib/rbac'

interface NavigationItem {
  module: AppModule
  to: string
  label: string
  icon: string
}

const navigation: NavigationItem[] = [
  {
    module: 'resumen',
    to: '/resumen',
    label: 'Resumen',
    icon: 'M4 13h6V4H4v9Zm10 7h6v-9h-6v9ZM4 20h6v-3H4v3Zm10-13h6V4h-6v3Z',
  },
  {
    module: 'solicitudes',
    to: '/solicitudes',
    label: 'Solicitudes',
    icon: 'M6 3h12v18H6V3Zm3 5h6M9 12h6M9 16h4',
  },
  {
    module: 'alertas-denuncias',
    to: '/alertas-denuncias',
    label: 'Alertas y denuncias',
    icon: 'M12 3 2.8 20h18.4L12 3Zm0 6v5m0 3h.01',
  },
  {
    module: 'casos',
    to: '/casos',
    label: 'Casos',
    icon: 'M4 6h16v14H4V6Zm4 0V3h8v3M8 11h8M8 15h5',
  },
  {
    module: 'evaluaciones',
    to: '/evaluaciones',
    label: 'Evaluaciones',
    icon: 'M7 3h10v4h4v14H3V7h4V3Zm2 2v4h6V5H9Zm-2 8h10M7 17h7',
  },
  {
    module: 'programacion',
    to: '/programacion',
    label: 'Programación',
    icon: 'M6 2v3m12-3v3M4 9h16M5 5h14a2 2 0 0 1 2 2v13H3V7a2 2 0 0 1 2-2Zm3 8h3v3H8v-3Z',
  },
  {
    module: 'establecimientos',
    to: '/establecimientos',
    label: 'Establecimientos',
    icon: 'M4 21V8l8-5 8 5v13M8 21v-5h8v5M8 10h.01M12 10h.01M16 10h.01',
  },
  {
    module: 'hallazgos',
    to: '/hallazgos',
    label: 'Hallazgos',
    icon: 'M12 3 3 7v6c0 5 4 8 9 9s9-3 9-9V7l-9-4Zm-3 9 2 2 4-4',
  },
  {
    module: 'evidencias',
    to: '/evidencias',
    label: 'Evidencias',
    icon: 'M4 5h16v14H4V5Zm3 10 3-3 3 3 2-2 3 3M8 9h.01',
  },
  {
    module: 'correcciones',
    to: '/correcciones',
    label: 'Correcciones',
    icon: 'M4 12a8 8 0 1 0 3-6M4 4v6h6M9 12l2 2 4-5',
  },
  {
    module: 'fichas-bpm',
    to: '/fichas-bpm',
    label: 'Fichas BPM',
    icon: 'M6 3h12a2 2 0 0 1 2 2v16H4V5a2 2 0 0 1 2-2Zm2 5h8M8 12h8M8 16h5',
  },
  {
    module: 'reportes',
    to: '/reportes',
    label: 'Reportes',
    icon: 'M4 20V10h4v10H4Zm6 0V4h4v16h-4Zm6 0v-7h4v7h-4Z',
  },
]

const administrationNavigation: NavigationItem[] = [
  {
    module: 'empresas',
    to: '/empresas',
    label: 'Empresas',
    icon: 'M3 21h18M5 21V5h9v16m0-11h5v11M8 9h3M8 13h3M8 17h3',
  },
  {
    module: 'usuarios',
    to: '/usuarios',
    label: 'Gestión de usuarios',
    icon: 'M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2m7-10a4 4 0 1 0 0-8 4 4 0 0 0 0 8Zm13 10v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75',
  },
  {
    module: 'parametros',
    to: '/parametros',
    label: 'Parámetros',
    icon: 'M4 7h10m4 0h2M4 17h2m4 0h10M14 4v6M7 14v6',
  },
  {
    module: 'auditoria',
    to: '/auditoria',
    label: 'Auditoría',
    icon: 'M5 3h14v18H5V3Zm4 5h6M9 12h6M9 16h4',
  },
]

const accountNavigation: NavigationItem[] = [
  {
    module: 'notificaciones',
    to: '/notificaciones',
    label: 'Notificaciones y sincronización',
    icon: 'M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9M10 21h4',
  },
  {
    module: 'perfil',
    to: '/perfil',
    label: 'Mi perfil',
    icon: 'M20 21a8 8 0 0 0-16 0m8-10a4 4 0 1 0 0-8 4 4 0 0 0 0 8Z',
  },
]

interface SidebarProps {
  mobile?: boolean
  currentModule?: AppModule
  onNavigate?: () => void
}

export function Sidebar({ mobile = false, currentModule = 'resumen', onNavigate }: SidebarProps) {
  const { roles } = useAuth()
  const visibleNavigation = navigation.filter((item) => canAccessModule(roles, item.module))
  const visibleAdministration = administrationNavigation.filter((item) =>
    canAccessModule(roles, item.module),
  )
  const visibleAccount = accountNavigation.filter((item) => canAccessModule(roles, item.module))
  const linkClass = (isActive: boolean) =>
    `flex min-h-11 items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition-colors ${isActive ? 'bg-white/12 text-white' : 'text-slate-300 hover:bg-white/8 hover:text-white'}`

  const renderLink = (item: NavigationItem) => (
    <li key={item.module}>
      <a
        href={moduleHref(item.module)}
        className={linkClass(currentModule === item.module)}
        aria-current={currentModule === item.module ? 'page' : undefined}
        onClick={onNavigate}
      >
        <svg
          viewBox="0 0 24 24"
          className="size-5 shrink-0"
          fill="none"
          stroke="currentColor"
          aria-hidden="true"
        >
          <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="1.7" d={item.icon} />
        </svg>
        {item.label}
      </a>
    </li>
  )

  return (
    <div className="relative flex h-full flex-col overflow-hidden bg-[linear-gradient(165deg,#043a2e_0%,#0A4D3C_58%,#063e31_100%)] text-white">
      <span
        className="pointer-events-none absolute -right-36 bottom-24 z-0 size-80 rounded-full border-[4rem] border-emerald-300/20 opacity-75 blur-md"
        aria-hidden="true"
      />
      <span
        className="pointer-events-none absolute -bottom-40 -left-40 z-0 size-96 rounded-full border-[5rem] border-emerald-400/16 opacity-70 blur-lg"
        aria-hidden="true"
      />
      <div className="relative z-10 flex h-20 items-center px-5">
        <img
          src="/assets/logo-white.png"
          alt="SIGERSA"
          className="h-9 w-auto max-w-[11rem] object-contain object-left"
        />
      </div>
      <nav
        className="sigersa-scrollbar relative z-10 flex-1 overflow-y-auto px-3 py-5"
        aria-label={mobile ? 'Navegación móvil' : 'Navegación principal'}
      >
        <p className="px-3 pb-2 text-[0.65rem] font-bold tracking-[0.16em] text-slate-400 uppercase">
          Operaciones
        </p>
        <ul className="space-y-1">{visibleNavigation.map(renderLink)}</ul>
        {visibleAdministration.length > 0 && (
          <>
            <p className="mt-6 px-3 pb-2 text-[0.65rem] font-bold tracking-[0.16em] text-slate-400 uppercase">
              Administración
            </p>
            <ul className="space-y-1">{visibleAdministration.map(renderLink)}</ul>
          </>
        )}
        <p className="mt-6 px-3 pb-2 text-[0.65rem] font-bold tracking-[0.16em] text-slate-400 uppercase">
          Cuenta
        </p>
        <ul className="space-y-1">{visibleAccount.map(renderLink)}</ul>
      </nav>
      <div className="relative z-10 border-t border-white/10 p-4">
        <div className="rounded-xl bg-white/6 p-3">
          <p className="text-xs font-semibold text-emerald-100">Centro de ayuda</p>
          <p className="mt-1 text-xs leading-5 text-slate-400">
            Guías de inspección y soporte técnico.
          </p>
          <a
            href="https://digemaps.gob.do/"
            target="_blank"
            rel="noreferrer"
            className="mt-3 flex min-h-10 items-center justify-center gap-2 rounded-xl border border-white/10 bg-emerald-500/15 px-3 text-xs font-bold text-white transition hover:bg-emerald-500/25"
          >
            Ver recursos <span aria-hidden="true">→</span>
          </a>
        </div>
      </div>
    </div>
  )
}
