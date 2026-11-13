import { useState } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { Button, Form, Input, Typography, theme } from 'antd'
import { LockOutlined, MailOutlined } from '@ant-design/icons'
import { PublisherMark } from '../../components/PublisherMark'
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
      const response = (err as { response?: { status?: number; data?: { detail?: string } } }).response
      const detail = response?.data?.detail
      setError(response?.status === 401
        ? (detail || 'E-posta veya şifre hatalı.')
        : 'Giriş yapılamadı. Bağlantıyı kontrol edin.')
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
          <div style={{ fontSize: 13, fontWeight: 700, letterSpacing: '0.32em', color: '#fff' }}>VAYS</div>
          <div style={{ marginTop: 10, fontSize: 12, fontWeight: 500, letterSpacing: '0.18em', textTransform: 'uppercase', color: 'rgba(255,255,255,0.62)' }}>
            Veri Apartman Yönetim Sistemi
          </div>
        </div>
        <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', textAlign: 'center' }}>
          <LockMark />
          <p style={{ margin: '28px 0 0', maxWidth: 420, fontSize: 28, fontWeight: 650, lineHeight: 1.3, letterSpacing: '-0.02em', color: '#fff' }}>
            Aidat, Isınma ve Sayaç İşlemleri Tek Panelde
          </p>
        </div>
        <PublisherMark tone="dark" flush />
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
          <img
            src="/brand/vays-logo.png"
            alt="Vays — Veri Apartman Yönetim Sistemi"
            style={{ width: '100%', maxWidth: 340, height: 'auto', display: 'block', margin: '0 auto 28px' }}
          />
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
          <div className="login-publisher-mobile">
            <PublisherMark tone="light" flush />
          </div>
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
