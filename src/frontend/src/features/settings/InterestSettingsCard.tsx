import { useEffect } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert,
  Button,
  Card,
  Form,
  InputNumber,
  Switch,
  Typography,
  message,
} from 'antd'
import { apiClient } from '../../api/client'
import type { InterestSettingsDTO } from '../../types/api'

/**
 * Faz 6.8: Gecikme faizi ayarları kartı.
 * Yıllık basit faiz (günlük işletim): Anapara × Oran/100 × Gün/365
 * Bileşik faiz (faiz üzerine faiz) YOKTUR.
 */
export function InterestSettingsCard() {
  const queryClient = useQueryClient()
  const [form] = Form.useForm<InterestSettingsDTO>()
  const isEnabled = Form.useWatch('isInterestEnabled', form)

  const query = useQuery({
    queryKey: ['interest-settings'],
    queryFn: async () =>
      (await apiClient.get<InterestSettingsDTO>('/settings/interest')).data,
  })

  useEffect(() => {
    if (query.data) form.setFieldsValue(query.data)
  }, [query.data, form])

  const saveMutation = useMutation({
    mutationFn: async (values: InterestSettingsDTO) =>
      (await apiClient.put<InterestSettingsDTO>('/settings/interest', values)).data,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['interest-settings'] })
      message.success('Faiz ayarları kaydedildi.')
    },
  })

  return (
    <Card
      title="Gecikme Faizi Ayarları"
      style={{ maxWidth: 800, marginTop: 16 }}
    >
      <Alert
        type="info"
        showIcon
        message="Basit Faiz Kuralı"
        description={
          <>
            <strong>Anapara × Yıllık Oran/100 × (Geçen Gün / 365)</strong> formülü kullanılır.
            Faiz üzerine faiz (bileşik faiz) <strong>uygulanmaz</strong>.
            Faiz tahakkuku DB'ye yazılmaz; Cari Hesap görünümünde anlık hesaplanır.
          </>
        }
        style={{ marginBottom: 16 }}
      />
      <Form
        form={form}
        layout="vertical"
        onFinish={(values) => saveMutation.mutate(values)}
      >
        <Form.Item
          name="isInterestEnabled"
          label="Gecikme Faizini Etkinleştir"
          valuePropName="checked"
        >
          <Switch checkedChildren="Açık" unCheckedChildren="Kapalı" />
        </Form.Item>

        {isEnabled && (
          <>
            <Form.Item
              name="interestRate"
              label="Yıllık Faiz Oranı (%)"
              rules={[
                { required: true, message: 'Faiz oranı zorunludur.' },
                { type: 'number', min: 0, max: 300, message: '0–300 arasında olmalıdır.' },
              ]}
            >
              <InputNumber
                style={{ width: 200 }}
                min={0}
                max={300}
                step={0.5}
                precision={2}
                decimalSeparator=","
                addonAfter="% yıllık"
              />
            </Form.Item>
            <Typography.Text type="secondary">
              Periyot: <strong>Günlük</strong> (sabit, değiştirilemez)
            </Typography.Text>
          </>
        )}

        <Form.Item name="interestPeriod" hidden>
          <InputNumber />
        </Form.Item>

        <Button
          type="primary"
          htmlType="submit"
          loading={saveMutation.isPending}
          style={{ marginTop: 16 }}
        >
          Kaydet
        </Button>
      </Form>
    </Card>
  )
}
