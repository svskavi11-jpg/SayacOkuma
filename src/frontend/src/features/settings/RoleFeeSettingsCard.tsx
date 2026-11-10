import { useEffect } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Button, Card, Col, Divider, Form, InputNumber, Radio, Row, Select, Space, Typography, message } from 'antd'
import { CrownOutlined } from '@ant-design/icons'
import { apiClient } from '../../api/client'
import { IfPermission } from '../auth/IfPermission'
import type { RoleFeeSettingsDTO, RolePaymentType, RolePeriodType } from '../../types/api'

interface RoleFeeFormValues {
  managementExpenseBudgetItemId?: string | null
  managerPaymentType: RolePaymentType
  managerExemptionPercent: number
  managerFixedAmount: number
  managerPeriodType: RolePeriodType
  auditorPaymentType: RolePaymentType
  auditorExemptionPercent: number
  auditorFixedAmount: number
  auditorPeriodType: RolePeriodType
}

/**
 * Varsayılan Görev Ücretleri (Yönetici ve Denetçi için Muafiyet Oranı % veya Sabit Tutar / Huzur Hakkı)
 * ve Görev Mahsup Bütçe Kalemi ayar kartı.
 */
export function RoleFeeSettingsCard() {
  const queryClient = useQueryClient()
  const [form] = Form.useForm<RoleFeeFormValues>()
  const managerPaymentType = Form.useWatch('managerPaymentType', form) ?? 'Oran'
  const auditorPaymentType = Form.useWatch('auditorPaymentType', form) ?? 'Oran'
  const selectedBudgetItemId = Form.useWatch('managementExpenseBudgetItemId', form)

  const query = useQuery({
    queryKey: ['role-fee-settings'],
    queryFn: async () => (await apiClient.get<RoleFeeSettingsDTO>('/settings/role-fees')).data,
  })

  useEffect(() => {
    if (query.data) {
      form.setFieldsValue({
        managementExpenseBudgetItemId: query.data.managementExpenseBudgetItemId ?? undefined,
        managerPaymentType: query.data.managerPaymentType,
        managerExemptionPercent: Number((query.data.managerExemptionRate * 100).toFixed(2)),
        managerFixedAmount: query.data.managerFixedAmount,
        managerPeriodType: query.data.managerPeriodType,
        auditorPaymentType: query.data.auditorPaymentType,
        auditorExemptionPercent: Number((query.data.auditorExemptionRate * 100).toFixed(2)),
        auditorFixedAmount: query.data.auditorFixedAmount,
        auditorPeriodType: query.data.auditorPeriodType,
      })
    }
  }, [query.data, form])

  const saveMutation = useMutation({
    mutationFn: async (values: RoleFeeFormValues) =>
      (
        await apiClient.put<RoleFeeSettingsDTO>('/settings/role-fees', {
          managementExpenseBudgetItemId: values.managementExpenseBudgetItemId ?? null,
          managerPaymentType: values.managerPaymentType,
          managerExemptionRate: Number(((values.managerExemptionPercent ?? 0) / 100).toFixed(4)),
          managerFixedAmount: values.managerFixedAmount ?? 0,
          managerPeriodType: values.managerPeriodType ?? 'Aylik',
          auditorPaymentType: values.auditorPaymentType,
          auditorExemptionRate: Number(((values.auditorExemptionPercent ?? 0) / 100).toFixed(4)),
          auditorFixedAmount: values.auditorFixedAmount ?? 0,
          auditorPeriodType: values.auditorPeriodType ?? 'Yillik',
        })
      ).data,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['role-fee-settings'] })
      void queryClient.invalidateQueries({ queryKey: ['budget-realization'] })
      void queryClient.invalidateQueries({ queryKey: ['audit-income-expense'] })
      void queryClient.invalidateQueries({ queryKey: ['audit-apartment-tracking'] })
      void queryClient.invalidateQueries({ queryKey: ['general-assembly-report'] })
      void queryClient.invalidateQueries({ queryKey: ['ledger'] })
      void queryClient.invalidateQueries({ queryKey: ['apartments'] })
      message.success('Görev mahsup bütçe kalemi ve varsayılan görev ücretleri kaydedildi; mevcut mahsuplar senkronize edildi.')
    },
  })

  const budgetOptions = (query.data?.availableBudgetItems ?? []).map((b) => ({
    value: b.id,
    label: `${b.year} — ${b.category} (Tahmini: ${b.estimatedAmount.toLocaleString('tr-TR', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    })} TL)`,
  }))

  return (
    <Card
      title={
        <Space>
          <CrownOutlined style={{ color: '#722ed1' }} />
          <span>Görev Mahsup Bütçe Kalemi & Varsayılan Görev Ücretleri</span>
        </Space>
      }
      style={{ maxWidth: 800, marginTop: 16 }}
      loading={query.isLoading}
    >
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        message="Görev Mahsubu (WaiverOffset) Muhasebe ve Bütçe Bağlantısı"
        description="Yönetici ve Denetçi görev mahsuplarının (Aidat Muafiyeti veya Sabit Huzur Hakkı) İşletme Projesi ve Denetim Raporu'nda hangi bütçe kaleminden düşüleceğini buradan seçebilirsiniz. Kalemi değiştirdiğinizde mevcut dönem mahsupları (Daire 64 dahil) otomatik olarak yeni seçtiğiniz kaleme taşınır."
      />
      {!selectedBudgetItemId && (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 16 }}
          message="Görev Mahsup Bütçe Kalemi henüz seçilmedi"
          description="Muafiyetli veya sabit ücretli görev ataması yapabilmek için lütfen aşağıdaki listeden bir bütçe kalemi (örn. Huzur Hakkı, Yönetim Gideri vb.) seçip kaydedin. Liste boşsa önce Bütçe sayfasından ilgili kalemi oluşturun."
        />
      )}
      <Form form={form} layout="vertical" onFinish={(values) => saveMutation.mutate(values)}>
        <Form.Item
          name="managementExpenseBudgetItemId"
          label="Görev Mahsup Bütçe Kalemi"
          extra="Görev mahsuplarının bütçe gerçekleşme ve genel kurul denetim raporlarında yansıtılacağı bütçe kalemi."
        >
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            placeholder="Bütçe kalemi seçin (örn: Huzur Hakkı, Yönetim Gideri...)"
            options={budgetOptions}
            notFoundContent="Kayıtlı bütçe kalemi bulunamadı. Önce Bütçe sayfasından kalem ekleyin."
          />
        </Form.Item>

        <Divider style={{ margin: '12px 0' }} />

        <Typography.Title level={5} style={{ marginTop: 0 }}>
          Yönetici Varsayılan Ücreti
        </Typography.Title>
        <Row gutter={16}>
          <Col xs={24} sm={10}>
            <Form.Item name="managerPaymentType" label="Ücretlendirme Türü" rules={[{ required: true }]}>
              <Radio.Group optionType="button" buttonStyle="solid">
                <Radio.Button value="Oran">Muafiyet Oranı (%)</Radio.Button>
                <Radio.Button value="SabitTutar">Sabit Tutar (TL)</Radio.Button>
              </Radio.Group>
            </Form.Item>
          </Col>
          {managerPaymentType === 'Oran' ? (
            <Col xs={24} sm={14}>
              <Form.Item
                name="managerExemptionPercent"
                label="Muafiyet Oranı (%)"
                rules={[{ required: true, message: 'Oran zorunludur.' }]}
              >
                <InputNumber
                  min={0}
                  max={100}
                  step={5}
                  precision={2}
                  decimalSeparator=","
                  addonAfter="%"
                  style={{ width: 180 }}
                />
              </Form.Item>
            </Col>
          ) : (
            <>
              <Col xs={24} sm={7}>
                <Form.Item
                  name="managerFixedAmount"
                  label="Sabit Tutar (Huzur Hakkı)"
                  rules={[{ required: true, message: 'Sabit tutar zorunludur.' }]}
                >
                  <InputNumber
                    min={0}
                    step={500}
                    precision={2}
                    decimalSeparator=","
                    addonAfter="TL"
                    style={{ width: '100%' }}
                  />
                </Form.Item>
              </Col>
              <Col xs={24} sm={7}>
                <Form.Item
                  name="managerPeriodType"
                  label="Periyot"
                  rules={[{ required: true, message: 'Periyot zorunludur.' }]}
                >
                  <Select
                    options={[
                      { value: 'Aylik', label: 'Aylık' },
                      { value: 'Yillik', label: 'Yıllık' },
                    ]}
                  />
                </Form.Item>
              </Col>
            </>
          )}
        </Row>

        <Divider style={{ margin: '12px 0' }} />

        <Typography.Title level={5} style={{ marginTop: 0 }}>
          Denetçi Varsayılan Ücreti
        </Typography.Title>
        <Row gutter={16}>
          <Col xs={24} sm={10}>
            <Form.Item name="auditorPaymentType" label="Ücretlendirme Türü" rules={[{ required: true }]}>
              <Radio.Group optionType="button" buttonStyle="solid">
                <Radio.Button value="Oran">Muafiyet Oranı (%)</Radio.Button>
                <Radio.Button value="SabitTutar">Sabit Tutar (TL)</Radio.Button>
              </Radio.Group>
            </Form.Item>
          </Col>
          {auditorPaymentType === 'Oran' ? (
            <Col xs={24} sm={14}>
              <Form.Item
                name="auditorExemptionPercent"
                label="Muafiyet Oranı (%)"
                rules={[{ required: true, message: 'Oran zorunludur.' }]}
              >
                <InputNumber
                  min={0}
                  max={100}
                  step={5}
                  precision={2}
                  decimalSeparator=","
                  addonAfter="%"
                  style={{ width: 180 }}
                />
              </Form.Item>
            </Col>
          ) : (
            <>
              <Col xs={24} sm={7}>
                <Form.Item
                  name="auditorFixedAmount"
                  label="Sabit Tutar (Huzur Hakkı)"
                  rules={[{ required: true, message: 'Sabit tutar zorunludur.' }]}
                >
                  <InputNumber
                    min={0}
                    step={500}
                    precision={2}
                    decimalSeparator=","
                    addonAfter="TL"
                    style={{ width: '100%' }}
                  />
                </Form.Item>
              </Col>
              <Col xs={24} sm={7}>
                <Form.Item
                  name="auditorPeriodType"
                  label="Periyot"
                  rules={[{ required: true, message: 'Periyot zorunludur.' }]}
                >
                  <Select
                    options={[
                      { value: 'Aylik', label: 'Aylık' },
                      { value: 'Yillik', label: 'Yıllık' },
                    ]}
                  />
                </Form.Item>
              </Col>
            </>
          )}
        </Row>

        <IfPermission code="Settings.General">
        <Button type="primary" htmlType="submit" loading={saveMutation.isPending}>
          Görev Ücretlerini ve Bütçe Kalemini Kaydet
        </Button>
        </IfPermission>
      </Form>
    </Card>
  )
}
