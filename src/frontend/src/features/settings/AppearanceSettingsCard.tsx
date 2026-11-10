import { useMutation } from '@tanstack/react-query'
import { Button, Card, Space, Typography, message } from 'antd'
import { CheckOutlined } from '@ant-design/icons'
import { apiClient } from '../../api/client'
import { IfPermission } from '../auth/IfPermission'
import { useTheme } from '../theme/ThemeProvider'
import { THEME_OPTIONS, type ThemeId } from '../theme/themes'

export function AppearanceSettingsCard() {
  const { themeId, applyTheme } = useTheme()

  const saveMutation = useMutation({
    mutationFn: async (theme: ThemeId) =>
      (await apiClient.put<{ theme: string }>('/settings/theme', { theme })).data,
    onSuccess: (result) => {
      applyTheme(result.theme === 'blue' || result.theme === 'dark' ? result.theme : 'teal')
      message.success('Görünüm bu site için kaydedildi.')
    },
    onError: () => {
      message.error('Tema kaydedilemedi.')
    },
  })

  function choose(next: ThemeId) {
    const previous = themeId
    applyTheme(next)
    saveMutation.mutate(next, {
      onError: () => applyTheme(previous),
    })
  }

  return (
    <Card title="Görünüm ve Tema" style={{ maxWidth: 800, marginBottom: 16 }}>
      <Typography.Paragraph type="secondary">
        Seçim hemen uygulanır ve bu sitenin ayar kaydına yazılır. Giriş ekranı, son seçimi hatırlar.
      </Typography.Paragraph>
      <IfPermission code="Settings.General">
      <Space size={12} wrap>
        {THEME_OPTIONS.map((option) => {
          const selected = option.id === themeId
          return (
            <Button
              key={option.id}
              type={selected ? 'primary' : 'default'}
              loading={saveMutation.isPending && saveMutation.variables === option.id}
              onClick={() => choose(option.id)}
              style={{
                height: 'auto',
                padding: '12px 16px',
                textAlign: 'left',
                borderRadius: 16,
              }}
            >
              <span style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
                <span
                  style={{
                    width: 28,
                    height: 28,
                    borderRadius: 10,
                    background: option.swatch,
                    flexShrink: 0,
                  }}
                />
                <span>
                  <span style={{ display: 'block', fontWeight: 700 }}>
                    {option.title} {selected ? <CheckOutlined /> : null}
                  </span>
                  <span style={{ display: 'block', fontWeight: 400, opacity: 0.75 }}>
                    {option.description}
                  </span>
                </span>
              </span>
            </Button>
          )
        })}
      </Space>
      </IfPermission>
    </Card>
  )
}
