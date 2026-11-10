import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import { App as AntdApp, ConfigProvider, theme } from 'antd'
import trTR from 'antd/es/locale/tr_TR'
import { apiClient } from '../../api/client'
import { useAuth } from '../auth/AuthContext'
import {
  normalizeTheme,
  readStoredTheme,
  writeStoredTheme,
  type ThemeId,
} from './themes'

type ThemeContextValue = {
  themeId: ThemeId
  applyTheme: (themeId: ThemeId) => void
}

const ThemeContext = createContext<ThemeContextValue | null>(null)

export function useTheme() {
  const context = useContext(ThemeContext)
  if (!context) throw new Error('useTheme, ThemeProvider içinde kullanılmalıdır.')
  return context
}

const LIGHT_BG = '#F8FAFC'

function buildTheme(themeId: ThemeId) {
  const dark = themeId === 'dark'
  const colorPrimary = themeId === 'blue' ? '#1677FF' : '#0D9488'
  return {
    algorithm: dark ? theme.darkAlgorithm : theme.defaultAlgorithm,
    token: {
      colorPrimary,
      borderRadius: 16,
      fontFamily: "'Segoe UI', system-ui, sans-serif",
      ...(dark ? {} : { colorBgLayout: LIGHT_BG, colorBgContainer: '#ffffff' }),
    },
    components: {
      Card: {
        borderRadiusLG: 16,
      },
      Layout: dark
        ? {}
        : {
            bodyBg: LIGHT_BG,
            headerBg: '#ffffff',
          },
    },
  }
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const { isAuthenticated } = useAuth()
  const [themeId, setThemeId] = useState<ThemeId>(readStoredTheme)

  const query = useQuery({
    queryKey: ['theme-settings'],
    queryFn: async () => (await apiClient.get<{ theme: string }>('/settings/theme')).data,
    enabled: isAuthenticated,
  })

  useEffect(() => {
    if (!query.data?.theme) return
    const next = normalizeTheme(query.data.theme)
    setThemeId(next)
    writeStoredTheme(next)
  }, [query.data])

  const value = useMemo<ThemeContextValue>(
    () => ({
      themeId,
      applyTheme: (next) => {
        setThemeId(next)
        writeStoredTheme(next)
      },
    }),
    [themeId],
  )

  return (
    <ThemeContext.Provider value={value}>
      <ConfigProvider locale={trTR} theme={buildTheme(themeId)}>
        <AntdApp>{children}</AntdApp>
      </ConfigProvider>
    </ThemeContext.Provider>
  )
}
