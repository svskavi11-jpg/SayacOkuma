import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert, Button, Card, Col, DatePicker, Form, Input, InputNumber, Modal, Popconfirm, Row, Select, Space, Statistic, Table, Tag, Tooltip, Typography, message,
} from 'antd'
import { EyeOutlined, StopOutlined, ThunderboltOutlined } from '@ant-design/icons'
import type { ColumnsType } from 'antd/es/table'
import { apiClient } from '../../api/client'
import { useAuth } from '../auth/AuthContext'
import { formatDate as dateFmt } from '../../utils/dateUtils'
import {
  AIDAT_CHARGE_TYPE_LABELS,
  AIDAT_DISTRIBUTION_LABELS,
  type AidatChargeType,
  type AidatDistributionMethod,
  type AidatInvoiceListItem,
  type AidatPreviewResult,
  type CreateAidatInvoiceRequest,
  type CreateAidatInvoiceResult,
} from '../../types/api'

const tl = (v: number) => v.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })

const METHOD_OPTIONS = (Object.entries(AIDAT_DISTRIBUTION_LABELS) as [AidatDistributionMethod, string][])
  .map(([value, label]) => ({ value, label }))

/** Faz 6.9: Aidat tahakkuk oluşturma + geçmiş dönem listesi. */
export function AidatInvoicesPage() {
  const queryClient = useQueryClient()
  const { hasPermission } = useAuth()
  const canCreate = hasPermission('Invoices.Create')
  const canCancel = hasPermission('Invoices.Cancel')
  const [form] = Form.useForm()
  const [preview, setPreview] = useState<AidatPreviewResult | null>(null)
  const method = Form.useWatch('distributionMethod', form)
  const chargeType = Form.useWatch('chargeType', form) as AidatChargeType | undefined

  const invoicesQuery = useQuery({
    queryKey: ['aidat-invoices'],
    queryFn: async () => (await apiClient.get<AidatInvoiceListItem[]>('/aidat/invoices')).data,
  })

  const buildRequest = (values: Record<string, unknown>): CreateAidatInvoiceRequest => {
    const req: CreateAidatInvoiceRequest = {
      periodStart: (values.periodStart as { format: (f: string) => string }).format('YYYY-MM-DD'),
      periodEnd: (values.periodEnd as { format: (f: string) => string }).format('YYYY-MM-DD'),
      dueDate: (values.dueDate as { format: (f: string) => string }).format('YYYY-MM-DD'),
      distributionMethod: values.distributionMethod as AidatDistributionMethod,
      equalAmount: values.distributionMethod === 'Esit' ? (values.equalAmount as number) : null,
      note: (values.note as string) || null,
      chargeType: (values.chargeType as AidatChargeType) || 'StandartAidat',
      title: values.chargeType === 'OzelToplama' ? ((values.title as string) || null) : null,
      targetAmount: values.chargeType === 'OzelToplama' ? (values.targetAmount as number) : null,
    }
    // Hata ayıklama: backend'e giden payload'ı konsolda göster (enum/string tip kontrolü).
    console.log('[Aidat] Tahakkuk payload:', JSON.stringify(req, null, 2))
    return req
  }

  const previewMutation = useMutation({
    mutationFn: async (req: CreateAidatInvoiceRequest) =>
      (await apiClient.post<AidatPreviewResult>('/aidat/invoices/preview', req)).data,
    onSuccess: (data) => setPreview(data),
  })

  const notifyMutation = useMutation({
    mutationFn: async (apartmentIds: string[]) =>
      (
        await apiClient.post('/notifications', {
          apartmentIds,
          channel: 'Sms',
          purpose: 'Aidat',
          includePdf: false,
        })
      ).data as { sent: number; failed: number; simulated: number },
    onSuccess: (result) => {
      void queryClient.invalidateQueries({ queryKey: ['notification-logs'] })
      message.success(
        `Aidat bildirimi bitti. Başarılı: ${result.sent}, Başarısız: ${result.failed}, Simüle: ${result.simulated}.`,
      )
    },
  })

  const createMutation = useMutation({
    mutationFn: async (req: CreateAidatInvoiceRequest) =>
      (await apiClient.post<CreateAidatInvoiceResult>('/aidat/invoices', req)).data,
    onSuccess: (result) => {
      void queryClient.invalidateQueries({ queryKey: ['aidat-invoices'] })
      void queryClient.invalidateQueries({ queryKey: ['apartments'] })
      setPreview(null)
      form.resetFields()
      message.success(
        `Tahakkuk oluşturuldu: ${result.chargeCount} daire, toplam ${tl(result.totalAmount)}` +
        (result.skippedCount > 0 ? ` (${result.skippedCount} daire atlandı — tarifesiz/m² yok).` : '.'),
      )

      if (!result.canNotify) {
        if (result.notifyBlockReason) {
          Modal.warning({ title: 'Bildirim gönderilemez', content: result.notifyBlockReason })
        }
        return
      }

      Modal.confirm({
        title: 'Aidat bildirimi gönderilsin mi?',
        content: `Hesaplama bitti. Tahakkuk gören ${result.chargedApartmentIds.length} daireye SMS aidat bildirimi gönderilsin mi?`,
        okText: 'Evet, gönder',
        cancelText: 'Hayır',
        onOk: () => notifyMutation.mutateAsync(result.chargedApartmentIds),
      })
    },
  })

  const cancelMutation = useMutation({
    mutationFn: async (id: string) =>
      (await apiClient.post(`/aidat/invoices/${id}/cancel`)).data,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['aidat-invoices'] })
      void queryClient.invalidateQueries({ queryKey: ['ledger'] })
      void queryClient.invalidateQueries({ queryKey: ['apartments'] })
      message.success('Aidat tahakkuku iptal edildi. Daire borçları cari hesaptan düşüldü.')
    },
  })

  const handlePreview = () =>
    form.validateFields().then((values) => previewMutation.mutate(buildRequest(values)))

  const handleCreate = () =>
    form.validateFields().then((values) => createMutation.mutate(buildRequest(values)))

