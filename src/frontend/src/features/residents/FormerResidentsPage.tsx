import { AppModal } from '../../components/AppModal'
import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert,
  Button,
  Card,
  DatePicker,
  Form,
  Input,
  InputNumber,
  Table,
  Tabs,
  Tag,
  Tooltip,
  Typography,
  message,
} from 'antd'
import { DollarOutlined } from '@ant-design/icons'
import type { ColumnsType } from 'antd/es/table'
import dayjs, { type Dayjs } from 'dayjs'
import { apiClient } from '../../api/client'
import { formatDate as dateFmt } from '../../utils/dateUtils'
import type { FormerResidentDebtItem, RecordPaymentRequest } from '../../types/api'
import { ResidentHistoryTab } from './ResidentHistoryTab'

const tl = (v: number) => v.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })

/**
 * Faz 7.1: Geçmiş Sakinler — dairede oturmayı bitirmiş ve borcu kalan eski sakinler.
 * Kiracı satırlarında KMK m.22 müteselsil sorumluluk uyarısı gösterilir;
 * "Tahsilat Gir" hedefli tahsilat açar (borç yalnızca o sakinin borçlarına işlenir).
 */
export function FormerResidentsPage() {
  const queryClient = useQueryClient()
  const [paymentFor, setPaymentFor] = useState<FormerResidentDebtItem | null>(null)
  const [form] = Form.useForm<{ paymentDate: Dayjs; amount: number; description?: string }>()

  const formerQuery = useQuery({
    queryKey: ['former-residents'],
    queryFn: async () =>
      (await apiClient.get<FormerResidentDebtItem[]>('/residents/former-with-debt')).data,
  })

  const paymentMutation = useMutation({
    mutationFn: async (req: RecordPaymentRequest) =>
      (await apiClient.post('/payments', req)).data,
    onSuccess: (_res, req) => {
      void queryClient.invalidateQueries({ queryKey: ['former-residents'] })
      void queryClient.invalidateQueries({ queryKey: ['ledger', req.apartmentId] })
      void queryClient.invalidateQueries({ queryKey: ['payments', req.apartmentId] })
      setPaymentFor(null)
      form.resetFields()
      message.success('Hedefli tahsilat kaydedildi — borç yalnızca bu sakinin borçlarına işlendi.')
    },
  })

// ── Tablo kolonları ──────────────────────────────────────────────────────────

  const columns: ColumnsType<FormerResidentDebtItem> = [
    { title: 'Kapı No', dataIndex: 'doorNumber', width: 80 },
    { title: 'Daire', dataIndex: 'apartmentTitle' },
    {
      title: 'Sakin',
      dataIndex: 'ownerName',
      render: (v: string, r: FormerResidentDebtItem) => (
        <div>
          <Typography.Text strong>{v}</Typography.Text>
          {r.phoneNumber && (
            <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block' }}>
              {r.phoneNumber}
            </Typography.Text>
          )}
        </div>
      ),
    },
    {
      title: 'Tür',
      dataIndex: 'residentTypeLabel',
      width: 100,
      render: (v: string) => (v === 'Kiracı' ? <Tag color="geekblue">Kiracı</Tag> : <Tag color="purple">Ev Sahibi</Tag>),
    },
    { title: 'Çıkış Tarihi', dataIndex: 'leftOn', width: 120, render: (v: string | null) => dateFmt(v) },
    { title: 'Anapara', dataIndex: 'principalDue', width: 120, align: 'right', render: (v: number) => tl(v) },
    { title: 'Faiz', dataIndex: 'interestAccrued', width: 110, align: 'right', render: (v: number) => tl(v) },
    {
      title: 'Bakiye',
      dataIndex: 'balance',
      width: 130,
      align: 'right',
      render: (v: number) => <Typography.Text type="danger" strong>{tl(v)}</Typography.Text>,
    },
    {
      title: 'Rücu (KMK m.22)',
      dataIndex: 'rucuPossible',
      width: 170,
      render: (v: boolean, r: FormerResidentDebtItem) =>
        v ? (
          <Tooltip title={`Ev Sahibi: ${r.evSahibiName ?? '—'} — borç için ev sahibine de rücu edilebilir (müteselsil sorumluluk).`}>
            <Tag color="orange" style={{ cursor: 'help' }}>Ev Sahibine Rücu</Tag>
          </Tooltip>
        ) : (
          <Tag>—</Tag>
        ),
    },
    {
      title: 'İşlem',
      fixed: 'right',
      width: 130,
      render: (_: unknown, r: FormerResidentDebtItem) => (
        <Button
          size="small"
          type="primary"
          icon={<DollarOutlined />}
          onClick={() => {
            setPaymentFor(r)
            form.setFieldsValue({ amount: r.balance })
          }}
        >
          Tahsilat Gir
        </Button>
      ),
    },
  ]

  return (
    <Card title="Sakin Geçmişi">
      <Tabs
        items={[
          {
            key: 'debt',
            label: 'Borçlu Eski Sakinler',
            children: (
      <>
      <Alert
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
        message="Dairede oturmayı bitirmiş ancak cari hesabında borcu kalan sakinler listelenir."
        description="KMK Madde 22 gereği borçtan ev sahibi ve kiracı müteselsilen sorumludur: kiracı borcunu ödemeden ayrıldıysa kalan borç için ev sahibine rücu edilebilir."
      />
      <Table
        scroll={{ x: 'max-content' }}
        rowKey={(r) => `${r.apartmentId}-${r.ownerId}`}
        size="small"
        columns={columns}
        dataSource={formerQuery.data}
        loading={formerQuery.isLoading}
        pagination={{ pageSize: 15 }}
        locale={{ emptyText: 'Borcu olan eski sakin yok. 🎉' }}
      />
      </>
            ),
          },
          {
            key: 'history',
            label: 'Tüm Değişim Geçmişi',
            children: <ResidentHistoryTab />,
          },
        ]}
      />

      <AppModal
        title={`Tahsilat Gir — ${paymentFor?.ownerName ?? ''} (Daire ${paymentFor?.doorNumber ?? ''})`}
        open={paymentFor !== null}
        onCancel={() => setPaymentFor(null)}
        confirmLoading={paymentMutation.isPending}
        okText="Tahsilatı Kaydet"
        cancelText="İptal"
        onOk={() =>
          form.validateFields().then((values) => {
            if (paymentFor === null) return
            paymentMutation.mutate({
              apartmentId: paymentFor.apartmentId,
              paymentDate: (values.paymentDate as Dayjs).format('YYYY-MM-DD'),
              amount: values.amount as number,
              description:
                (values.description as string) ||
                `Geçmiş sakin tahsilatı — ${paymentFor.ownerName} (${paymentFor.residentTypeLabel})`,
              targetOwnerId: paymentFor.ownerId,
            })
          })
        }
      >
        {paymentFor && (
          <>
            <Alert
              type={paymentFor.rucuPossible ? 'warning' : 'info'}
              showIcon
              style={{ marginBottom: 16 }}
              message={
                paymentFor.rucuPossible
                  ? `Kalan bakiye: ${tl(paymentFor.balance)} — Ev Sahibine rücu edilebilir (${paymentFor.evSahibiName ?? '—'}).`
                  : `Kalan bakiye: ${tl(paymentFor.balance)}`
              }
              description="Bu tahsilat hedeflidir: yalnızca bu sakinin borçlarına (Aidat/Isınma, vade sırasına göre FIFO) işlenir; diğer sakinlerin borçlarına dokunmaz."
            />
            <Form form={form} layout="vertical">
              <Form.Item name="paymentDate" label="Tahsilat Tarihi" rules={[{ required: true, message: 'Tarih zorunludur.' }]}>
                <DatePicker style={{ width: '100%' }} format="DD.MM.YYYY" disabledDate={(d) => d.isAfter(dayjs(), 'day')} />
              </Form.Item>
              <Form.Item name="amount" label="Tutar (TL)" rules={[{ required: true, message: 'Tutar zorunludur.' }]}>
                <InputNumber style={{ width: '100%' }} min={0.01} precision={2} decimalSeparator="," addonAfter="TL" />
              </Form.Item>
              <Form.Item name="description" label="Açıklama / Dekont No">
                <Input maxLength={500} placeholder="İsteğe bağlı" />
              </Form.Item>
            </Form>
          </>
        )}
      </AppModal>
    </Card>
  )
}
