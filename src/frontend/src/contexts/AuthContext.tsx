import { useEffect, useMemo, useState, type ReactNode } from 'react'
import {
  clearSession,
  getSession,
  getSessionIdentity,
  logout,
  subscribeSessionChanges,
  type AuthSession,
} from '../lib/api'
import { normalizeRoles, roleNames } from '../lib/rbac'
import { AuthContext, type AuthContextValue } from './auth-context'

interface AuthProviderProps {
  children: ReactNode
  initialSession?: AuthSession | null
}

export function AuthProvider({ children, initialSession }: AuthProviderProps) {
  const [session, setSession] = useState<AuthSession | null>(() =>
    initialSession === undefined ? getSession() : initialSession,
  )

  useEffect(() => subscribeSessionChanges(setSession), [])

  const value = useMemo<AuthContextValue>(() => {
    const roles = normalizeRoles(session?.roles ?? [])
    return {
      session,
      identity: session ? getSessionIdentity(session) : null,
      roles,
      roleLabel: roles.map((role) => roleNames[role]).join(' · ') || 'Sin rol asignado',
      isAdministrator: roles.includes('ADMINISTRADOR'),
      authenticate: setSession,
      signOut: async () => {
        try {
          await logout()
        } finally {
          clearSession()
          setSession(null)
        }
      },
    }
  }, [session])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
