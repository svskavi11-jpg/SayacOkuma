import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert,
  Button,
  DatePicker,
  Form,
  Input,
  InputNumber,
  Modal,
  Popconfirm,
  Radio,
  Select,
  Space,
  Table,
  Tag,
  Tooltip,
  Typography,
  message,
} from 'antd'
import { StopOutlined } from '@ant-design/icons'
import type { ColumnsType } from 'antd/es/table'
import dayjs, { type Dayjs } from 'dayjs'
import { apiClient } from '../../api/client'
import { formatDate } from '../../utils/dateUtils'
import type {
  ApartmentListItem,
  RoleAssignmentListItem,
  RoleFeeSettingsDTO,
  RolePaymentType,
  RolePeriodType,
  RoleType,
} from '../../types/api'

interface RoleForm {
  roleType: RoleType
  personName: string
  paymentType: RolePaymentType
  exemptionPercent: number
  fixedAmount?: number
  periodType: RolePeriodType
  startDate: Dayjs
  endDate?: Dayjs
  boardDecisionDate?: Dayjs
}

const roleLabel: Record<RoleType, string> = {
  Yonetici: 'Yönetici',
  Denetci: 'Denetçi',
}

const periodLabel: Record<RolePeriodType, string> = {
  Aylik: 'Aylık',
  Yillik: 'Yıllık',
}

