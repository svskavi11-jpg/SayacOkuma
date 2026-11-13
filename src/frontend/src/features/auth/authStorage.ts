const TOKEN_KEY = 'westalife.token'
const SESSION_KEY = 'westalife.session'
/** SuperAdmin site seçimi. Interceptor bu kimliği X-Tenant-Id olarak gönderir. */
export const TENANT_OVERRIDE_KEY = 'westalife.tenantId'
const TENANT_OVERRIDE_NAME_KEY = 'westalife.tenantName'
const TENANT_ID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

let memoryTenantId: string | null = null

export function isTenantId(value: string | null | undefined): value is string {
  return typeof value === 'string' && TENANT_ID_PATTERN.test(value)
}

export function rememberTenant(id: string | null) {
  memoryTenantId = isTenantId(id) ? id : null
}

/** Bellekteki seçim, yoksa localStorage. İstek anında okunur. */
export function currentTenantId(): string | null {
  if (isTenantId(memoryTenantId)) {
    return memoryTenantId
  }
  const stored = localStorage.getItem(TENANT_OVERRIDE_KEY)
  return isTenantId(stored) ? stored : null
}

export interface AuthSession {
  token: string
  expiresOn: string
  userId: string
  tenantId: string | null
  role: string
  fullName: string
  isSuperAdmin: boolean
  permissions: string[]
}

export function readSession(): AuthSession | null {
  const token = localStorage.getItem(TOKEN_KEY)
  const raw = localStorage.getItem(SESSION_KEY)
  if (!token || !raw) {
    return null
  }

  try {
    const session = JSON.parse(raw) as AuthSession
    if (!session?.token || session.token !== token) {
      return null
    }

    if (session.expiresOn && Date.parse(session.expiresOn) <= Date.now()) {
      clearSession()
      return null
    }

    return {
      ...session,
      isSuperAdmin: session.isSuperAdmin === true,
      permissions: Array.isArray(session.permissions) ? session.permissions : [],
    }
  } catch {
    return null
  }
}

export function writeSession(session: AuthSession) {
  localStorage.setItem(TOKEN_KEY, session.token)
  localStorage.setItem(SESSION_KEY, JSON.stringify(session))
}

export function clearSession() {
  localStorage.removeItem(TOKEN_KEY)
  localStorage.removeItem(SESSION_KEY)
  clearTenantOverride()
}

export function readTenantOverride(): { id: string; name: string } | null {
  const id = currentTenantId()
  if (!id) {
    return null
  }
  return { id, name: localStorage.getItem(TENANT_OVERRIDE_NAME_KEY) ?? 'Seçili site' }
}

export function writeTenantOverride(id: string, name: string) {
  if (!isTenantId(id)) {
    return false
  }
  localStorage.setItem(TENANT_OVERRIDE_KEY, id)
  localStorage.setItem(TENANT_OVERRIDE_NAME_KEY, name)
  rememberTenant(id)
  return true
}

export function clearTenantOverride() {
  localStorage.removeItem(TENANT_OVERRIDE_KEY)
  localStorage.removeItem(TENANT_OVERRIDE_NAME_KEY)
  rememberTenant(null)
}

export function readToken() {
  return localStorage.getItem(TOKEN_KEY)
}
