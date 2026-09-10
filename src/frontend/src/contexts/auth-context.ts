import { createContext } from 'react'
import type { AuthSession, SessionIdentity } from '../lib/api'
import type { CanonicalRole } from '../lib/rbac'

export interface AuthContextValue {
  session: AuthSession | null
  identity: SessionIdentity | null
  roles: CanonicalRole[]
  roleLabel: string
  isAdministrator: boolean
  authenticate: (session: AuthSession) => void
  signOut: () => Promise<void>
}

export const AuthContext = createContext<AuthContextValue | null>(null)
