import { useState } from 'react'
import { AppLayout } from './components/layout/AppLayout'
import { AllItemsAdmin } from './features/admin/AllItemsAdmin'
import { LoginPage } from './features/auth/LoginPage'
import { DynamicInspectionForm } from './features/inspection/DynamicInspectionForm'
import { useSyncStatus } from './hooks/useSyncStatus'
import { clearSession, getSession, logout, type AuthSession } from './lib/api'

type View = 'inspection' | 'admin'

function App() {
  const syncStatus = useSyncStatus()
  const [session, setSession] = useState<AuthSession | null>(() => getSession())
  const [view, setView] = useState<View>('inspection')

  if (!session) return <LoginPage onAuthenticated={setSession} />

  const isAdministrator = session.roles.includes('ADMINISTRADOR')
  return (
    <AppLayout {...syncStatus}>
      <div className="mb-6 flex flex-wrap items-center justify-between gap-3 rounded-xl bg-white p-3 shadow-card">
        <nav className="flex gap-2" aria-label="Módulos de Fase 1">
          <button
            type="button"
            onClick={() => setView('inspection')}
            aria-current={view === 'inspection' ? 'page' : undefined}
            className={`min-h-11 rounded-xl px-4 font-bold ${view === 'inspection' ? 'bg-brand-700 text-white' : 'text-ink-body hover:bg-surface-muted'}`}
          >
            Ficha dinámica
          </button>
          {isAdministrator && (
            <button
              type="button"
              onClick={() => setView('admin')}
              aria-current={view === 'admin' ? 'page' : undefined}
              className={`min-h-11 rounded-xl px-4 font-bold ${view === 'admin' ? 'bg-brand-700 text-white' : 'text-ink-body hover:bg-surface-muted'}`}
            >
              Administrar ficha
            </button>
          )}
        </nav>
        <button
          type="button"
          onClick={async () => {
            try {
              await logout()
            } finally {
              clearSession()
              setSession(null)
            }
          }}
          className="min-h-11 rounded-xl border border-slate-300 px-4 font-bold text-ink-body"
        >
          Cerrar sesión
        </button>
      </div>
      {view === 'inspection' ? <DynamicInspectionForm /> : <AllItemsAdmin />}
    </AppLayout>
  )
}

export default App
