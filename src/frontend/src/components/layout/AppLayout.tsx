import { useEffect, useState } from 'react'
import { Outlet, useNavigate } from 'react-router-dom'
import { useAuth } from '../../contexts/useAuth'
import { useSyncStatus } from '../../hooks/useSyncStatus'
import { alerts } from '../../lib/alerts'
import { Header } from './Header'
import { Sidebar } from './Sidebar'

export function AppLayout() {
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false)
  const { identity, roleLabel, signOut } = useAuth()
  const { isOnline, isSyncing, pendingCount } = useSyncStatus()
  const navigate = useNavigate()

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
      navigate('/login', { replace: true })
    }
  }

  return (
    <div className="min-h-screen bg-surface-canvas text-ink-strong">
      <a
        href="#main-content"
        className="fixed top-3 left-3 z-50 -translate-y-20 rounded-lg bg-white px-4 py-2 font-semibold text-brand-700 shadow-lg transition-transform focus:translate-y-0"
      >
        Saltar al contenido
      </a>

      <aside className="fixed inset-y-0 left-0 hidden w-64 lg:block">
        <Sidebar />
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
            <Sidebar mobile onNavigate={() => setMobileMenuOpen(false)} />
          </aside>
        </div>
      )}

      <div className="lg:pl-64">
        <Header
          isOnline={isOnline}
          isSyncing={isSyncing}
          pendingCount={pendingCount}
          identity={identity ?? { name: 'Usuario SIGERSA', email: '', initials: 'US' }}
          roleLabel={roleLabel}
          onOpenMenu={() => setMobileMenuOpen(true)}
          onLogout={() => void handleLogout()}
        />
        <main id="main-content" className="px-4 py-6 md:px-6 lg:px-8 lg:py-8">
          <div className="mx-auto max-w-7xl">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  )
}
