import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from './AuthContext'

export function ProtectedRoute() {
  const { isAuthenticated } = useAuth()
  if (!isAuthenticated) {
    return <Navigate to="/login" replace />
  }
  return <Outlet />
}

export function homePath(role: string | undefined) {
  return role === 'Resident' ? '/resident' : '/'
}

export function StaffRoute() {
  const { session } = useAuth()
  if (session?.role === 'Resident') {
    return <Navigate to="/resident" replace />
  }
  return <Outlet />
}

export function ResidentRoute() {
  const { session } = useAuth()
  if (session?.role !== 'Resident') {
    return <Navigate to="/" replace />
  }
  return <Outlet />
}
