import { HashRouter, Navigate, Route, Routes, useNavigate } from 'react-router-dom'
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
import { moduleRoles, type AppModule } from './lib/rbac'

function LoginRoute() {
  const { session, authenticate } = useAuth()
  const navigate = useNavigate()
  if (session) return <Navigate to="/resumen" replace />
  return (
    <LoginPage
      onAuthenticated={(value) => {
        authenticate(value)
        navigate('/resumen', { replace: true })
      }}
      onRecover={() => navigate('/recuperar-clave')}
    />
  )
}

function RecoveryRoute() {
  const { session } = useAuth()
  const navigate = useNavigate()
  return session ? (
    <Navigate to="/resumen" replace />
  ) : (
    <PasswordRecoveryPage onBack={() => navigate('/login', { replace: true })} />
  )
}

function RootRoute() {
  const { session } = useAuth()
  return <Navigate to={session ? '/resumen' : '/login'} replace />
}

function ProtectedModule({ module, children }: { module: AppModule; children: React.ReactNode }) {
  return (
    <Route element={<RequireRoles allowedRoles={moduleRoles[module]} />}>
      <Route path={module} element={children} />
    </Route>
  )
}

export function ApplicationRoutes() {
  return (
    <Routes>
      <Route index element={<RootRoute />} />
      <Route path="login" element={<LoginRoute />} />
      <Route path="recuperar-clave" element={<RecoveryRoute />} />
      <Route element={<RequireAuthentication />}>
        <Route element={<AppLayout />}>
          <Route path="acceso-denegado" element={<AccessDenied />} />
          {ProtectedModule({ module: 'resumen', children: <OverviewPage /> })}
          {ProtectedModule({ module: 'empresas', children: <CompaniesPage /> })}
          {ProtectedModule({ module: 'establecimientos', children: <EstablishmentsPage /> })}
          {ProtectedModule({ module: 'parametros', children: <ParametersPage /> })}
          {ProtectedModule({ module: 'solicitudes', children: <RequestsPage /> })}
          {ProtectedModule({ module: 'alertas-denuncias', children: <AlertsPage /> })}
          {ProtectedModule({ module: 'casos', children: <CasesPage /> })}
          {ProtectedModule({ module: 'programacion', children: <SchedulingPage /> })}
          {ProtectedModule({ module: 'evaluaciones', children: <EvaluationsPage /> })}
          {ProtectedModule({ module: 'fichas-bpm', children: <FichasPage /> })}
          {ProtectedModule({ module: 'hallazgos', children: <FindingsPage /> })}
          {ProtectedModule({ module: 'evidencias', children: <EvidencePage /> })}
          {ProtectedModule({ module: 'correcciones', children: <CorrectionsPage /> })}
          {ProtectedModule({ module: 'reportes', children: <ReportsPage /> })}
          {ProtectedModule({ module: 'usuarios', children: <UsersManagement /> })}
          {ProtectedModule({ module: 'auditoria', children: <AuditPage /> })}
          {ProtectedModule({ module: 'perfil', children: <ProfilePage /> })}
          {ProtectedModule({ module: 'notificaciones', children: <NotificationsPage /> })}
          <Route path="*" element={<Navigate to="/resumen" replace />} />
        </Route>
      </Route>
    </Routes>
  )
}

function App() {
  return (
    <HashRouter>
      <AuthProvider>
        <ApplicationRoutes />
      </AuthProvider>
    </HashRouter>
  )
}

export default App
