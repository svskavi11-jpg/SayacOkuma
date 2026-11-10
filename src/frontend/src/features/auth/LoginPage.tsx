import { useState } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { Button, Form, Input, Typography, theme } from 'antd'
import { LockOutlined, MailOutlined } from '@ant-design/icons'
import { useAuth } from './AuthContext'
import { homePath } from './ProtectedRoute'

type LoginForm = {
  email: string
  password: string
}

export function LoginPage() {
  const { login, isAuthenticated, session } = useAuth()
  const navigate = useNavigate()
  const { token } = theme.useToken()
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  if (isAuthenticated) return <Navigate to={homePath(session?.role)} replace />

  async function onFinish(values: LoginForm) {
    setSubmitting(true)
    setError(null)
    try {
      const role = await login(values.email.trim(), values.password)
      navigate(homePath(role), { replace: true })
    } catch (err) {
      const status = (err as { response?: { status?: number } }).response?.status
      setError(status === 401 ? 'E-posta veya şifre hatalı.' : 'Giriş yapılamadı. Bağlantıyı kontrol edin.')
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div style={{ minHeight: '100vh', display: 'flex', background: token.colorBgLayout }}>
      <section
        className="login-hero"
        style={{
          flex: '1 1 46%',
          display: 'flex',
          flexDirection: 'column',
          justifyContent: 'space-between',
          padding: '48px 56px',
          color: '#fff',
          background: `linear-gradient(165deg, ${token.colorPrimary} 0%, #0f172a 100%)`,
        }}
      >
        <div>
          <Typography.Title level={3} style={{ color: '#fff', margin: 0 }}>
            Westalife
          </Typography.Title>
          <Typography.Text style={{ color: 'rgba(255,255,255,0.82)' }}>
            Site yönetim sistemi
          </Typography.Text>
        </div>
        <div style={{ display: 'flex', justifyContent: 'center' }}>
          <LockMark />
        </div>
        <Typography.Paragraph style={{ color: 'rgba(255,255,255,0.78)', margin: 0, maxWidth: 360 }}>
          Aidat, ısınma ve sayaç işlemleri tek panelde.
        </Typography.Paragraph>
      </section>

      <section
        style={{
          flex: '1 1 54%',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          padding: 32,
        }}
      >
        <div style={{ width: '100%', maxWidth: 420 }}>
          <Typography.Title level={2} style={{ marginBottom: 8, fontWeight: 750 }}>
            Sisteme Giriş Yapın
          </Typography.Title>
          <Typography.Paragraph type="secondary" style={{ marginBottom: 28 }}>
            E-posta ve şifrenizle devam edin.
          </Typography.Paragraph>
          <Form<LoginForm> layout="vertical" requiredMark={false} onFinish={onFinish} size="large">
            <Form.Item
              name="email"
              rules={[
                { required: true, message: 'E-posta zorunludur.' },
                { type: 'email', message: 'Geçerli bir e-posta girin.' },
              ]}
            >
              <Input
                prefix={<MailOutlined />}
                placeholder="E-posta"
                autoComplete="username"
                variant="borderless"
                style={{ background: token.colorFillTertiary, borderRadius: 16, padding: '12px 16px' }}
              />
            </Form.Item>
            <Form.Item name="password" rules={[{ required: true, message: 'Şifre zorunludur.' }]}>
              <Input.Password
                prefix={<LockOutlined />}
                placeholder="Şifre"
                autoComplete="current-password"
                variant="borderless"
                style={{ background: token.colorFillTertiary, borderRadius: 16, padding: '12px 16px' }}
              />
            </Form.Item>
            {error && (
              <Typography.Paragraph type="danger" style={{ marginBottom: 16 }}>
                {error}
              </Typography.Paragraph>
            )}
            <Button type="primary" htmlType="submit" block loading={submitting} style={{ height: 48, fontWeight: 700 }}>
              Giriş Yap
            </Button>
          </Form>
        </div>
      </section>
    </div>
  )
}

function LockMark() {
  return (
    <svg width="180" height="180" viewBox="0 0 180 180" aria-hidden="true">
      <rect x="38" y="78" width="104" height="78" rx="22" fill="rgba(255,255,255,0.16)" />
      <path
        d="M62 78 V58 a28 28 0 0 1 56 0 V78"
        fill="none"
        stroke="rgba(255,255,255,0.9)"
        strokeWidth="10"
        strokeLinecap="round"
      />
      <circle cx="90" cy="112" r="8" fill="#fff" />
      <rect x="86" y="116" width="8" height="18" rx="4" fill="#fff" />
    </svg>
  )
}
