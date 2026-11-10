import { useEffect } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Button, Card, Form, Switch, Typography, message } from 'antd'
import { apiClient } from '../../api/client'
import { IfPermission } from '../auth/IfPermission'
import type { BudgetSettingsDTO } from '../../types/api'

/** Faz 6.10: Bütçe takibi anahtarı. Kapalıyken giderler serbest tür ile girilir. */
export function BudgetSettingsCard() {
  const queryClient = useQueryClient()
  const [form] = Form.useForm<BudgetSettingsDTO>()

  const query = useQuery({
    queryKey: ['budget-settings'],
    queryFn: async () => (await apiClient.get<BudgetSettingsDTO>('/settings/budget')).data,
  })

  useEffect(() => {
    if (query.data) form.setFieldsValue(query.data)
  }, [query.data, form])

  const saveMutation = useMutation({
    mutationFn: async (values: BudgetSettingsDTO) =>
      (await apiClient.put<BudgetSettingsDTO>('/settings/budget', values)).data,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['budget-settings'] })
      message.success('Bütçe takip ayarı kaydedildi.')
    },
  })

  return (
    <Card title="Bütçe Takibi" style={{ maxWidth: 800, marginTop: 16 }} loading={query.isLoading}>
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        message="Bütçe kullanmayan siteler bu anahtarı kapalı bırakır."
        description="Kapalıyken Bütçe ve Giderler menüsü durur; sayfada yalnız Gider Girişi (serbest gider türü) görünür. Açıkken gider bir bütçe kalemine bağlanır ve harcama tahmini tutardan düşülerek izlenir."
      />
      <Form form={form} layout="vertical" onFinish={(values) => saveMutation.mutate(values)}>
        <Form.Item name="isBudgetTrackingEnabled" label="Bütçe Takibini Etkinleştir" valuePropName="checked">
          <Switch checkedChildren="Açık" unCheckedChildren="Kapalı" />
        </Form.Item>
        <Typography.Paragraph type="secondary">
          %90 aşım uyarısı kaydı engellemez. Kalan = tahmini − gerçekleşen.
        </Typography.Paragraph>
        <IfPermission code="Settings.General">
        <Button type="primary" htmlType="submit" loading={saveMutation.isPending}>
          Kaydet
        </Button>
        </IfPermission>
      </Form>
    </Card>
  )
}
