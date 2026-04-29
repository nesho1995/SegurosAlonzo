import { createContext } from 'react'
import type { AuthUser } from '../types/auth'

export type AuthContextValue = {
  user: AuthUser | null
  loading: boolean
  hasPermission: (permission: string) => boolean
  signIn: (username: string, password: string) => Promise<void>
  signOut: () => Promise<void>
  refresh: () => Promise<void>
}

export const AuthContext = createContext<AuthContextValue>({
  user: null,
  loading: true,
  hasPermission: () => false,
  signIn: async () => {},
  signOut: async () => {},
  refresh: async () => {},
})
