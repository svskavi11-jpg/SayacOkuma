import { useEffect, useState } from 'react'
import { Outlet, useLocation, useNavigate } from 'react-router-dom'
import { App as AntdApp, Button, Drawer, Grid, Layout, Menu, Space, Typography } from 'antd'
import {
  LogoutOutlined,
  DashboardOutlined,
  DollarOutlined,
  HomeOutlined,
  FileTextOutlined,
  BarChartOutlined,
  AccountBookOutlined,
  BankOutlined,
  SettingOutlined,
  MoneyCollectOutlined,
  TeamOutlined,
  MessageOutlined,
  CrownOutlined,
  MenuOutlined,
  SafetyCertificateOutlined,
} from '@ant-design/icons'
import { registerNotifier } from '../api/notifier'
import { PublisherMark } from './PublisherMark'
import { useAuth } from '../features/auth/AuthContext'
import { ChangePasswordModal } from '../features/auth/ChangePasswordModal'

const { Sider, Content, Header } = Layout

const menuItems = [
  { key: '/',          icon: <DashboardOutlined />,    label: 'Panel' },
  { key: '/apartments', icon: <HomeOutlined />,        label: 'Daireler' },
  { key: '/invoices',   icon: <FileTextOutlined />,    label: 'Faturalar' },
  { key: '/payments',   icon: <DollarOutlined />,      label: 'Tahsilat' },
  {
    key: '/aidat',
    icon: <MoneyCollectOutlined />,
    label: 'Aidat Yönetimi',
    children: [
      { key: '/aidat/tariffs',  label: 'Aidat Tarifeleri' },
      { key: '/aidat/invoices', label: 'Aidat Tahakkukları' },
    ],
  },
  { key: '/former-residents', icon: <TeamOutlined />, label: 'Sakin Geçmişi' },
  { key: '/budget',     icon: <AccountBookOutlined />, label: 'Bütçe ve Giderler' },
  { key: '/bank-accounts', icon: <BankOutlined />, label: 'Banka Hesapları' },
  { key: '/reports',    icon: <BarChartOutlined />,    label: 'Raporlar' },
  { key: '/notifications', icon: <MessageOutlined />, label: 'Bildirim Merkezi' },
  { key: '/settings',   icon: <SettingOutlined />,     label: 'Ayarlar' },
  { key: '/roles', icon: <SafetyCertificateOutlined />, label: 'Roller' },
]

const menuPermission: Record<string, string | string[]> = {
  '/apartments': 'Apartments.View',
  '/invoices': 'Invoices.View',
  '/payments': 'Payments.View',
  '/aidat': 'Invoices.View',
  '/former-residents': 'Apartments.View',
  '/budget': 'Budget.View',
  '/bank-accounts': 'Bank.View',
  '/reports': [
    'Reports.GeneralAssembly',
    'Reports.IncomeExpense',
    'Reports.ApartmentTracking',
    'Reports.FuelBreakdown',
    'Reports.BankControl',
    'Reports.GasNotifications',
  ],
  '/notifications': 'Notifications.View',
  '/settings': ['Settings.General', 'Settings.Notification', 'Settings.MBus', 'Settings.ManageUsers'],
  '/roles': 'Roles.View',
}

