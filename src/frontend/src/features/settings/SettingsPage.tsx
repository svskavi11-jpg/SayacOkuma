import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert,
  Button,
  Card,
  Col,
  Divider,
  Form,
  Input,
  InputNumber,
  Row,
  Select,
  Typography,
  message,
} from 'antd'
import { ApiOutlined } from '@ant-design/icons'
import { apiClient } from '../../api/client'
import type { AppSettingsDTO, MbusConnectionTestResult } from '../../types/api'
import { NoticeSettingsCard } from './NoticeSettingsCard'
import { InterestSettingsCard } from './InterestSettingsCard'

/** Ayarlar sayfası: şirket/bina adı ve 30/70 oranları. Toplam tam %100 olmalıdır. */
export function SettingsPage() {
  const queryClient = useQueryClient()
  const [form] = Form.useForm<AppSettingsDTO>()

  const settingsQuery = useQuery({
    queryKey: ['settings'],
    queryFn: async () => (await apiClient.get<AppSettingsDTO>('/settings')).data,
  })

  // Hook'lar koşulsuz çağrılmalı (rules of hooks).
  const fixed = Form.useWatch('fixedCostRatio', form)
  const consumption = Form.useWatch('consumptionRatio', form)
  const meterConnectionType = Form.useWatch('meterConnectionType', form)

  const saveMutation = useMutation({
    mutationFn: async (values: AppSettingsDTO) =>
      (await apiClient.put<AppSettingsDTO>('/settings', values)).data,
    onSuccess: (saved) => {
      void queryClient.invalidateQueries({ queryKey: ['settings'] })
      void queryClient.invalidateQueries({ queryKey: ['dashboard-summary'] })
      message.success(
        'Ayarlar kaydedildi. Hesaplama motoru, PDF başlıkları ve M-Bus okuma motoru artık yeni değerleri kullanıyor.',
      )
      form.setFieldsValue(saved)
    },
  })

  // "Bağlantıyı Test Et": formdaki GÜNCEL değerleri önce kaydeder (test endpoint'i DB'den okur),
  // sonra Izar Center'a (TCP) / COM portuna (USB) ulaşmayı dener.
  const testMutation = useMutation({
    mutationFn: async (): Promise<MbusConnectionTestResult> => {
      const values = await form.validateFields()
      await apiClient.put('/settings', values as AppSettingsDTO)
      void queryClient.invalidateQueries({ queryKey: ['settings'] })
      return (await apiClient.post<MbusConnectionTestResult>('/settings/test-mbus-connection')).data
    },
    onSuccess: (result) => {
      void queryClient.invalidateQueries({ queryKey: ['settings'] })
      // Sonuç Alert'i testMutation.data üzerinden render edilir (satır içi bildirim).
      void result
    },
  })

  const initial = settingsQuery.data
  const sum =
    (fixed ?? initial?.fixedCostRatio ?? 0) + (consumption ?? initial?.consumptionRatio ?? 0)
  const sumValid = Math.round(sum * 100) === 100

  if (settingsQuery.isLoading || !initial) {
    return <Card loading style={{ maxWidth: 800 }} />
  }

  return (
    <>
    <Card title="Ayarlar" style={{ maxWidth: 800 }}>
      <Typography.Paragraph type="secondary">
        Şirket adı PDF başlıklarında, bina adı tüm ekran ve raporlarda kullanılır.
        Oranlar hesaplama motorunun temelidir — toplam tam %100 olmalıdır.
      </Typography.Paragraph>
      <Form
        form={form}
        layout="vertical"
        initialValues={initial}
        onFinish={(values) => saveMutation.mutate(values as AppSettingsDTO)}
      >
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
          <Col span={12}>
            <Form.Item
              name="fixedCostRatio"
              label="Sabit Gider Oranı"
              rules={[{ required: true, message: 'Sabit gider oranı zorunludur.' }]}
            >
              <InputNumber style={{ width: '100%' }} min={0.01} max={0.99} step={0.01} decimalSeparator="," addonAfter="oran (0,30 = %30)" />
            </Form.Item>
          </Col>
          <Col span={12}>
            <Form.Item
              name="consumptionRatio"
              label="Tüketim Gider Oranı"
              rules={[{ required: true, message: 'Tüketim oranı zorunludur.' }]}
            >
              <InputNumber style={{ width: '100%' }} min={0.01} max={0.99} step={0.01} decimalSeparator="," addonAfter="oran (0,70 = %70)" />
            </Form.Item>
          </Col>
        </Row>

        <Divider orientation="left">M-Bus Donanım Ayarları</Divider>
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
            <Col span={14}>
              <Form.Item
                name="tcpHost"
                label="Izar Center IP Adresi / Host"
                rules={[{ required: true, message: 'IP adresi zorunludur.' }]}
              >
                <Input placeholder="192.168.1.50" />
              </Form.Item>
            </Col>
            <Col span={10}>
              <Form.Item name="tcpPort" label="Port">
                <InputNumber style={{ width: '100%' }} min={1} max={65535} decimalSeparator="," />
              </Form.Item>
            </Col>
          </Row>
        )}
        {meterConnectionType === 'Usb' && (
          <Row gutter={16}>
            <Col span={14}>
              <Form.Item
                name="usbPortName"
                label="COM Portu"
                rules={[{ required: true, message: 'COM portu zorunludur.' }]}
              >
                <Input placeholder="COM3" />
              </Form.Item>
            </Col>
            <Col span={10}>
              <Form.Item name="usbBaudRate" label="Baud Rate">
                <InputNumber style={{ width: '100%' }} min={300} step={300} decimalSeparator="," />
              </Form.Item>
            </Col>
          </Row>
        )}
        <Button
          icon={<ApiOutlined />}
          loading={testMutation.isPending}
          onClick={() => testMutation.mutate()}
          style={{ marginBottom: 24 }}
        >
          Bağlantıyı Test Et
        </Button>
        <Typography.Paragraph>
          Toplam:{' '}
          <Typography.Text type={sumValid ? 'success' : 'danger'} strong>
            %{(sum * 100).toFixed(2)}
          </Typography.Text>{' '}
          {sumValid ? '(geçerli ✔)' : '(toplam %100 olmalıdır!)'}
        </Typography.Paragraph>
        <Button
          type="primary"
          htmlType="submit"
          disabled={!sumValid}
          loading={saveMutation.isPending}
        >
          Kaydet
        </Button>
      </Form>
    </Card>
    <InterestSettingsCard />
    <NoticeSettingsCard />
    </>
  )
}
