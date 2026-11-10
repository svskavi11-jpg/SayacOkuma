import type { ReactNode } from 'react'
import { useAuth } from './AuthContext'

export function IfPermission({ code, children }: { code: string; children: ReactNode }) {
  const { hasPermission } = useAuth()
  return hasPermission(code) ? <>{children}</> : null
}
