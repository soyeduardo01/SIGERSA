import type { ReactNode } from 'react'
import { useAuth } from '../../contexts/useAuth'
import { hasAnyRole, type CanonicalRole } from '../../lib/rbac'

interface AccessGuardProps {
  children: ReactNode
  fallback: ReactNode
}

export function RequireAuthentication({ children, fallback }: AccessGuardProps) {
  const { session } = useAuth()
  return session ? children : fallback
}

export function RequireRoles({
  allowedRoles,
  children,
  fallback,
}: AccessGuardProps & { allowedRoles: readonly CanonicalRole[] }) {
  const { roles } = useAuth()
  return hasAnyRole(roles, allowedRoles) ? children : fallback
}
