import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Button, Form, Input, InputNumber, Modal, Popconfirm, Space, Table, Tag, message } from 'antd'
import { DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons'
import type { ColumnsType } from 'antd/es/table'
import { apiClient } from '../../api/client'
import type { BudgetRealizationReport, BudgetRealizationRow } from '../../types/api'

const tl = (v: number) => v.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })
const pct = (v: number) =>
  `${v.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}%`

/** Faz 6.10: Yıllık bütçe kalemleri. Tahmini tutar sabittir. */
export function BudgetItemsTab({ year, locked = false }: { year: number; locked?: boolean }) {
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<BudgetRealizationRow | null>(null)
  const [form] = Form.useForm<{ category: string; estimatedAmount: number }>()

  const query = useQuery({
    queryKey: ['budget-items', year],
    queryFn: async () => (await apiClient.get<BudgetRealizationReport>('/budget/items', { params: { year } })).data,
  })

  const saveMutation = useMutation({
    mutationFn: async (values: { category: string; estimatedAmount: number }) => {
      if (editing) {
        await apiClient.put(`/budget/items/${editing.budgetItemId}`, values)
        return
      }
      await apiClient.post('/budget/items', { year, ...values })
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['budget-items'] })
      void queryClient.invalidateQueries({ queryKey: ['budget-realization'] })
      setOpen(false)
      setEditing(null)
      form.resetFields()
      message.success(editing ? 'Bütçe kalemi güncellendi.' : 'Bütçe kalemi eklendi.')
    },
  })

  const deleteMutation = useMutation({
    mutationFn: async (id: string) => apiClient.delete(`/budget/items/${id}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['budget-items'] })
      void queryClient.invalidateQueries({ queryKey: ['budget-realization'] })
      message.success('Bütçe kalemi silindi.')
    },
  })

  const columns: ColumnsType<BudgetRealizationRow> = [
    { title: 'Kategori', dataIndex: 'category' },
    { title: 'Tahmini', dataIndex: 'estimatedAmount', align: 'right', width: 150, render: (v: number) => tl(v) },
    { title: 'Gerçekleşen', dataIndex: 'actualAmount', align: 'right', width: 150, render: (v: number) => tl(v) },
    {
      title: 'Kalan',
      dataIndex: 'remainingAmount',
      align: 'right',
      width: 150,
      render: (v: number) => <span style={{ color: v < 0 ? '#cf1322' : undefined }}>{tl(v)}</span>,
    },
    {
      title: '%',
      dataIndex: 'utilizationPercent',
      width: 110,
      align: 'right',
      render: (v: number) => <Tag color={v >= 100 ? 'red' : v >= 90 ? 'gold' : 'green'}>{pct(v)}</Tag>,
    },
    {
      title: 'İşlem',
      width: 180,
      render: (_: unknown, row) => (
        <Space size={4}>
          <Button
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
          </Button>
          <Popconfirm
            title="Bu kalem silinsin mi?"
            description="Bağlı gideri olan kalem silinemez."
            okText="Sil"
            cancelText="Vazgeç"
            onConfirm={() => deleteMutation.mutate(row.budgetItemId)}
          >
            <Button size="small" danger icon={<DeleteOutlined />} disabled={locked} />
          </Popconfirm>
        </Space>
      ),
    },
  ]

  return (
    <>
      {locked && <Alert type="warning" showIcon style={{ marginBottom: 12 }} message="Bu bütçe yılı kilitli. Kalem eklenemez veya değiştirilemez." />}
      <Space style={{ marginBottom: 12 }}>
        <Button
          type="primary"
          disabled={locked}
          icon={<PlusOutlined />}
          onClick={() => {
            setEditing(null)
            form.resetFields()
            setOpen(true)
          }}
        >
          Kalem Ekle
        </Button>
      </Space>
      <Table
        rowKey="budgetItemId"
        size="small"
        loading={query.isLoading}
        dataSource={query.data?.rows ?? []}
        columns={columns}
        pagination={{ pageSize: 12 }}
        locale={{ emptyText: `${year} yılı için bütçe kalemi yok.` }}
      />
      <Modal
        title={editing ? 'Bütçe Kalemini Düzenle' : `${year} — Yeni Bütçe Kalemi`}
        open={open}
        confirmLoading={saveMutation.isPending}
        okText="Kaydet"
        cancelText="Vazgeç"
        onCancel={() => {
          setOpen(false)
          setEditing(null)
        }}
        onOk={() =>
          form.validateFields().then((values) => saveMutation.mutate(values))
        }
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="category"
            label="Kategori"
            rules={[{ required: true, message: 'Kategori zorunludur.' }]}
          >
            <Input maxLength={100} placeholder="örn. Asansör bakımı" />
          </Form.Item>
          <Form.Item
            name="estimatedAmount"
            label="Tahmini Tutar"
            rules={[{ required: true, message: 'Tahmini tutar zorunludur.' }]}
          >
            <InputNumber
              style={{ width: '100%' }}
              min={0.01}
              precision={2}
              decimalSeparator=","
              addonAfter="TL"
            />
          </Form.Item>
        </Form>
      </Modal>
    </>
  )
}
