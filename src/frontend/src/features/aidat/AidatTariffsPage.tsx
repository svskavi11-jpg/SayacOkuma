import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Button, Card, Form, Input, InputNumber, Modal, Space, Table, Tag, Typography, message } from 'antd'
import { EditOutlined, MoneyCollectOutlined, PlusOutlined } from '@ant-design/icons'
import type { ColumnsType } from 'antd/es/table'
import { apiClient } from '../../api/client'
import type {
  AidatTariffItem,
  AssignTariffBySquareMetersResult,
  CreateAidatTariffRequest,
} from '../../types/api'

const tl = (v: number) => v.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })
const fmtOne = (v: number) => v.toLocaleString('tr-TR', { minimumFractionDigits: 1 })

/** Faz 6.9: Aidat tarife tanımlama ekranı ("1+1 C" → 32,1 m² → 2.000,00 TL/ay). */
export function AidatTariffsPage() {
  const queryClient = useQueryClient()
  const [modalOpen, setModalOpen] = useState(false)
  const [editing, setEditing] = useState<AidatTariffItem | null>(null)
  const [form] = Form.useForm()

  // Faz 6.9: Toplu tarife atama — m²'si eşleşen tüm dairelere.
  const [assignFor, setAssignFor] = useState<AidatTariffItem | null>(null)
  const [assignForm] = Form.useForm<{ squareMeters: number }>()
  const assignMutation = useMutation({
    mutationFn: async (input: { tariffId: string; squareMeters: number }) =>
      (await apiClient.post<AssignTariffBySquareMetersResult>(
        `/aidat/tariffs/${input.tariffId}/assign-by-square-meters`,
        { squareMeters: input.squareMeters },
      )).data,
    onSuccess: (res) => {
      void queryClient.invalidateQueries({ queryKey: ['aidat-tariffs'] })
      void queryClient.invalidateQueries({ queryKey: ['apartments'] })
      setAssignFor(null)
      const doors = res.doorNumbers.length > 0
        ? ` Atanan daireler: ${res.doorNumbers.slice(0, 12).join(', ')}${res.doorNumbers.length > 12 ? ', …' : ''}`
        : ''
      Modal.success({
        title: 'Toplu tarife atama tamamlandı',
        content:
          `${res.assignedCount} daireye atandı.` +
          (res.alreadyAssignedCount > 0 ? ` ${res.alreadyAssignedCount} daire zaten bu tarifedeydi.` : '') +
          (res.noMatchCount > 0 ? ` ${res.noMatchCount} dairenin m²'si eşleşmedi.` : '') +
          doors,
        okText: 'Tamam',
      })
    },
  })

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
      title: 'İşlem',
      width: 220,
      render: (_: unknown, r: AidatTariffItem) => (
        <Space size={4}>
          <Button size="small" icon={<EditOutlined />} onClick={() => openEdit(r)}>Düzenle</Button>
          <Button
            size="small"
            icon={<MoneyCollectOutlined />}
            disabled={!r.isActive}
            onClick={() => {
              setAssignFor(r)
              assignForm.setFieldsValue({ squareMeters: r.squareMeters })
            }}
          >
            Daireleri Ata
          </Button>
        </Space>
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

      {/* Faz 6.9: Toplu tarife atama — m²'si eşleşen tüm dairelere */}
      <Modal
        title={`Daireleri Ata — ${assignFor?.name ?? ''}`}
        open={assignFor !== null}
        onCancel={() => setAssignFor(null)}
        confirmLoading={assignMutation.isPending}
        okText="Daireleri Ata"
        cancelText="Vazgeç"
        onOk={() =>
          assignForm.validateFields().then((values) => {
            if (assignFor === null) return
            assignMutation.mutate({ tariffId: assignFor.id, squareMeters: values.squareMeters })
          })
        }
      >
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 16 }}
          message={`m²'si ${assignFor ? fmtOne(assignFor.squareMeters) : ''} olan tüm aktif daireler (SCD aktif kaydı) bu tarifeye atanacak.`}
          description="Zaten bu tarifeye atanmış daireler tekrar dokunulmaz. m² eşleştirmesi kuruş hassasiyetiyle yapılır (47,4 ≡ 47,40)."
        />
        <Form form={assignForm} layout="vertical">
          <Form.Item
            name="squareMeters"
            label="Eşleştirilecek m²"
            rules={[{ required: true, message: 'm² değeri zorunludur.' }]}
          >
            <InputNumber style={{ width: '100%' }} min={0.1} max={1000} precision={2} decimalSeparator="," addonAfter="m²" />
          </Form.Item>
        </Form>
      </Modal>
    </Card>
  )
}
