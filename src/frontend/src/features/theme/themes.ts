export const THEME_STORAGE_KEY = 'westalife.theme'

export const THEME_IDS = ['teal', 'blue', 'dark'] as const

export type ThemeId = (typeof THEME_IDS)[number]

export const THEME_OPTIONS: { id: ThemeId; title: string; description: string; swatch: string }[] = [
  {
    id: 'teal',
    title: 'Modern Turkuaz',
    description: 'Açık zemin, turkuaz vurgu.',
    swatch: '#0D9488',
  },
  {
    id: 'blue',
    title: 'Kurumsal Mavi',
    description: 'Açık zemin, mavi vurgu.',
    swatch: '#1677FF',
  },
  {
    id: 'dark',
    title: 'Karanlık Mod',
    description: 'Koyu zemin, turkuaz vurgu.',
    swatch: '#134E4A',
  },
]

export function normalizeTheme(value: string | null | undefined): ThemeId {
  if (value === 'blue' || value === 'dark' || value === 'teal') return value
  return 'teal'
}

export function readStoredTheme(): ThemeId {
  try {
    return normalizeTheme(localStorage.getItem(THEME_STORAGE_KEY))
  } catch {
    return 'teal'
  }
}

export function writeStoredTheme(theme: ThemeId) {
  try {
    localStorage.setItem(THEME_STORAGE_KEY, theme)
  } catch {
    // Giriş ekranı önbelleği yazılamazsa tema yine oturum içinde uygulanır.
  }
}