// ── Tablo kolonları ──────────────────────────────────────────────────────────

  const previewColumns: ColumnsType<AidatPreviewResult['rows'][number]> = [
    { title: 'Kapı No', dataIndex: 'doorNumber', width: 90 },
    { title: 'Daire', dataIndex: 'apartmentTitle' },
    { title: 'Tarife', dataIndex: 'tariffName', width: 130, render: (v: string | null) => v ?? '—' },
    {
      title: 'm²', dataIndex: 'squareMeters', width: 90, align: 'right',
      render: (v: number | null) => (v === null ? '—' : v.toLocaleString('tr-TR', { minimumFractionDigits: 1 })),
    },
    {
      title: 'Borç', dataIndex: 'amount', width: 130, align: 'right',
      render: (v: number, r) => (r.skipReason ? <Tag color="orange">Atlandı</Tag> : tl(v)),
    },
    { title: 'Not', dataIndex: 'skipReason', render: (v: string | null) => v ?? '—' },
  ]

  const listColumns: ColumnsType<AidatInvoiceListItem> = [
    {
      title: 'Dönem', dataIndex: 'periodStart', width: 240,
      render: (_: unknown, r: AidatInvoiceListItem) => {
        const label = r.title || `${dateFmt(r.periodStart)} – ${dateFmt(r.periodEnd)}`
        return (
          <Space size={6}>
            <Typography.Text delete={r.isCancelled} type={r.isCancelled ? 'secondary' : undefined}>
              {label}
            </Typography.Text>
            {r.isCancelled && <Tag color="red">İptal Edildi</Tag>}
          </Space>
        )
      },
    },
    {
      title: 'Tür', dataIndex: 'chargeType', width: 130,
      render: (v: AidatChargeType) => <Tag color={v === 'OzelToplama' ? 'purple' : 'blue'}>{AIDAT_CHARGE_TYPE_LABELS[v] ?? 'Standart Aidat'}</Tag>,
    },
    { title: 'Son Ödeme', dataIndex: 'dueDate', width: 110, render: (v: string) => dateFmt(v) },
    {
      title: 'Yöntem', dataIndex: 'distributionMethod', width: 130,
      render: (v: AidatDistributionMethod) => <Tag color="geekblue">{AIDAT_DISTRIBUTION_LABELS[v]}</Tag>,
    },
    { title: 'Daire', dataIndex: 'chargeCount', width: 80, align: 'right' },
    {
      title: 'Toplam', dataIndex: 'totalAmount', width: 130, align: 'right',
      render: (v: number, r) => (
        <Typography.Text delete={r.isCancelled} type={r.isCancelled ? 'secondary' : undefined}>
          {tl(v)}
        </Typography.Text>
      ),
    },
    {
      title: 'Hedef', dataIndex: 'targetAmount', width: 130, align: 'right',
      render: (v: number | null) => (v == null ? '—' : tl(v)),
    },
    {
      title: 'Fon Harcaması', dataIndex: 'fundSpent', width: 140, align: 'right',
      render: (v: number, r) => (r.chargeType === 'OzelToplama' ? tl(v) : '—'),
    },
    {
      title: 'Fon Bakiyesi', dataIndex: 'fundBalance', width: 140, align: 'right',
      render: (v: number, r) => (r.chargeType === 'OzelToplama' ? tl(v) : '—'),
    },
    { title: 'Tahsil Edilen', dataIndex: 'paidAmount', width: 130, align: 'right', render: (v: number) => tl(v) },
    {
      title: 'Kalan', dataIndex: 'remainingAmount', width: 130, align: 'right',
      render: (v: number, r) => (
        r.isCancelled
          ? <Typography.Text type="secondary">{tl(0)}</Typography.Text>
          : <Typography.Text type={v > 0 ? 'danger' : 'success'}>{tl(v)}</Typography.Text>
      ),
    },
    {
      title: 'Tahsilat Oranı', dataIndex: 'collectionRatio', width: 130,
      render: (v: number, r) => (
        r.isCancelled
          ? <Tag color="default">İptal</Tag>
          : <Tag color={v >= 100 ? 'green' : v >= 50 ? 'gold' : 'red'}>{v.toLocaleString('tr-TR')}%</Tag>
      ),
    },
    {
      title: 'İşlemi Yapan',
      width: 180,
      render: (_: unknown, r: AidatInvoiceListItem) => {
        const name = r.isCancelled ? (r.lastModifiedBy || r.createdBy) : r.createdBy
        return name?.trim() ? name : '—'
      },
    },
    {
      title: 'İşlemler',
      key: 'actions',
      fixed: 'right',
      width: 140,
      align: 'center',
      render: (_: unknown, r: AidatInvoiceListItem) => {
        if (r.isCancelled) {
          return (
            <Tooltip title={r.cancelledAt ? `İptal tarihi: ${dateFmt(r.cancelledAt)}` : 'İptal edildi'}>
              <Tag color="red">İptal Edildi</Tag>
            </Tooltip>
          )
        }

        const hasPayments = r.paidAmount > 0
        const hasExpenses = r.chargeType === 'OzelToplama' && r.fundSpent > 0
        const isLocked = !!r.isLocked
        const disabled = isLocked || hasPayments || hasExpenses

        const disableReason = isLocked
          ? 'Bütçe dönemi kilitli olduğu için iptal edilemez.'
          : hasPayments
            ? 'Bu tahakkuka ait tahsilat bulunduğu için iptal edilemez. Önce ilgili tahsilatları iptal ediniz.'
            : hasExpenses
              ? 'Bu özel toplama fonuna bağlı gider kaydı bulunduğu için iptal edilemez.'
              : null

        if (!canCancel) {
          return null
        }

        if (disabled) {
          return (
            <Tooltip title={disableReason}>
              <span>
                <Button size="small" danger icon={<StopOutlined />} disabled>
                  İptal Et
                </Button>
              </span>
            </Tooltip>
          )
        }

        return (
          <Popconfirm
            title="Tahakkuku İptal Et"
            description="Bu tahakkuku iptal etmek istediğinize emin misiniz? Bu işlem geri alınamaz, dairelerin borçları silinir."
            okText="Evet, İptal Et"
            cancelText="Vazgeç"
            okButtonProps={{ danger: true, loading: cancelMutation.isPending }}
            onConfirm={() => cancelMutation.mutate(r.id)}
          >
            <Button size="small" danger icon={<StopOutlined />} loading={cancelMutation.isPending}>
              İptal Et
            </Button>
          </Popconfirm>
        )
      },
    },
  ]

  return (
    <Card title="Aidat Tahakkukları">
      <Typography.Title level={5}>Yeni Tahakkuk Oluştur</Typography.Title>
      <Form form={form} layout="vertical" initialValues={{ chargeType: 'StandartAidat' }}>
        <Row gutter={12}>
          <Col xs={24} md={6}>
            <Form.Item name="chargeType" label="Tahakkuk Türü" rules={[{ required: true, message: 'Zorunludur.' }]}>
              <Select
                options={(Object.entries(AIDAT_CHARGE_TYPE_LABELS) as [AidatChargeType, string][])
                  .map(([value, label]) => ({ value, label }))}
              />
            </Form.Item>
          </Col>
          {chargeType === 'OzelToplama' && (
            <>
              <Col xs={24} md={8}>
                <Form.Item name="title" label="Fon Adı" rules={[{ required: true, message: 'Fon adı zorunludur.' }]}>
                  <Input maxLength={120} placeholder="Çatı Onarımı Fonu" />
                </Form.Item>
              </Col>
              <Col xs={24} md={6}>
                <Form.Item name="targetAmount" label="Hedef Tutar" rules={[{ required: true, message: 'Hedef tutar zorunludur.' }]}>
                  <InputNumber style={{ width: '100%' }} min={0.01} precision={2} decimalSeparator="," addonAfter="TL" />
                </Form.Item>
              </Col>
            </>
          )}
          <Col xs={24} md={6}>
            <Form.Item name="periodStart" label="Dönem Başlangıcı" rules={[{ required: true, message: 'Zorunludur.' }]}>
              <DatePicker style={{ width: '100%' }} format="DD.MM.YYYY" />
            </Form.Item>
          </Col>
          <Col xs={24} md={6}>
            <Form.Item name="periodEnd" label="Dönem Bitişi" rules={[{ required: true, message: 'Zorunludur.' }]}>
              <DatePicker style={{ width: '100%' }} format="DD.MM.YYYY" />
            </Form.Item>
          </Col>
          <Col xs={24} md={6}>
            <Form.Item name="dueDate" label="Son Ödeme Tarihi" rules={[{ required: true, message: 'Zorunludur.' }]}>
              <DatePicker style={{ width: '100%' }} format="DD.MM.YYYY" />
            </Form.Item>
          </Col>
          <Col xs={24} md={6}>
            <Form.Item name="distributionMethod" label="Dağıtım Yöntemi" rules={[{ required: true, message: 'Zorunludur.' }]}>
              <Select options={METHOD_OPTIONS} placeholder="Seçin…" />
            </Form.Item>
          </Col>
          {method === 'Esit' && (
            <Col xs={24} md={6}>
              <Form.Item name="equalAmount" label="Daire Başına Tutar (TL)" rules={[{ required: true, message: 'Eşit dağıtım için tutar zorunludur.' }]}>
                <InputNumber style={{ width: '100%' }} min={0.01} precision={2} decimalSeparator="," addonAfter="TL" />
              </Form.Item>
            </Col>
          )}
          <Col xs={24} md={method === 'Esit' ? 18 : 24}>
            <Form.Item name="note" label="Not (isteğe bağlı)">
              <Input maxLength={500} placeholder="örn: Ocak 2026 aidatı" />
            </Form.Item>
          </Col>
        </Row>
        {canCreate && (
        <Space>
          <Button icon={<EyeOutlined />} loading={previewMutation.isPending} onClick={handlePreview}>
            Önizle
          </Button>
          <Button type="primary" icon={<ThunderboltOutlined />} loading={createMutation.isPending} onClick={handleCreate}>
            Tahakkuk Oluştur
          </Button>
        </Space>
        )}
      </Form>

      {preview && (
        <div style={{ marginTop: 16 }}>
          {preview.skippedCount > 0 && (
            <Alert
              type="warning"
              showIcon
              style={{ marginBottom: 12 }}
              message={`${preview.skippedCount} daire atlanacak (tarife atanmamış veya aktif m² kaydı yok).`}
            />
          )}
          <Row gutter={16} style={{ marginBottom: 12 }}>
            <Col xs={24} md={6}><Statistic title="Borçlanan Daire" value={preview.chargeCount} /></Col>
            <Col xs={24} md={6}>
              <Statistic
                title="Atlanan Daire"
                value={preview.skippedCount}
                valueStyle={{ color: preview.skippedCount > 0 ? '#d46b08' : undefined }}
              />
            </Col>
            <Col xs={24} md={6}><Statistic title="Toplam Tahakkuk" value={preview.totalAmount} precision={2} suffix="TL" /></Col>
          </Row>
          <Table
            scroll={{ x: 'max-content' }}
            rowKey="doorNumber"
            size="small"
            columns={previewColumns}
            dataSource={preview.rows}
            pagination={{ pageSize: 15 }}
          />
        </div>
      )}

      <Typography.Title level={5} style={{ marginTop: 24 }}>Geçmiş Dönemler</Typography.Title>
      <Table
        scroll={{ x: 'max-content' }}
        rowKey="id"
        size="small"
        columns={listColumns}
        dataSource={invoicesQuery.data}
        loading={invoicesQuery.isLoading}
        pagination={{ pageSize: 10 }}
        locale={{ emptyText: 'Henüz aidat tahakkuku oluşturulmamış.' }}
      />
    </Card>
  )
}

