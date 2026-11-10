import { AppModal } from '../../components/AppModal'
import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Button, Form, Input, InputNumber, Popconfirm, Space, Table, message } from 'antd'
import { DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons'
import type { ColumnsType } from 'antd/es/table'
import { apiClient } from '../../api/client'
import { useAuth } from '../auth/AuthContext'
import type { IncomeItem } from '../../types/api'

const tl = (v: number) => v.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })

/** Tahmini gelir kalemleri. Denge = bu toplam eksi tahmini gider. */
export function IncomeItemsTab({ year, locked }: { year: number; locked: boolean }) {
  const queryClient = useQueryClient()
  const { hasPermission } = useAuth()
  const canCreate = hasPermission('Budget.Create')
  const canEdit = hasPermission('Budget.Edit')
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<IncomeItem | null>(null)
  const [form] = Form.useForm<{ category: string; estimatedAmount: number }>()

  const query = useQuery({
    queryKey: ['income-items', year],
    queryFn: async () => (await apiClient.get<IncomeItem[]>('/budget/income-items', { params: { year } })).data,
  })

  const saveMutation = useMutation({
    mutationFn: async (values: { category: string; estimatedAmount: number }) => {
      if (editing) {
        await apiClient.put(`/budget/income-items/${editing.id}`, values)
        return
      }
      await apiClient.post('/budget/income-items', { year, ...values })
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['income-items'] })
      void queryClient.invalidateQueries({ queryKey: ['budget-realization'] })
      setOpen(false)
      setEditing(null)
      form.resetFields()
      message.success(editing ? 'Gelir kalemi güncellendi.' : 'Gelir kalemi eklendi.')
    },
  })

  const deleteMutation = useMutation({
    mutationFn: async (id: string) => apiClient.delete(`/budget/income-items/${id}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['income-items'] })
      void queryClient.invalidateQueries({ queryKey: ['budget-realization'] })
      message.success('Gelir kalemi silindi.')
    },
  })

  const total = (query.data ?? []).reduce((sum, row) => sum + row.estimatedAmount, 0)

  const columns: ColumnsType<IncomeItem> = [
    { title: 'Gelir Kalemi', dataIndex: 'category' },
    { title: 'Tahmini', dataIndex: 'estimatedAmount', align: 'right', width: 160, render: (v: number) => tl(v) },
    {
      title: 'İşlem',
      fixed: 'right',
      width: 180,
      render: (_: unknown, row) => (
        <Space size={4}>
          {canEdit && <Button
            size="small"
            disabled={locked}
            icon={<EditOutlined />}
            onClick={() => {
              setEditing(row)
              form.setFieldsValue({ category: row.category, estimatedAmount: row.estimatedAmount })
              setOpen(true)
            }}
          >
            Düzenle
          </Button>}
          {canEdit && <Popconfirm title="Bu gelir kalemi silinsin mi?" okText="Sil" cancelText="Vazgeç" onConfirm={() => deleteMutation.mutate(row.id)}>
            <Button size="small" danger icon={<DeleteOutlined />} disabled={locked} />
          </Popconfirm>}
        </Space>
      ),
    },
  ]

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      {locked && <Alert type="warning" showIcon message="Bu bütçe yılı kilitli. Gelir kalemi değiştirilemez." />}
      <Space>
        {canCreate && <Button
          type="primary"
          icon={<PlusOutlined />}
          disabled={locked}
          onClick={() => {
            setEditing(null)
            form.resetFields()
            setOpen(true)
          }}
        >
          Gelir Kalemi Ekle
        </Button>}
        <span>Tahmini gelir toplamı: {tl(total)}</span>
      </Space>
      <Table
        scroll={{ x: 'max-content' }}
        rowKey="id"
        size="small"
        loading={query.isLoading}
        dataSource={query.data ?? []}
        columns={columns}
        pagination={false}
        locale={{ emptyText: `${year} yılı için gelir kalemi yok.` }}
      />
      <AppModal
        title={editing ? 'Gelir Kalemini Düzenle' : `${year} — Yeni Gelir Kalemi`}
        open={open}
        confirmLoading={saveMutation.isPending}
        okText="Kaydet"
        cancelText="Vazgeç"
        onCancel={() => setOpen(false)}
        onOk={() => form.validateFields().then((values) => saveMutation.mutate(values))}
      >
        <Form form={form} layout="vertical">
          <Form.Item name="category" label="Kalem" rules={[{ required: true, message: 'Kalem zorunludur.' }]}>
            <Input maxLength={100} placeholder="Banka faizi, ortak alan kirası" />
          </Form.Item>
          <Form.Item name="estimatedAmount" label="Tahmini tutar" rules={[{ required: true, message: 'Tutar zorunludur.' }]}>
            <InputNumber min={0.01} precision={2} decimalSeparator="," addonAfter="TL" style={{ width: '100%' }} />
          </Form.Item>
        </Form>
      </AppModal>
    </Space>
  )
}
