import { useEffect, type ReactNode } from 'react'
import { AccessDenied } from './components/layout/AccessDenied'
import { AppLayout } from './components/layout/AppLayout'
import { RequireAuthentication, RequireRoles } from './components/routing/ProtectedRoute'
import { AuthProvider } from './contexts/AuthContext'
import { useAuth } from './contexts/useAuth'
import { UsersManagement } from './features/admin/UsersManagement'
import { LoginPage } from './features/auth/LoginPage'
import { PasswordRecoveryPage } from './features/auth/PasswordRecoveryPage'
import {
  AlertsPage,
  AuditPage,
  CasesPage,
  CompaniesPage,
  CorrectionsPage,
  EstablishmentsPage,
  EvaluationsPage,
  EvidencePage,
  FichasPage,
  FindingsPage,
  NotificationsPage,
  OverviewPage,
  ParametersPage,
  ProfilePage,
  ReportsPage,
  RequestsPage,
  SchedulingPage,
} from './features/operations/ModuleViews'
import { loginHref, moduleHref, readModuleFromLocation, recoveryHref } from './lib/navigation'
import { moduleRoles, type AppModule } from './lib/rbac'

const modules: Record<AppModule, ReactNode> = {
  resumen: <OverviewPage />,
  empresas: <CompaniesPage />,
  evaluaciones: <EvaluationsPage />,
  programacion: <SchedulingPage />,
  establecimientos: <EstablishmentsPage />,
  parametros: <ParametersPage />,
  solicitudes: <RequestsPage />,
  'alertas-denuncias': <AlertsPage />,
  casos: <CasesPage />,
  'fichas-bpm': <FichasPage />,
  hallazgos: <FindingsPage />,
  evidencias: <EvidencePage />,
  correcciones: <CorrectionsPage />,
  reportes: <ReportsPage />,
  usuarios: <UsersManagement />,
  auditoria: <AuditPage />,
  perfil: <ProfilePage />,
  notificaciones: <NotificationsPage />,
}

function Redirect({ href }: { href: string }) {
  useEffect(() => window.location.replace(href), [href])
  return (
    <main className="grid min-h-dvh place-items-center bg-surface-canvas text-sm text-ink-muted">
      Cargando SIGERSA…
    </main>
  )
}

function LoginEntry() {
  const { session, authenticate } = useAuth()
  if (session) return <Redirect href={moduleHref('resumen')} />
  return (
    <LoginPage
      onAuthenticated={(value) => {
        authenticate(value)
        window.location.assign(moduleHref('resumen'))
      }}
      onRecover={() => window.location.assign(recoveryHref)}
    />
  )
}

function RecoveryEntry() {
  const { session } = useAuth()
  return session ? (
    <Redirect href={moduleHref('resumen')} />
  ) : (
    <PasswordRecoveryPage onBack={() => window.location.assign(loginHref)} />
  )
}

function ModuleEntry() {
  const currentModule = readModuleFromLocation()

  if (!currentModule) return <Redirect href={moduleHref('resumen')} />

  return (
    <RequireAuthentication fallback={<Redirect href={loginHref} />}>
      <AppLayout currentModule={currentModule}>
        <RequireRoles allowedRoles={moduleRoles[currentModule]} fallback={<AccessDenied />}>
          {modules[currentModule]}
        </RequireRoles>
      </AppLayout>
    </RequireAuthentication>
  )
}

function App() {
  const entry = document.documentElement.dataset.entry ?? 'login'
  return (
    <AuthProvider>
      {entry === 'module' ? (
        <ModuleEntry />
      ) : entry === 'recovery' ? (
        <RecoveryEntry />
      ) : (
        <LoginEntry />
      )}
    </AuthProvider>
  )
}

export default App
