import { useEffect, useState, type ReactNode } from 'react'
import { useAuth } from '../../contexts/useAuth'
import { useSyncStatus } from '../../hooks/useSyncStatus'
import { alerts } from '../../lib/alerts'
import type { AppModule } from '../../lib/rbac'
import { Header } from './Header'
import { Sidebar } from './Sidebar'

export function AppLayout({
  children,
  currentModule,
}: {
  children: ReactNode
  currentModule: AppModule
}) {
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false)
  const { identity, roleLabel, signOut } = useAuth()
  const { isOnline, isSyncing, pendingCount } = useSyncStatus()

  useEffect(() => {
    if (!mobileMenuOpen) return

    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setMobileMenuOpen(false)
    }

    document.addEventListener('keydown', closeOnEscape)
    return () => document.removeEventListener('keydown', closeOnEscape)
  }, [mobileMenuOpen])

  async function handleLogout() {
    if (
      !(await alerts.confirm({
        title: '¿Cerrar sesión?',
        text: 'Se cerrará la sesión actual en este dispositivo.',
        confirmText: 'Cerrar sesión',
      }))
    )
      return

    try {
      await signOut()
    } catch (error) {
      await alerts.error(error, 'No se pudo notificar el cierre de sesión')
    } finally {
      window.location.replace('/')
    }
  }

  return (
    <div className="min-h-dvh bg-surface-canvas text-ink-strong">
      <a
        href="#main-content"
        className="fixed top-3 left-3 z-50 -translate-y-20 rounded-lg bg-white px-4 py-2 font-semibold text-brand-700 shadow-lg transition-transform focus:translate-y-0"
      >
        Saltar al contenido
      </a>

      <aside className="fixed inset-y-0 left-0 hidden w-72 lg:block">
        <Sidebar currentModule={currentModule} />
      </aside>

      {mobileMenuOpen && (
        <div className="fixed inset-0 z-40 lg:hidden">
          <button
            type="button"
            className="absolute inset-0 bg-slate-950/55"
            aria-label="Cerrar navegación"
            onClick={() => setMobileMenuOpen(false)}
          />
          <aside id="mobile-navigation" className="relative h-full w-[min(20rem,88vw)] shadow-2xl">
            <Sidebar
              mobile
              currentModule={currentModule}
              onNavigate={() => setMobileMenuOpen(false)}
            />
          </aside>
        </div>
      )}

      <div className="flex min-h-dvh min-w-0 flex-col lg:pl-72">
        <Header
          isOnline={isOnline}
          isSyncing={isSyncing}
          pendingCount={pendingCount}
          identity={identity ?? { name: 'Usuario SIGERSA', email: '', initials: 'US' }}
          roleLabel={roleLabel}
          onOpenMenu={() => setMobileMenuOpen(true)}
          onLogout={() => void handleLogout()}
        />
        <main id="main-content" className="flex-1 bg-surface-canvas px-4 py-6 md:px-6 lg:px-7">
          <div className="mx-auto max-w-[1540px]">{children}</div>
        </main>
      </div>
    </div>
  )
}
