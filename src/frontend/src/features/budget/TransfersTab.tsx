import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Button, DatePicker, Form, Input, InputNumber, Popconfirm, Select, Space, Table, message } from 'antd'
import { DeleteOutlined } from '@ant-design/icons'
import type { ColumnsType } from 'antd/es/table'
import dayjs, { type Dayjs } from 'dayjs'
import { apiClient } from '../../api/client'
import { useAuth } from '../auth/AuthContext'
import { formatDate } from '../../utils/dateUtils'
import type { BudgetRealizationReport, BudgetTransferItem } from '../../types/api'

const tl = (v: number) => v.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })

interface TransferForm {
  transferDate: Dayjs
  amount: number
  fromBudgetItemId: string
  toBudgetItemId: string
  note?: string
}

/** Kalemler arasi virman. Kaynak kalan butceyi asamaz. */
export function TransfersTab({ year, locked }: { year: number; locked: boolean }) {
  const { hasPermission } = useAuth()
  const canTransfer = hasPermission('Budget.Transfer')
  const queryClient = useQueryClient()
  const [form] = Form.useForm<TransferForm>()

  const itemsQuery = useQuery({
    queryKey: ['budget-items', year],
    queryFn: async () => (await apiClient.get<BudgetRealizationReport>('/budget/items', { params: { year } })).data,
  })

  const transfersQuery = useQuery({
    queryKey: ['budget-transfers', year],
    queryFn: async () => (await apiClient.get<BudgetTransferItem[]>('/budget/transfers', { params: { year } })).data,
  })

  const options = (itemsQuery.data?.rows ?? []).map((row) => ({ value: row.budgetItemId, label: row.category }))

  const saveMutation = useMutation({
    mutationFn: async (values: TransferForm) =>
      apiClient.post('/budget/transfers', {
        transferDate: values.transferDate.format('YYYY-MM-DD'),
        amount: values.amount,
        fromBudgetItemId: values.fromBudgetItemId,
        toBudgetItemId: values.toBudgetItemId,
        note: values.note?.trim() || null,
      }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['budget-transfers'] })
      void queryClient.invalidateQueries({ queryKey: ['budget-items'] })
      void queryClient.invalidateQueries({ queryKey: ['budget-realization'] })
      form.resetFields()
      message.success('Virman kaydedildi.')
    },
  })

  const deleteMutation = useMutation({
    mutationFn: async (id: string) => apiClient.delete(`/budget/transfers/${id}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['budget-transfers'] })
      void queryClient.invalidateQueries({ queryKey: ['budget-items'] })
      void queryClient.invalidateQueries({ queryKey: ['budget-realization'] })
      message.success('Virman silindi.')
    },
  })

  const columns: ColumnsType<BudgetTransferItem> = [
    { title: 'Tarih', dataIndex: 'transferDate', width: 120, render: (v: string) => formatDate(v) },
    { title: 'Kaynak', dataIndex: 'fromCategory' },
    { title: 'Hedef', dataIndex: 'toCategory' },
    { title: 'Tutar', dataIndex: 'amount', align: 'right', width: 140, render: (v: number) => tl(v) },
    { title: 'Not', dataIndex: 'note', render: (v: string | null) => v || '—' },
    {
      title: '',
      fixed: 'right',
      width: 70,
      render: (_: unknown, row) => (
        canTransfer ? <Popconfirm title="Bu virman silinsin mi?" okText="Sil" cancelText="Vazgeç" onConfirm={() => deleteMutation.mutate(row.id)}>
          <Button size="small" danger icon={<DeleteOutlined />} disabled={locked} />
        </Popconfirm> : null
      ),
    },
  ]

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      {locked && <Alert type="warning" showIcon message="Bu bütçe yılı kilitli. Virman yapılamaz." />}
      {canTransfer && <Form form={form} layout="inline" disabled={locked} onFinish={(values) => saveMutation.mutate(values)}>
        <Form.Item name="transferDate" rules={[{ required: true, message: 'Tarih zorunludur.' }]}>
          <DatePicker format="DD.MM.YYYY" placeholder="Tarih" disabledDate={(d) => d.year() !== year || d.isAfter(dayjs(), 'day')} />
        </Form.Item>
        <Form.Item name="fromBudgetItemId" rules={[{ required: true, message: 'Kaynak zorunludur.' }]}>
          <Select style={{ minWidth: 180 }} placeholder="Kaynak kalem" options={options} />
        </Form.Item>
        <Form.Item name="toBudgetItemId" rules={[{ required: true, message: 'Hedef zorunludur.' }]}>
          <Select style={{ minWidth: 180 }} placeholder="Hedef kalem" options={options} />
        </Form.Item>
        <Form.Item name="amount" rules={[{ required: true, message: 'Tutar zorunludur.' }]}>
          <InputNumber min={0.01} precision={2} decimalSeparator="," addonAfter="TL" placeholder="Tutar" style={{ width: 150 }} />
        </Form.Item>
        <Form.Item name="note">
          <Input maxLength={500} placeholder="Not" style={{ width: 180 }} />
        </Form.Item>
        <Form.Item>
          <Button type="primary" htmlType="submit" loading={saveMutation.isPending}>Aktar</Button>
        </Form.Item>
      </Form>}
      <Table
        scroll={{ x: 'max-content' }}
        rowKey="id"
        size="small"
        loading={transfersQuery.isLoading}
        dataSource={transfersQuery.data ?? []}
        columns={columns}
        pagination={{ pageSize: 12 }}
        locale={{ emptyText: `${year} yılı için virman yok.` }}
      />
    </Space>
  )
}
