import { useEffect } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Button, Card, Col, DatePicker, Form, Row, Space, Typography, message } from 'antd'
import { CalendarOutlined } from '@ant-design/icons'
import dayjs, { type Dayjs } from 'dayjs'
import { apiClient } from '../../api/client'
import { IfPermission } from '../auth/IfPermission'
import type { ManagementPeriodSettingsDTO } from '../../types/api'

interface ManagementPeriodFormValues {
  startDate: Dayjs
  endDate: Dayjs
}

/**
 * Aktif Yönetim Dönemi (Başlangıç ve Bitiş Tarihi) ayar kartı.
 * Tüm denetim ve genel kurul raporları varsayılan olarak bu dinamik tarih aralığını kullanır.
 */
export function ManagementPeriodSettingsCard() {
  const queryClient = useQueryClient()
  const [form] = Form.useForm<ManagementPeriodFormValues>()

  const query = useQuery({
    queryKey: ['management-period-settings'],
    queryFn: async () =>
      (await apiClient.get<ManagementPeriodSettingsDTO>('/settings/management-period')).data,
  })

  useEffect(() => {
    if (query.data) {
      form.setFieldsValue({
        startDate: dayjs(query.data.startDate),
        endDate: dayjs(query.data.endDate),
      })
    }
  }, [query.data, form])

  const saveMutation = useMutation({
    mutationFn: async (values: ManagementPeriodFormValues) =>
      (
        await apiClient.put<ManagementPeriodSettingsDTO>('/settings/management-period', {
          startDate: values.startDate.format('YYYY-MM-DD'),
          endDate: values.endDate.format('YYYY-MM-DD'),
        })
      ).data,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['management-period-settings'] })
      void queryClient.invalidateQueries({ queryKey: ['audit-income-expense'] })
      void queryClient.invalidateQueries({ queryKey: ['general-assembly-report'] })
      message.success('Aktif yönetim dönemi kaydedildi.')
    },
  })

  return (
    <Card
      title={
        <Space>
          <CalendarOutlined style={{ color: '#1677ff' }} />
          <span>Aktif Yönetim Dönemi</span>
        </Space>
      }
      style={{ maxWidth: 800, marginTop: 16 }}
      loading={query.isLoading}
    >
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        message="Raporlar ve Genel Kurul / Denetim Raporu için varsayılan dönem aralığı"
        description="Site yönetim döneminiz takvim yılından farklıysa (örneğin 01.06.2026 – 31.05.2027), başlangıç ve bitiş tarihlerini buradan tanımlayabilirsiniz. Tüm denetim raporları ve dinamik aylık matris tabloları bu aralığı baz alır."
      />
      <Form form={form} layout="vertical" onFinish={(values) => saveMutation.mutate(values)}>
        <Row gutter={16}>
          <Col xs={24} sm={12}>
            <Form.Item
              name="startDate"
              label="Dönem Başlangıç Tarihi"
              rules={[{ required: true, message: 'Başlangıç tarihi zorunludur.' }]}
            >
              <DatePicker format="DD.MM.YYYY" style={{ width: '100%' }} allowClear={false} />
            </Form.Item>
          </Col>
          <Col xs={24} sm={12}>
            <Form.Item
              name="endDate"
              label="Dönem Bitiş Tarihi"
              rules={[{ required: true, message: 'Bitiş tarihi zorunludur.' }]}
            >
              <DatePicker format="DD.MM.YYYY" style={{ width: '100%' }} allowClear={false} />
            </Form.Item>
          </Col>
        </Row>
        <Typography.Paragraph type="secondary" style={{ marginBottom: 12 }}>
          İpucu: Seçilen dönem içindeki aylar Genel Kurul Matris Raporu’nda otomatik olarak dinamik sütunlara dönüştürülür.
        </Typography.Paragraph>
        <IfPermission code="Settings.General">
        <Button type="primary" htmlType="submit" loading={saveMutation.isPending}>
          Yönetim Dönemini Kaydet
        </Button>
        </IfPermission>
      </Form>
    </Card>
  )
}

