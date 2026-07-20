import { createContext, useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import { clearAuth, getAuth, setAuth, subscribeAuth } from '../api/authStore'
import { auth as authApi } from '../api/endpoints'
import type { Credentials, TokenPair } from '../api/types'

interface AuthContextValue {
  user: { id: string; email: string } | null
  register: (credentials: Credentials) => Promise<void>
  login: (credentials: Credentials) => Promise<void>
  logout: () => Promise<void>
}

export const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [tokens, setTokens] = useState<TokenPair | null>(getAuth)
  const queryClient = useQueryClient()

  useEffect(() => subscribeAuth(() => setTokens(getAuth())), [])

  const applyTokens = useCallback((next: TokenPair) => {
    setAuth(next)
  }, [])

  const register = useCallback(
    async (credentials: Credentials) => applyTokens(await authApi.register(credentials)),
    [applyTokens],
  )

  const login = useCallback(
    async (credentials: Credentials) => applyTokens(await authApi.login(credentials)),
    [applyTokens],
  )

  const logout = useCallback(async () => {
    const current = getAuth()
    clearAuth()
    queryClient.clear()
    if (current) {
      try {
        await authApi.logout(current.refreshToken)
      } catch {
        // The session is already gone locally; a failed server revoke is not worth surfacing.
      }
    }
  }, [queryClient])

  const value = useMemo<AuthContextValue>(
    () => ({
      user: tokens ? { id: tokens.userId, email: tokens.email } : null,
      register,
      login,
      logout,
    }),
    [tokens, register, login, logout],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}
