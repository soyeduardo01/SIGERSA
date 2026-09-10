import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../../contexts/useAuth'
import { hasAnyRole, type CanonicalRole } from '../../lib/rbac'

export function RequireAuthentication() {
  const { session } = useAuth()
  const location = useLocation()

  return session ? <Outlet /> : <Navigate to="/login" replace state={{ from: location }} />
}

export function RequireRoles({ allowedRoles }: { allowedRoles: readonly CanonicalRole[] }) {
  const { roles } = useAuth()
  return hasAnyRole(roles, allowedRoles) ? <Outlet /> : <Navigate to="/acceso-denegado" replace />
}