export function AppLayout() {
  const navigate = useNavigate()
  const location = useLocation()
  const { notification } = AntdApp.useApp()
  const { session, logout, hasPermission, activeTenant, leaveTenant } = useAuth()
  const screens = Grid.useBreakpoint()
  const isNarrow = screens.lg === false
  const [menuOpen, setMenuOpen] = useState(false)
  const [passwordOpen, setPasswordOpen] = useState(false)
  const isResident = session?.role === 'Resident'
  const tenantOverride = session?.role === 'SuperAdmin' || session?.isSuperAdmin ? activeTenant : null
  const residentMenu = [
    { key: '/resident', icon: <HomeOutlined />, label: 'Hesabım' },
  ]
  const staffMenu = menuItems.filter((item) => {
    const code = menuPermission[item.key]
    if (!code) {
      return true
    }
    return Array.isArray(code) ? code.some((item) => hasPermission(item)) : hasPermission(code)
  })
  const items = isResident
    ? residentMenu
    : session?.role === 'SuperAdmin' || session?.isSuperAdmin
      ? [...staffMenu, { key: '/admin', icon: <CrownOutlined />, label: 'Müşteri Yönetimi' }]
      : staffMenu

  // Global notifier kaydi: axios interceptor hatalari buradan AntD notification olarak gosterilir.
  useEffect(() => {
    registerNotifier((args) => notification.error(args))
  }, [notification])

  useEffect(() => {
    setMenuOpen(false)
  }, [location.pathname])

  const menu = (
    <Menu
      theme={isNarrow ? 'light' : 'dark'}
      mode="inline"
      selectedKeys={[location.pathname]}
      items={items}
      onClick={({ key }) => {
        navigate(key)
        setMenuOpen(false)
      }}
    />
  )

  return (
    <Layout style={{ minHeight: '100vh' }}>
      {!isNarrow && (
        <Sider
          theme="dark"
          width={220}
          style={{ position: 'sticky', top: 0, height: '100vh', overflow: 'hidden' }}
        >
          <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>
            <div style={{ padding: 12, flex: '0 0 auto' }}>
              <div style={{ background: '#fff', borderRadius: 12, padding: 8 }}>
                <img
                  src="/brand/vays-logo.png"
                  alt="Vays"
                  style={{ width: '100%', height: 'auto', display: 'block' }}
                />
              </div>
            </div>
            <div style={{ flex: 1, minHeight: 0, overflow: 'auto' }}>{menu}</div>
            <PublisherMark tone="dark" />
          </div>
        </Sider>
      )}
      <Drawer
        title="Menü"
        placement="left"
        open={isNarrow && menuOpen}
        onClose={() => setMenuOpen(false)}
        styles={{ body: { padding: 0, display: 'flex', flexDirection: 'column', overflow: 'hidden' } }}
        width={280}
      >
        <div style={{ padding: 16, flex: '0 0 auto' }}>
          <img
            src="/brand/vays-logo.png"
            alt="Vays"
            style={{ width: '100%', height: 'auto', display: 'block' }}
          />
        </div>
        <div style={{ flex: 1, minHeight: 0, overflow: 'auto' }}>{menu}</div>
        <PublisherMark tone="light" />
      </Drawer>
      <Layout>
        {tenantOverride && (
          <div style={{ background: '#fffbe6', borderBottom: '1px solid #ffe58f', padding: isNarrow ? '8px 12px' : '8px 24px', display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 8, flexWrap: 'wrap' }}>
            <Typography.Text>
              Şu an: <strong>{tenantOverride.name}</strong> sitesindesiniz.
            </Typography.Text>
            <Button
              size="small"
              onClick={() => {
                leaveTenant()
                window.location.reload()
              }}
            >
              Çıkış Yap
            </Button>
          </div>
        )}
        <Header style={{ background: '#fff', borderBottom: '1px solid #eee', paddingInline: isNarrow ? 12 : 24, display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 8 }}>
          <Space>
            {isNarrow && (
              <Button type="text" icon={<MenuOutlined />} aria-label="Menü" onClick={() => setMenuOpen(true)} />
            )}
            {isNarrow && (
              <img src="/favicon-192.png" alt="Vays" width={32} height={32} style={{ display: 'block' }} />
            )}
            {!isNarrow && (
              <Typography.Text strong>{isResident ? 'Sakin Paneli' : 'Yönetim Paneli'}</Typography.Text>
            )}
          </Space>
          <Space>
            <Button type="text" onClick={() => setPasswordOpen(true)} style={{ maxWidth: isNarrow ? 140 : 240, paddingInline: 8 }}>
              <Typography.Text ellipsis>{session?.fullName}</Typography.Text>
            </Button>
            <Button
              icon={<LogoutOutlined />}
              aria-label="Çıkış"
              onClick={() => {
                logout()
                navigate('/login', { replace: true })
              }}
            >
              {isNarrow ? null : 'Çıkış'}
            </Button>
          </Space>
        </Header>
        <Content style={{ margin: isNarrow ? 8 : 16 }}>
          <Outlet />
        </Content>
        <ChangePasswordModal open={passwordOpen} onClose={() => setPasswordOpen(false)} />
      </Layout>
    </Layout>
  )
}