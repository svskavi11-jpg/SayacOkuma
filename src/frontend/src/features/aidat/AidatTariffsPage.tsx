import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Button, Card, Form, Input, InputNumber, Modal, Space, Table, Tag, Typography, message } from 'antd'
import { EditOutlined, PlusOutlined } from '@ant-design/icons'
import type { ColumnsType } from 'antd/es/table'
import { apiClient } from '../../api/client'
import type { AidatTariffItem, CreateAidatTariffRequest } from '../../types/api'

const tl = (v: number) => v.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })

/** Faz 6.9: Aidat tarife tanımlama ekranı ("1+1 C" → 32,1 m² → 2.000,00 TL/ay). */
export function AidatTariffsPage() {
  const queryClient = useQueryClient()
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<AidatTariffItem | null>(null)
  const [form] = Form.useForm()

  const tariffsQuery = useQuery({
    queryKey: ['aidat-tariffs'],
    queryFn: async () => (await apiClient.get<AidatTariffItem[]>('/aidat/tariffs')).data,
  })

  const saveMutation = useMutation({
    mutationFn: async (req: CreateAidatTariffRequest) =>
      editing
        ? (await apiClient.put(`/aidat/tariffs/${editing.id}`, req)).data
        : (await apiClient.post('/aidat/tariffs', req)).data,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['aidat-tariffs'] })
      setModalOpen(false)
      form.resetFields()
      setEditing(null)
      message.success(editing ? 'Tarife güncellendi.' : 'Tarife eklendi.')
    },
  })

  const openCreate = () => {
    setEditing(null)
    form.resetFields()
    setModalOpen(true)
  }

  const openEdit = (t: AidatTariffItem) => {
    setEditing(t)
    form.setFieldsValue({ name: t.name, squareMeters: t.squareMeters, amount: t.amount })
    setModalOpen(true)
  }

  const columns: ColumnsType<AidatTariffItem> = [
    { title: 'Tarife Adı', dataIndex: 'name' },
    { title: 'm²', dataIndex: 'squareMeters', width: 100, align: 'right', render: (v: number) => v.toLocaleString('tr-TR', { minimumFractionDigits: 1 }) },
    { title: 'Aylık Tutar', dataIndex: 'amount', width: 140, align: 'right', render: (v: number) => tl(v) },
    {
      title: 'Durum', dataIndex: 'isActive', width: 100,
      render: (v: boolean) => (v ? <Tag color="green">Aktif</Tag> : <Tag color="red">Pasif</Tag>),
    },
    { title: 'Atanmış Daire', dataIndex: 'apartmentCount', width: 120, align: 'right' },
    {
      title: 'İşlem', width: 100,
      render: (_: unknown, r: AidatTariffItem) => (
        <Button size="small" icon={<EditOutlined />} onClick={() => openEdit(r)}>Düzenle</Button>
      ),
    },
  ]

  return (
    <Card
      title="Aidat Tarifeleri"
      extra={
        <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>Tarife Ekle</Button>
      }
    >
      <Table
        rowKey="id"
        size="small"
        columns={columns}
        dataSource={tariffsQuery.data}
        loading={tariffsQuery.isLoading}
        pagination={false}
        locale={{ emptyText: 'Henüz tarife tanımlanmamış.' }}
      />

      <Modal
        title={editing ? 'Tarife Düzenle' : 'Yeni Tarife'}
        open={modalOpen}
        onCancel={() => { setModalOpen(false); setEditing(null); form.resetFields() }}
        confirmLoading={saveMutation.isPending}
        okText="Kaydet"
        cancelText="İptal"
        onOk={() =>
          form.validateFields().then((values) => {
            saveMutation.mutate({
              name: values.name as string,
              squareMeters: values.squareMeters as number,
              amount: values.amount as number,
            })
          })
        }
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="name"
            label="Tarife Adı"
            rules={[
              { required: true, message: 'Tarife adı zorunludur.' },
              { max: 100, message: 'En fazla 100 karakter.' },
            ]}
          >
            <Input placeholder="örn: 1+1 C" maxLength={100} />
          </Form.Item>
          <Form.Item
            name="squareMeters"
            label="Daire Büyüklüğü (m²)"
            rules={[{ required: true, message: 'm² zorunludur.' }]}
          >
            <InputNumber style={{ width: '100%' }} min={0.1} max={1000} precision={2} decimalSeparator="," placeholder="örn: 32,1" />
          </Form.Item>
          <Form.Item
            name="amount"
            label="Aylık Aidat Tutarı (TL)"
            rules={[{ required: true, message: 'Tutar zorunludur.' }]}
          >
            <InputNumber style={{ width: '100%' }} min={0.01} precision={2} decimalSeparator="," addonAfter="TL" placeholder="örn: 2.000,00" />
          </Form.Item>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            Tarife-bazlı dağıtımda dairenin borcu, daireye atanmış tarifenin tutarı kadardır.
            m² dağıtımında birim fiyat bu tarifenin tutarından türetilir.
          </Typography.Text>
        </Form>
      </Modal>
      <Space />
    </Card>
  )
}
