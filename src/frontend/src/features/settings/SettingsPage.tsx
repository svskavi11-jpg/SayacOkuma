import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../auth/AuthContext'
import {
  Alert,
  Button,
  Card,
  Col,
  Form,
  Input,
  InputNumber,
  Row,
  Select,
  Tabs,
  Typography,
  message,
  theme,
} from 'antd'
import { ApiOutlined } from '@ant-design/icons'
import { apiClient } from '../../api/client'
import type { AppSettingsDTO, MbusConnectionTestResult } from '../../types/api'
import { NoticeSettingsCard } from './NoticeSettingsCard'
import { AppearanceSettingsCard } from './AppearanceSettingsCard'
import { InterestSettingsCard } from './InterestSettingsCard'
import { BudgetSettingsCard } from './BudgetSettingsCard'
import { ManagementPeriodSettingsCard } from './ManagementPeriodSettingsCard'
import { RoleFeeSettingsCard } from './RoleFeeSettingsCard'
import { NotificationSettingsCard } from './NotificationSettingsCard'

/** Ayarlar sayfası. Her grup kendi sekmesindedir; site ve M-Bus aynı kaydı paylaşır. */
export function SettingsPage() {
  const { token } = theme.useToken()
  const { hasPermission } = useAuth()
  const canGeneral = hasPermission('Settings.General')
  const canMbus = hasPermission('Settings.MBus')
  const canNotification = hasPermission('Settings.Notification')
  const needsMain = canGeneral || canMbus
  const queryClient = useQueryClient()
  const [siteForm] = Form.useForm<AppSettingsDTO>()
  const [mbusForm] = Form.useForm<AppSettingsDTO>()

  const settingsQuery = useQuery({
    queryKey: ['settings'],
    queryFn: async () => (await apiClient.get<AppSettingsDTO>('/settings')).data,
    enabled: needsMain,
  })

  const fixed = Form.useWatch('fixedCostRatio', siteForm)
  const consumption = Form.useWatch('consumptionRatio', siteForm)
  const meterConnectionType = Form.useWatch('meterConnectionType', mbusForm)

  const saveMutation = useMutation({
    mutationFn: async (values: AppSettingsDTO) =>
      (await apiClient.put<AppSettingsDTO>('/settings', values)).data,
    onSuccess: (saved) => {
      queryClient.setQueryData(['settings'], saved)
      void queryClient.invalidateQueries({ queryKey: ['dashboard-summary'] })
      message.success(
        'Ayarlar kaydedildi. Hesaplama motoru, PDF başlıkları ve M-Bus okuma motoru artık yeni değerleri kullanıyor.',
      )
      siteForm.setFieldsValue(saved)
      mbusForm.setFieldsValue(saved)
    },
  })

  const testMutation = useMutation({
    mutationFn: async (): Promise<MbusConnectionTestResult> => {
      const values = await mbusForm.validateFields()
      const current = queryClient.getQueryData<AppSettingsDTO>(['settings'])
      if (!current) {
        throw new Error('Ayarlar yüklenmedi.')
      }
      const saved = (await apiClient.put<AppSettingsDTO>('/settings', { ...current, ...values })).data
      queryClient.setQueryData(['settings'], saved)
      siteForm.setFieldsValue(saved)
      mbusForm.setFieldsValue(saved)
      return (await apiClient.post<MbusConnectionTestResult>('/settings/test-mbus-connection')).data
    },
  })

  const initial = settingsQuery.data
  const sum =
    (fixed ?? initial?.fixedCostRatio ?? 0) + (consumption ?? initial?.consumptionRatio ?? 0)
  const sumValid = Math.round(sum * 100) === 100

  function saveSection(partial: Partial<AppSettingsDTO>) {
    const current = queryClient.getQueryData<AppSettingsDTO>(['settings']) ?? initial
    if (!current) return
    saveMutation.mutate({ ...current, ...partial })
  }

  if (!needsMain && !canNotification) {
    return (
      <Card style={{ maxWidth: 800 }}>
        <Typography.Paragraph style={{ marginBottom: 0 }}>
          Personel hesapları Roller ekranından açılır.
        </Typography.Paragraph>
      </Card>
    )
  }

  const loadingMain = needsMain && (settingsQuery.isLoading || !initial)

  const items = [
    canGeneral && {
      key: 'gorunum',
      label: 'Görünüm',
      children: <AppearanceSettingsCard />,
    },
    canGeneral && {
      key: 'site',
      label: 'Site',
      children: loadingMain ? (
        <Card loading />
      ) : (
        <Form
          form={siteForm}
          layout="vertical"
          initialValues={initial}
          onFinish={(values) => saveSection(values)}
        >
          <Typography.Paragraph type="secondary">
            Şirket adı PDF başlıklarında, bina adı tüm ekran ve raporlarda kullanılır.
            Oranlar hesaplama motorunun temelidir. Toplam tam %100 olmalıdır.
          </Typography.Paragraph>
          <Form.Item
            name="companyName"
            label="Şirket Adı (PDF başlığı)"
            rules={[{ required: true, message: 'Şirket adı zorunludur.' }]}
          >
            <Input maxLength={100} />
          </Form.Item>
          <Form.Item
            name="buildingName"
            label="Bina Adı"
            rules={[{ required: true, message: 'Bina adı zorunludur.' }]}
          >
            <Input maxLength={200} />
          </Form.Item>
          <Row gutter={16}>
            <Col xs={24} md={12}>
              <Form.Item
                name="fixedCostRatio"
                label="Sabit Gider Oranı"
                rules={[{ required: true, message: 'Sabit gider oranı zorunludur.' }]}
              >
                <InputNumber style={{ width: '100%' }} min={0.01} max={0.99} step={0.01} decimalSeparator="," addonAfter="oran (0,30 = %30)" />
              </Form.Item>
            </Col>
            <Col xs={24} md={12}>
              <Form.Item
                name="consumptionRatio"
                label="Tüketim Gider Oranı"
                rules={[{ required: true, message: 'Tüketim oranı zorunludur.' }]}
              >
                <InputNumber style={{ width: '100%' }} min={0.01} max={0.99} step={0.01} decimalSeparator="," addonAfter="oran (0,70 = %70)" />
              </Form.Item>
            </Col>
          </Row>
          <Typography.Paragraph>
            Toplam:{' '}
            <Typography.Text type={sumValid ? 'success' : 'danger'} strong>
              %{(sum * 100).toFixed(2)}
            </Typography.Text>{' '}
            {sumValid ? '(geçerli)' : '(toplam %100 olmalıdır)'}
          </Typography.Paragraph>
          <Button type="primary" htmlType="submit" disabled={!sumValid} loading={saveMutation.isPending}>
            Kaydet
          </Button>
        </Form>
      ),
    },
    canMbus && {
      key: 'mbus',
      label: 'M-Bus',
      children: loadingMain ? (
        <Card loading />
      ) : (
        <Form
          form={mbusForm}
          layout="vertical"
          initialValues={initial}
          onFinish={(values) => saveSection(values)}
        >
          <Typography.Paragraph type="secondary">
            Sayaç okuma donanımı. Test, formdaki değerleri kaydedip bağlantıyı dener.
          </Typography.Paragraph>
          {testMutation.data && (
            <Alert
              type={testMutation.data.success ? 'success' : 'error'}
              message={testMutation.data.success ? 'Bağlantı Başarılı' : 'Bağlantı Hatası'}
              description={`${testMutation.data.message} (${testMutation.data.durationMs} ms)`}
              showIcon
              style={{ marginBottom: 16 }}
            />
          )}
          <Form.Item name="meterConnectionType" label="Bağlantı Tipi">
            <Select
              options={[
                { value: 'Simulator', label: 'Simulator (test verisi)' },
                { value: 'Tcp', label: 'TCP — Izar Center (Ethernet)' },
                { value: 'Usb', label: 'USB — Optokopf / RS-232 adaptör' },
              ]}
            />
          </Form.Item>
          {meterConnectionType === 'Tcp' && (
            <Row gutter={16}>
              <Col xs={24} md={14}>
                <Form.Item
                  name="tcpHost"
                  label="Izar Center IP Adresi / Host"
                  rules={[{ required: true, message: 'IP adresi zorunludur.' }]}
                >
                  <Input placeholder="192.168.1.50" />
                </Form.Item>
              </Col>
              <Col xs={24} md={10}>
                <Form.Item name="tcpPort" label="Port">
                  <InputNumber style={{ width: '100%' }} min={1} max={65535} decimalSeparator="," />
                </Form.Item>
              </Col>
            </Row>
          )}
          {meterConnectionType === 'Usb' && (
            <Row gutter={16}>
              <Col xs={24} md={14}>
                <Form.Item
                  name="usbPortName"
                  label="COM Portu"
                  rules={[{ required: true, message: 'COM portu zorunludur.' }]}
                >
                  <Input placeholder="COM3" />
                </Form.Item>
              </Col>
              <Col xs={24} md={10}>
                <Form.Item name="usbBaudRate" label="Baud Rate">
                  <InputNumber style={{ width: '100%' }} min={300} step={300} decimalSeparator="," />
                </Form.Item>
              </Col>
            </Row>
          )}
          <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
            <Button
              icon={<ApiOutlined />}
              loading={testMutation.isPending}
              onClick={() => testMutation.mutate()}
            >
              Bağlantıyı Test Et
            </Button>
            <Button type="primary" htmlType="submit" loading={saveMutation.isPending}>
              Kaydet
            </Button>
          </div>
        </Form>
      ),
    },
    canGeneral && { key: 'donem', label: 'Dönem', children: <ManagementPeriodSettingsCard /> },
    canGeneral && { key: 'gorev', label: 'Görev ücretleri', children: <RoleFeeSettingsCard /> },
    canGeneral && { key: 'faiz', label: 'Faiz', children: <InterestSettingsCard /> },
    canGeneral && { key: 'butce', label: 'Bütçe', children: <BudgetSettingsCard /> },
    canGeneral && { key: 'form', label: 'Gider formu', children: <NoticeSettingsCard /> },
    canNotification && { key: 'iletisim', label: 'İletişim', children: <NotificationSettingsCard /> },
  ].filter((item) => item !== false)

  return (
    <div
      className="settings-page"
      style={{
        ['--settings-surface' as string]: token.colorBgContainer,
        ['--settings-radius' as string]: `${token.borderRadius}px`,
        ['--settings-line' as string]: token.colorBorderSecondary,
        ['--settings-accent' as string]: token.colorPrimary,
      }}
    >
      <Tabs
        className="settings-tabs"
        defaultActiveKey={items[0]?.key}
        destroyOnHidden={false}
        items={items}
      />
    </div>
  )
}
