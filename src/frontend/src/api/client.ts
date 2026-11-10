import axios from 'axios'
import { notifyError } from './notifier'
import type { ProblemDetails } from '../types/api'
import { clearSession, readSession, readToken, TENANT_OVERRIDE_KEY } from '../features/auth/authStorage'

// Tek axios instance: tum istekler /api/v1 on ekiyle Vite proxy uzerinden API'ye gider.
export const apiClient = axios.create({
  baseURL: '/api/v1',
  timeout: 60_000,
})

interface HandledAxiosError extends Error {
  handled?: boolean
}

apiClient.interceptors.request.use((config) => {
  const token = readToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }

  const session = readSession()
  if (session?.role === 'SuperAdmin') {
    const tenantId = localStorage.getItem(TENANT_OVERRIDE_KEY)
    if (tenantId) {
      config.headers['X-Tenant-Id'] = tenantId
    }
  }

  return config
})

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error?.response?.status === 401 && !window.location.pathname.startsWith('/login')) {
      clearSession()
      window.location.assign('/login')
      return Promise.reject(error)
    }

    const problem = error?.response?.data as ProblemDetails | undefined

    // Faz 4 GlobalExceptionHandler -> RFC 7807 ProblemDetails doner.
    if (problem && typeof problem === 'object' && 'title' in problem && 'status' in problem) {
      const fieldErrors = problem.errors
        ? Object.entries(problem.errors)
            .map(([field, messages]) => `${field}: ${messages.join(', ')}`)
            .join(' | ')
        : undefined

      notifyError(problem.title ?? 'Hata', fieldErrors ?? problem.detail, problem.traceId)

      const handled = new Error(problem.detail ?? problem.title) as HandledAxiosError
      handled.handled = true
      return Promise.reject(handled)
    }

    if (!error.response) {
      notifyError('Baglanti hatasi', 'API sunucusuna ulasilamadi (localhost:5199 kontrol edin).')
      const handled = new Error('Network error') as HandledAxiosError
      handled.handled = true
      return Promise.reject(handled)
    }

    return Promise.reject(error)
  },
)