export function RoleAssignModal({
  apartment,
  onClose,
}: {
  apartment: ApartmentListItem | null
  onClose: () => void
}) {
  const queryClient = useQueryClient()
  const [form] = Form.useForm<RoleForm>()
  const [closeDates, setCloseDates] = useState<Record<string, Dayjs>>({})
  const paymentType = Form.useWatch('paymentType', form) ?? 'Oran'

  const rolesQuery = useQuery({
    queryKey: ['role-assignments', apartment?.id],
    enabled: apartment !== null,
    queryFn: async () =>
      (await apiClient.get<RoleAssignmentListItem[]>(`/apartments/${apartment!.id}/roles`)).data,
  })

  const defaultsQuery = useQuery({
    queryKey: ['role-fee-settings'],
    enabled: apartment !== null,
    queryFn: async () => (await apiClient.get<RoleFeeSettingsDTO>('/settings/role-fees')).data,
  })

  const applyRoleDefaults = (role: RoleType, defaults?: RoleFeeSettingsDTO) => {
    if (!defaults) {
      form.setFieldsValue({
        paymentType: 'Oran',
        exemptionPercent: role === 'Denetci' ? 0 : 100,
        fixedAmount: undefined,
        periodType: role === 'Denetci' ? 'Yillik' : 'Aylik',
      })
      return
    }

    if (role === 'Yonetici') {
      form.setFieldsValue({
        paymentType: defaults.managerPaymentType,
        exemptionPercent: Number((defaults.managerExemptionRate * 100).toFixed(2)),
        fixedAmount: defaults.managerFixedAmount > 0 ? defaults.managerFixedAmount : undefined,
        periodType: defaults.managerPeriodType,
      })
    } else {
      form.setFieldsValue({
        paymentType: defaults.auditorPaymentType,
        exemptionPercent: Number((defaults.auditorExemptionRate * 100).toFixed(2)),
        fixedAmount: defaults.auditorFixedAmount > 0 ? defaults.auditorFixedAmount : undefined,
        periodType: defaults.auditorPeriodType,
      })
    }
  }

  useEffect(() => {
    if (apartment === null) return
    form.setFieldsValue({
      roleType: 'Yonetici',
      personName: apartment.ownerName ?? '',
      startDate: dayjs(),
      endDate: undefined,
      boardDecisionDate: undefined,
    })
    applyRoleDefaults('Yonetici', defaultsQuery.data)
    setCloseDates({})
  }, [apartment, defaultsQuery.data, form])

  const invalidateRelated = async () => {
    await queryClient.invalidateQueries({ queryKey: ['role-assignments', apartment?.id] })
    await queryClient.invalidateQueries({ queryKey: ['ledger'] })
    await queryClient.invalidateQueries({ queryKey: ['apartments'] })
    await queryClient.invalidateQueries({ queryKey: ['audit-apartment-tracking'] })
    await queryClient.invalidateQueries({ queryKey: ['audit-income-expense'] })
    await queryClient.invalidateQueries({ queryKey: ['general-assembly-report'] })
  }

  const assignMutation = useMutation({
    mutationFn: async (values: RoleForm) => {
      const isFixed = values.paymentType === 'SabitTutar'
      const normalizedRate = isFixed
        ? 0
        : Number(((values.exemptionPercent ?? 0) / 100).toFixed(4))

      await apiClient.post(`/apartments/${apartment!.id}/roles`, {
        personName: values.personName,
        roleType: values.roleType,
        startDate: values.startDate.format('YYYY-MM-DD'),
        endDate: values.endDate ? values.endDate.format('YYYY-MM-DD') : null,
        exemptionRate: normalizedRate,
        boardDecisionDate: values.boardDecisionDate
          ? values.boardDecisionDate.format('YYYY-MM-DD')
          : null,
        paymentType: values.paymentType,
        fixedAmount: isFixed ? values.fixedAmount : null,
        periodType: isFixed ? values.periodType : null,
      })
    },
    onSuccess: async () => {
      message.success('Görev kaydedildi ve görev mahsupları senkronize edildi.')
      form.setFieldsValue({ endDate: undefined, boardDecisionDate: undefined })
      await invalidateRelated()
    },
  })

  const closeMutation = useMutation({
    mutationFn: async (payload: { id: string; endDate: string }) => {
      await apiClient.post(`/apartments/roles/${payload.id}/close`, { endDate: payload.endDate })
    },
    onSuccess: async () => {
      message.success('Görev sonlandırıldı.')
      await invalidateRelated()
    },
  })

  const cancelMutation = useMutation({
    mutationFn: async (id: string) => {
      return (
        await apiClient.post<{ cancelledWaiverCount: number }>(`/apartments/roles/${id}/cancel`)
      ).data
    },
    onSuccess: async (res) => {
      message.success(
        res.cancelledWaiverCount > 0
          ? `Görev ataması ve bağlı ${res.cancelledWaiverCount} adet görev mahsubu iptal edildi; daire borcu güncellendi.`
          : 'Görev ataması iptal edildi.',
      )
      await invalidateRelated()
    },
  })

  const columns: ColumnsType<RoleAssignmentListItem> = [
    {
      title: 'Görev',
      dataIndex: 'roleType',
      width: 100,
      render: (v: RoleType, r) => (
        <Typography.Text delete={r.isCancelled} type={r.isCancelled ? 'secondary' : undefined}>
          {roleLabel[v]}
        </Typography.Text>
      ),
    },
    {
      title: 'Ad Soyad',
      dataIndex: 'personName',
      render: (v: string, r) => (
        <Typography.Text delete={r.isCancelled} type={r.isCancelled ? 'secondary' : undefined}>
          {v}
        </Typography.Text>
      ),
    },
    {
      title: 'Muafiyet / Huzur Hakkı',
      key: 'compensation',
      width: 190,
      align: 'right',
      render: (_: unknown, r) => {
        if (r.paymentType === 'SabitTutar' && r.fixedAmount) {
          const pLabel = r.periodType ? periodLabel[r.periodType] : 'Aylık'
          return (
            <Tag color="purple" style={{ marginInlineEnd: 0 }}>
              {r.fixedAmount.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} TL / {pLabel}
            </Tag>
          )
        }
        return (
          <Tag color="blue" style={{ marginInlineEnd: 0 }}>
            %{(r.exemptionRate * 100).toLocaleString('tr-TR', { maximumFractionDigits: 2 })}
          </Tag>
        )
      },
    },
    {
      title: 'Başlangıç',
      dataIndex: 'startDate',
      width: 105,
      render: (v: string) => formatDate(v),
    },
    {
      title: 'Bitiş / Durum',
      dataIndex: 'endDate',
      width: 120,
      render: (v: string | null, r) =>
        r.isCancelled ? (
          <Tooltip title={r.cancelledAt ? `İptal tarihi: ${formatDate(r.cancelledAt)}` : 'İptal edildi'}>
            <Tag color="red">İptal Edildi</Tag>
          </Tooltip>
        ) : v ? (
          formatDate(v)
        ) : (
          <Tag color="green">Devam Ediyor</Tag>
        ),
    },
    {
      title: 'İşlemler',
      key: 'actions',
      width: 310,
      render: (_: unknown, row) => {
        if (row.isCancelled) {
          return <Tag color="red">İptal Edildi</Tag>
        }

        return (
          <Space size={6} wrap>
            {row.isOpen && (
              <>
                <DatePicker
                  size="small"
                  style={{ width: 120 }}
                  format="DD.MM.YYYY"
                  value={closeDates[row.id] ?? dayjs()}
                  onChange={(value) => {
                    if (value) setCloseDates((current) => ({ ...current, [row.id]: value }))
                  }}
                />
                <Popconfirm
                  title="Görevi Sonlandır"
                  description={`Bu görevin bitiş tarihini ${(closeDates[row.id] ?? dayjs()).format('DD.MM.YYYY')} olarak kaydedip sonlandırmak istediğinize emin misiniz?`}
                  okText="Evet, Sonlandır"
                  cancelText="Vazgeç"
                  onConfirm={() =>
                    closeMutation.mutate({
                      id: row.id,
                      endDate: (closeDates[row.id] ?? dayjs()).format('YYYY-MM-DD'),
                    })
                  }
                >
                  <Button size="small" loading={closeMutation.isPending}>
                    Görevi Sonlandır
                  </Button>
                </Popconfirm>
              </>
            )}
            <Popconfirm
              title="Görevi İptal Et"
              description="Bu görev atamasını iptal etmek istediğinize emin misiniz? Üretilmiş görev mahsupları varsa iptal edilecek ve daire borcu geri eklenecektir."
              okText="Evet, İptal Et"
              cancelText="Vazgeç"
              okButtonProps={{ danger: true, loading: cancelMutation.isPending }}
              onConfirm={() => cancelMutation.mutate(row.id)}
            >
              <Button size="small" danger icon={<StopOutlined />} loading={cancelMutation.isPending}>
                İptal Et
              </Button>
            </Popconfirm>
          </Space>
        )
      },
    },
  ]

  const selectedBudgetItem = defaultsQuery.data?.availableBudgetItems?.find(
    (b) => b.id === defaultsQuery.data?.managementExpenseBudgetItemId,
  )

  return (
    <Modal
      title={`Görev Ata — Daire ${apartment?.doorNumber ?? ''}`}
      open={apartment !== null}
      onCancel={onClose}
      footer={null}
      width={940}
      destroyOnClose
    >
      {defaultsQuery.data && !defaultsQuery.data.managementExpenseBudgetItemId ? (
        <Alert
          type="warning"
          showIcon
          style={{ marginBottom: 12 }}
          message="Lütfen Ayarlar sayfasından Görev Mahsup Bütçe Kalemini seçin"
          description="Muafiyet oranı veya sabit huzur hakkı içeren görev ataması yapabilmek için Ayarlar sayfasındaki 'Görev Mahsup Bütçe Kalemi' alanından bir bütçe kalemi seçilmiş olmalıdır."
        />
      ) : selectedBudgetItem ? (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 12 }}
          message={`Görev Mahsup Bütçe Kalemi: ${selectedBudgetItem.year} — ${selectedBudgetItem.category}`}
        />
      ) : null}
      <Form form={form} layout="vertical" onFinish={(values) => assignMutation.mutate(values)}>
        <Space align="start" wrap>
          <Form.Item name="roleType" label="Görev" rules={[{ required: true, message: 'Görev seçin.' }]}>
            <Select
              style={{ width: 140 }}
              options={[
                { value: 'Yonetici', label: 'Yönetici' },
                { value: 'Denetci', label: 'Denetçi' },
              ]}
              onChange={(value: RoleType) => applyRoleDefaults(value, defaultsQuery.data)}
            />
          </Form.Item>
          <Form.Item
            name="personName"
            label="Ad Soyad"
            rules={[{ required: true, message: 'Görevli adı zorunludur.' }]}
          >
            <Input maxLength={150} style={{ width: 210 }} />
          </Form.Item>
          <Form.Item
            name="paymentType"
            label="Ücretlendirme Türü"
            rules={[{ required: true, message: 'Tür seçin.' }]}
          >
            <Radio.Group optionType="button" buttonStyle="solid">
              <Radio.Button value="Oran">Muafiyet Oranı (%)</Radio.Button>
              <Radio.Button value="SabitTutar">Sabit Tutar (Huzur Hakkı)</Radio.Button>
            </Radio.Group>
          </Form.Item>
          {paymentType === 'Oran' ? (
            <Form.Item
              name="exemptionPercent"
              label="Muafiyet Oranı (%)"
              rules={[{ required: true, message: 'Muafiyet oranı zorunludur.' }]}
              tooltip="Örn: Tam muafiyet için 100, yarı muafiyet için 50 girin."
            >
              <InputNumber
                min={0}
                max={100}
                step={5}
                precision={2}
                decimalSeparator=","
                addonAfter="%"
                style={{ width: 135 }}
              />
            </Form.Item>
          ) : (
            <>
              <Form.Item
                name="fixedAmount"
                label="Sabit Tutar (TL)"
                rules={[{ required: true, message: 'Sabit tutar zorunludur.' }]}
                tooltip="Önce Aidat borcundan, yetmezse Isınma borcundan düşülür; artan tutar Cari Hesaba Alacak olarak işlenir."
              >
                <InputNumber
                  min={0.01}
                  step={500}
                  precision={2}
                  decimalSeparator=","
                  addonAfter="TL"
                  style={{ width: 155 }}
                />
              </Form.Item>
              <Form.Item
                name="periodType"
                label="Periyot"
                rules={[{ required: true, message: 'Periyot seçin.' }]}
              >
                <Select
                  style={{ width: 110 }}
                  options={[
                    { value: 'Aylik', label: 'Aylık' },
                    { value: 'Yillik', label: 'Yıllık' },
                  ]}
                />
              </Form.Item>
            </>
          )}
        </Space>
        <Space align="start" wrap>
          <Form.Item
            name="startDate"
            label="Başlangıç"
            rules={[{ required: true, message: 'Başlangıç zorunludur.' }]}
          >
            <DatePicker format="DD.MM.YYYY" />
          </Form.Item>
          <Form.Item name="endDate" label="Bitiş (Boşsa devam ediyor)">
            <DatePicker format="DD.MM.YYYY" />
          </Form.Item>
          <Form.Item name="boardDecisionDate" label="Kurul Karar Tarihi">
            <DatePicker format="DD.MM.YYYY" />
          </Form.Item>
          <Form.Item label=" ">
            <Button type="primary" htmlType="submit" loading={assignMutation.isPending}>
              Görevi Kaydet
            </Button>
          </Form.Item>
        </Space>
      </Form>
      <Table
        rowKey="id"
        size="small"
        loading={rolesQuery.isLoading}
        dataSource={rolesQuery.data ?? []}
        columns={columns}
        pagination={false}
        locale={{ emptyText: 'Görev kaydı yok.' }}
      />
    </Modal>
  )
}
