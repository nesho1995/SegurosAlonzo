import { useCallback, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { getCurrentUser, login, logout } from '../api/authApi'
import { ApiError } from '../api/http'
import type { AuthUser } from '../types/auth'
import { AuthContext, type AuthContextValue } from './AuthContext'

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null)
  const [loading, setLoading] = useState(true)

  const refresh = useCallback(async () => {
    setLoading(true)
    try {
      setUser(await getCurrentUser())
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) setUser(null)
      else setUser(null)
    } finally {
      setLoading(false)
    }
  }, [])

  useEffect(() => {
    let alive = true
    getCurrentUser()
      .then((session) => {
        if (alive) setUser(session)
      })
      .catch(() => {
        if (alive) setUser(null)
      })
      .finally(() => {
        if (alive) setLoading(false)
      })

    return () => {
      alive = false
    }
  }, [])

  useEffect(() => {
    const onUnauthorized = () => setUser(null)
    window.addEventListener('app:unauthorized', onUnauthorized)
    return () => window.removeEventListener('app:unauthorized', onUnauthorized)
  }, [])

  const value = useMemo<AuthContextValue>(() => ({
    user,
    loading,
    hasPermission: (permission: string) => user?.permisos.includes(permission) ?? false,
    signIn: async (username: string, password: string) => {
      const session = await login({ username, password })
      if (session) setUser(session)
    },
    signOut: async () => {
      await logout()
      setUser(null)
      window.history.replaceState(null, '', '/login')
    },
    refresh,
  }), [user, loading, refresh])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
