import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import axios from 'axios'
import {
  clearSession,
  clearTenantOverride,
  readSession,
  readTenantOverride,
  rememberTenant,
  writeSession,
  writeTenantOverride,
  type AuthSession,
} from './authStorage'

interface LoginResponse {
  token: string
  expiresOn: string
  userId: string
  tenantId: string | null
  role: string
  fullName: string
  isSuperAdmin: boolean
  permissions: string[]
}

interface AccessProfile {
  role: string
  fullName: string
  isSuperAdmin: boolean
  permissions: string[]
}

interface AuthState {
  session: AuthSession | null
  activeTenant: { id: string; name: string } | null
  isAuthenticated: boolean
  login: (email: string, password: string) => Promise<string>
  logout: () => void
  enterTenant: (id: string, name: string) => boolean
  leaveTenant: () => void
  hasPermission: (code: string) => boolean
  hasAnyPermission: (codes: string[]) => boolean
}

const AuthContext = createContext<AuthState | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<AuthSession | null>(() => readSession())
  const [activeTenant, setActiveTenant] = useState(() => {
    const tenant = readTenantOverride()
    rememberTenant(tenant?.id ?? null)
    return tenant
  })

  useEffect(() => {
    if (!session?.token) {
      return
    }

    let cancelled = false
    axios
      .get<AccessProfile>('/api/auth/me', { headers: { Authorization: `Bearer ${session.token}` } })
      .then(({ data }) => {
        if (cancelled) {
          return
        }
        setSession((prev) => {
          if (!prev) {
            return prev
          }
          const next: AuthSession = {
            ...prev,
            role: data.role,
            fullName: data.fullName,
            isSuperAdmin: data.isSuperAdmin === true,
            permissions: data.permissions ?? [],
          }
          writeSession(next)
          return next
        })
      })
      .catch(() => {})

    return () => {
      cancelled = true
    }
  }, [session?.token])

  const value = useMemo<AuthState>(() => ({
    session,
    activeTenant,
    isAuthenticated: session !== null,
    async login(email, password) {
      // Giriş /api/auth altındadır. /api/v1 istemcisi bu adresi yanlış birleştirir.
      const { data } = await axios.post<LoginResponse>('/api/auth/login', { email, password })
      const next: AuthSession = {
        token: data.token,
        expiresOn: data.expiresOn,
        userId: data.userId,
        tenantId: data.tenantId,
        role: data.role,
        fullName: data.fullName,
        isSuperAdmin: data.isSuperAdmin === true,
        permissions: data.permissions ?? [],
      }
      writeSession(next)
      setSession(next)
      if (next.isSuperAdmin || next.role === 'SuperAdmin') {
        const tenant = readTenantOverride()
        rememberTenant(tenant?.id ?? null)
        setActiveTenant(tenant)
      } else {
        clearTenantOverride()
        setActiveTenant(null)
      }
      return next.role
    },
    logout() {
      clearSession()
      setSession(null)
      setActiveTenant(null)
    },
    enterTenant(id, name) {
      if (!writeTenantOverride(id, name)) {
        return false
      }
      setActiveTenant({ id, name })
      setSession((prev) => {
        if (!prev) {
          return prev
        }
        const next = { ...prev, tenantId: id }
        writeSession(next)
        return next
      })
      return true
    },
    leaveTenant() {
      clearTenantOverride()
      setActiveTenant(null)
      setSession((prev) => {
        if (!prev) {
          return prev
        }
        const next = { ...prev, tenantId: null }
        writeSession(next)
        return next
      })
    },
    hasPermission(code: string) {
      if (session?.isSuperAdmin || session?.role === 'SuperAdmin') {
        return true
      }
      return session?.permissions?.includes(code) ?? false
    },
    hasAnyPermission(codes: string[]) {
      return codes.some((code) => {
        if (session?.isSuperAdmin || session?.role === 'SuperAdmin') {
          return true
        }
        return session?.permissions?.includes(code) ?? false
      })
    },
  }), [session, activeTenant])

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth, AuthProvider içinde kullanılmalıdır.')
  }
  return context
}
