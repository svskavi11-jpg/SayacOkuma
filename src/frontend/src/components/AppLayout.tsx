import { useEffect } from 'react'
import { Outlet, useLocation, useNavigate } from 'react-router-dom'
import { App as AntdApp, Layout, Menu, Typography } from 'antd'
import {
  DashboardOutlined,
  DollarOutlined,
  HomeOutlined,
  FileTextOutlined,
  BarChartOutlined,
  SettingOutlined,
} from '@ant-design/icons'
import { registerNotifier } from '../api/notifier'

const { Sider, Content, Header } = Layout

const menuItems = [
  { key: '/',          icon: <DashboardOutlined />, label: 'Panel' },
  { key: '/apartments', icon: <HomeOutlined />,      label: 'Daireler' },
  { key: '/invoices',   icon: <FileTextOutlined />,  label: 'Faturalar' },
  { key: '/payments',   icon: <DollarOutlined />,    label: 'Tahsilat' },
  { key: '/reports',    icon: <BarChartOutlined />,  label: 'Raporlar' },
  { key: '/settings',   icon: <SettingOutlined />,   label: 'Ayarlar' },
]

export function AppLayout() {
  const navigate = useNavigate()
  const location = useLocation()
  const { notification } = AntdApp.useApp()

  // Global notifier kaydi: axios interceptor hatalari buradan AntD notification olarak gosterilir.
  useEffect(() => {
    registerNotifier((args) => notification.error(args))
  }, [notification])

  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Sider theme="dark" width={220}>
        <div style={{ padding: '16px', textAlign: 'center' }}>
          <Typography.Title level={5} style={{ color: '#fff', margin: 0 }}>
            Westalife Residence
          </Typography.Title>
          <Typography.Text style={{ color: '#aaa', fontSize: 12 }}>
            Sayac Okuma Sistemi
          </Typography.Text>
        </div>
        <Menu
          theme="dark"
          mode="inline"
          selectedKeys={[location.pathname]}
          items={menuItems}
          onClick={({ key }) => navigate(key)}
        />
      </Sider>
      <Layout>
        <Header style={{ background: '#fff', borderBottom: '1px solid #eee', paddingInline: 24 }}>
          <Typography.Text strong>Yonetim Paneli</Typography.Text>
        </Header>
        <Content style={{ margin: 16 }}>
          <Outlet />
        </Content>
      </Layout>
    </Layout>
  )
}