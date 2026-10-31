import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert, Button, Card, Col, DatePicker, Form, Input, InputNumber, Row, Select, Space, Statistic, Table, Tag, Typography, message,
} from 'antd'
import { EyeOutlined, ThunderboltOutlined } from '@ant-design/icons'
import type { ColumnsType } from 'antd/es/table'
import { apiClient } from '../../api/client'
import { formatDate as dateFmt } from '../../utils/dateUtils'
import {
  AIDAT_DISTRIBUTION_LABELS,
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
  const [form] = Form.useForm()
  const [preview, setPreview] = useState<AidatPreviewResult | null>(null)
  const method = Form.useWatch('distributionMethod', form)

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

  const createMutation = useMutation({
    mutationFn: async (req: CreateAidatInvoiceRequest) =>
      (await apiClient.post<CreateAidatInvoiceResult>('/aidat/invoices', req)).data,
    onSuccess: (result) => {
      void queryClient.invalidateQueries({ queryKey: ['aidat-invoices'] })
      setPreview(null)
      form.resetFields()
      message.success(
        `Tahakkuk oluşturuldu: ${result.chargeCount} daire, toplam ${tl(result.totalAmount)}` +
        (result.skippedCount > 0 ? ` (${result.skippedCount} daire atlandı — tarifesiz/m² yok).` : '.'),
      )
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
      title: 'Dönem', dataIndex: 'periodStart', width: 200,
      render: (_: unknown, r: AidatInvoiceListItem) => `${dateFmt(r.periodStart)} – ${dateFmt(r.periodEnd)}`,
    },
    { title: 'Son Ödeme', dataIndex: 'dueDate', width: 110, render: (v: string) => dateFmt(v) },
    {
      title: 'Yöntem', dataIndex: 'distributionMethod', width: 130,
      render: (v: AidatDistributionMethod) => <Tag color="geekblue">{AIDAT_DISTRIBUTION_LABELS[v]}</Tag>,
    },
    { title: 'Daire', dataIndex: 'chargeCount', width: 80, align: 'right' },
    { title: 'Toplam', dataIndex: 'totalAmount', width: 130, align: 'right', render: (v: number) => tl(v) },
    { title: 'Tahsil Edilen', dataIndex: 'paidAmount', width: 130, align: 'right', render: (v: number) => tl(v) },
    {
      title: 'Kalan', dataIndex: 'remainingAmount', width: 130, align: 'right',
      render: (v: number) => (
        <Typography.Text type={v > 0 ? 'danger' : 'success'}>{tl(v)}</Typography.Text>
      ),
    },
    {
      title: 'Tahsilat Oranı', dataIndex: 'collectionRatio', width: 130,
      render: (v: number) => (
        <Tag color={v >= 100 ? 'green' : v >= 50 ? 'gold' : 'red'}>{v.toLocaleString('tr-TR')}%</Tag>
      ),
    },
  ]

  return (
    <Card title="Aidat Tahakkukları">
      <Typography.Title level={5}>Yeni Tahakkuk Oluştur</Typography.Title>
      <Form form={form} layout="vertical">
        <Row gutter={12}>
          <Col span={6}>
            <Form.Item name="periodStart" label="Dönem Başlangıcı" rules={[{ required: true, message: 'Zorunludur.' }]}>
              <DatePicker style={{ width: '100%' }} format="DD.MM.YYYY" />
            </Form.Item>
          </Col>
          <Col span={6}>
            <Form.Item name="periodEnd" label="Dönem Bitişi" rules={[{ required: true, message: 'Zorunludur.' }]}>
              <DatePicker style={{ width: '100%' }} format="DD.MM.YYYY" />
            </Form.Item>
          </Col>
          <Col span={6}>
            <Form.Item name="dueDate" label="Son Ödeme Tarihi" rules={[{ required: true, message: 'Zorunludur.' }]}>
              <DatePicker style={{ width: '100%' }} format="DD.MM.YYYY" />
            </Form.Item>
          </Col>
          <Col span={6}>
            <Form.Item name="distributionMethod" label="Dağıtım Yöntemi" rules={[{ required: true, message: 'Zorunludur.' }]}>
              <Select options={METHOD_OPTIONS} placeholder="Seçin…" />
            </Form.Item>
          </Col>
          {method === 'Esit' && (
            <Col span={6}>
              <Form.Item name="equalAmount" label="Daire Başına Tutar (TL)" rules={[{ required: true, message: 'Eşit dağıtım için tutar zorunludur.' }]}>
                <InputNumber style={{ width: '100%' }} min={0.01} precision={2} decimalSeparator="," addonAfter="TL" />
              </Form.Item>
            </Col>
          )}
          <Col span={method === 'Esit' ? 18 : 24}>
            <Form.Item name="note" label="Not (isteğe bağlı)">
              <Input maxLength={500} placeholder="örn: Ocak 2026 aidatı" />
            </Form.Item>
          </Col>
        </Row>
        <Space>
          <Button icon={<EyeOutlined />} loading={previewMutation.isPending} onClick={handlePreview}>
            Önizle
          </Button>
          <Button type="primary" icon={<ThunderboltOutlined />} loading={createMutation.isPending} onClick={handleCreate}>
            Tahakkuk Oluştur
          </Button>
        </Space>
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
            <Col span={6}><Statistic title="Borçlanan Daire" value={preview.chargeCount} /></Col>
            <Col span={6}>
              <Statistic
                title="Atlanan Daire"
                value={preview.skippedCount}
                valueStyle={{ color: preview.skippedCount > 0 ? '#d46b08' : undefined }}
              />
            </Col>
            <Col span={6}><Statistic title="Toplam Tahakkuk" value={preview.totalAmount} precision={2} suffix="TL" /></Col>
          </Row>
          <Table
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

