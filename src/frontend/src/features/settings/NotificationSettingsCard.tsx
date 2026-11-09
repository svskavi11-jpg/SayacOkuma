import { useEffect } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Button, Card, Col, Form, Input, InputNumber, Row, Space, Switch, Typography, message } from 'antd'
import { MailOutlined } from '@ant-design/icons'
import { apiClient } from '../../api/client'
import type { NotificationSettingsDTO, NotificationSettingsUpdate } from '../../types/api'

interface NotificationSettingsForm extends NotificationSettingsUpdate {
  smtpPassword?: string
  smsPassword?: string
  whatsAppToken?: string
}

/** SMTP, SMS ve WhatsApp kimlik bilgileri. Şifreler API'den geri okunmaz. */
export function NotificationSettingsCard() {
  const queryClient = useQueryClient()
  const [form] = Form.useForm<NotificationSettingsForm>()

  const query = useQuery({
    queryKey: ['notification-settings'],
    queryFn: async () => (await apiClient.get<NotificationSettingsDTO>('/settings/notifications')).data,
  })

  useEffect(() => {
    if (!query.data) return
    form.setFieldsValue({
      testMode: query.data.testMode,
      publicBaseUrl: query.data.publicBaseUrl,
      smtpHost: query.data.smtpHost,
      smtpPort: query.data.smtpPort || 587,
      smtpUser: query.data.smtpUser,
      smsProvider: query.data.smsProvider || 'Netgsm',
      smsUser: query.data.smsUser,
      smsHeader: query.data.smsHeader,
      whatsAppPhoneNumberId: query.data.whatsAppPhoneNumberId,
      smtpPassword: undefined,
      smsPassword: undefined,
      whatsAppToken: undefined,
    })
  }, [query.data, form])

  const saveMutation = useMutation({
    mutationFn: async (values: NotificationSettingsForm) =>
      (
        await apiClient.put<NotificationSettingsDTO>('/settings/notifications', {
          ...values,
          smtpPassword: values.smtpPassword?.trim() || null,
          smsPassword: values.smsPassword?.trim() || null,
          whatsAppToken: values.whatsAppToken?.trim() || null,
        })
      ).data,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['notification-settings'] })
      message.success('İletişim ayarları kaydedildi.')
    },
  })

  const data = query.data

  return (
    <Card
      title={
        <Space>
          <MailOutlined style={{ color: '#1677ff' }} />
          <span>İletişim ve Bildirim Ayarları</span>
        </Space>
      }
      style={{ maxWidth: 800, marginTop: 16 }}
      loading={query.isLoading}
    >
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        message="Şifre veya token boşsa, ya da test modu açıksa mesaj gerçekten gitmez."
        description="Sistem çökmez; gönderim geçmişine Simüle Edildi yazılır. Dolu şifre alanını boş bırakırsanız kayıtlı sır korunur."
      />
      <Form
        form={form}
        layout="vertical"
        onFinish={(values) => saveMutation.mutate(values)}
        initialValues={{ testMode: false, smtpPort: 587, smsProvider: 'Netgsm' }}
      >
        <Form.Item name="testMode" label="Test modu" valuePropName="checked">
          <Switch />
        </Form.Item>
        <Form.Item
          name="publicBaseUrl"
          label="Genel site adresi (Public Base URL)"
          extra="SMS’teki [PdfLink] bu adrese göre üretilir. Örn: https://yonetim.westalife.com"
        >
          <Input placeholder="https://yonetim.ornek.com" />
        </Form.Item>
        <Typography.Title level={5}>SMTP</Typography.Title>
        {data?.smtpPasswordSet && (
          <Typography.Paragraph type="secondary">Kayıtlı bir SMTP şifresi var.</Typography.Paragraph>
        )}
        <Row gutter={12}>
          <Col span={12}>
            <Form.Item name="smtpHost" label="Sunucu">
              <Input placeholder="smtp.ornek.com" />
            </Form.Item>
          </Col>
          <Col span={12}>
            <Form.Item name="smtpPort" label="Port" rules={[{ required: true, message: 'Port zorunludur.' }]}>
              <InputNumber min={1} max={65535} style={{ width: '100%' }} />
            </Form.Item>
          </Col>
        </Row>
        <Row gutter={12}>
          <Col span={12}>
            <Form.Item name="smtpUser" label="Kullanıcı">
              <Input />
            </Form.Item>
          </Col>
          <Col span={12}>
            <Form.Item name="smtpPassword" label="Şifre">
              <Input.Password placeholder={data?.smtpPasswordSet ? 'Değiştirmek için yazın' : 'Şifre'} />
            </Form.Item>
          </Col>
        </Row>
        <Typography.Title level={5}>SMS API</Typography.Title>
        {data?.smsPasswordSet && (
          <Typography.Paragraph type="secondary">Kayıtlı bir SMS şifresi var.</Typography.Paragraph>
        )}
        <Row gutter={12}>
          <Col span={8}>
            <Form.Item name="smsProvider" label="Sağlayıcı">
              <Input placeholder="Netgsm" />
            </Form.Item>
          </Col>
          <Col span={8}>
            <Form.Item name="smsUser" label="Kullanıcı">
              <Input />
            </Form.Item>
          </Col>
          <Col span={8}>
            <Form.Item name="smsPassword" label="Şifre">
              <Input.Password placeholder={data?.smsPasswordSet ? 'Değiştirmek için yazın' : 'Şifre'} />
            </Form.Item>
          </Col>
        </Row>
        <Form.Item name="smsHeader" label="Başlık">
          <Input maxLength={20} placeholder="WESTALIFE" />
        </Form.Item>
        <Typography.Title level={5}>WhatsApp API</Typography.Title>
        {data?.whatsAppTokenSet && (
          <Typography.Paragraph type="secondary">Kayıtlı bir WhatsApp token var.</Typography.Paragraph>
        )}
        <Row gutter={12}>
          <Col span={12}>
            <Form.Item name="whatsAppToken" label="Token">
              <Input.Password placeholder={data?.whatsAppTokenSet ? 'Değiştirmek için yazın' : 'Token'} />
            </Form.Item>
          </Col>
          <Col span={12}>
            <Form.Item name="whatsAppPhoneNumberId" label="Phone Number Id">
              <Input />
            </Form.Item>
          </Col>
        </Row>
        <Button type="primary" htmlType="submit" loading={saveMutation.isPending}>
          Kaydet
        </Button>
      </Form>
    </Card>
  )
}
