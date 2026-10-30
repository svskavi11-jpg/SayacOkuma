import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert,
  Button,
  Card,
  Col,
  DatePicker,
  Descriptions,
  Form,
  Input,
  InputNumber,
  Modal,
  Row,
  Select,
  Statistic,
  Table,
  Tag,
  Tabs,
  Typography,
  message,
} from 'antd'
import {
  DollarOutlined,
  MinusCircleOutlined,
  PlusOutlined,
  StopOutlined,
  SwapOutlined,
} from '@ant-design/icons'
import type { ColumnsType } from 'antd/es/table'
import dayjs, { type Dayjs } from 'dayjs'
import { apiClient } from '../../api/client'
import { formatDate as dateFmt } from '../../utils/dateUtils'
import type {
  ApartmentListItem,
  CashFlowResult,
  LedgerResult,
  PaymentListItem,
  RecordPaymentRequest,
} from '../../types/api'

const tl = (v: number) => v.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })

// Backend'den gelen fatura dönem etiketi ("Apr 2026 - May 2026") Türkçe aylara çevrilir.
const EN_TO_TR_MONTHS: Record<string, string> = {
  Jan: 'Oca', Feb: 'Şub', Mar: 'Mar', Apr: 'Nis', May: 'May', Jun: 'Haz',
  Jul: 'Tem', Aug: 'Ağu', Sep: 'Eyl', Oct: 'Eki', Nov: 'Kas', Dec: 'Ara',
}

/** "Apr 2026 - May 2026" → "Nis 2026 - May 2026" (bilinmeyen kısımlar olduğu gibi kalır). */
function translatePeriodLabel(label: string): string {
  return label.replace(/\b[A-Z][a-z]{2}\b/g, (m) => EN_TO_TR_MONTHS[m] ?? m)
}

// Cari hesap hareket satırı (Devir Anapara / Devir Faiz / Fatura / Faiz / Tahsilat)
interface MovementRow {
  key: string
  date: string
  type: 'Devir Anapara' | 'Devir Faiz' | 'Fatura' | 'Faiz' | 'Tahsilat'
  description: string
  debit: number
  credit: number
  interestDays: number | null
}
type MovementRowWithBalance = MovementRow & { runningBalance: number }

// ────────────────────────────────────────────────────────────────────────────
// Ana sayfa
// ────────────────────────────────────────────────────────────────────────────

export function PaymentsPage() {
  const [activeTab, setActiveTab] = useState('payments')

  return (
    <Card title="Tahsilat ve Cari Hesap">
      <Tabs
        activeKey={activeTab}
        onChange={setActiveTab}
        items={[
          { key: 'payments', label: '💰 Tahsilatlar', children: <PaymentsTab /> },
          { key: 'ledger',   label: '📊 Cari Hesap',  children: <LedgerTab /> },
          { key: 'cashflow', label: '🔄 Nakit Akış',   children: <CashFlowTab /> },
        ]}
      />
    </Card>
  )
}

// ────────────────────────────────────────────────────────────────────────────
// Tab 1 — Tahsilat girişi + liste
// ────────────────────────────────────────────────────────────────────────────

function PaymentsTab() {
  const queryClient = useQueryClient()
  const [selectedApartmentId, setSelectedApartmentId] = useState<string | null>(null)
  const [addOpen, setAddOpen] = useState(false)
  const [cancelOpen, setCancelOpen] = useState<string | null>(null) // paymentId
  const [form] = Form.useForm()
  const [cancelForm] = Form.useForm()

  // Daire listesi (Combobox için)
  const apartmentsQuery = useQuery({
    queryKey: ['apartments'],
    queryFn: async () => (await apiClient.get<ApartmentListItem[]>('/apartments')).data,
  })

  // Seçili dairenin tahsilat geçmişi
  const paymentsQuery = useQuery({
    queryKey: ['payments', selectedApartmentId],
    queryFn: async () =>
      (
        await apiClient.get<PaymentListItem[]>(
          `/payments/by-apartment/${selectedApartmentId}`,
        )
      ).data,
    enabled: !!selectedApartmentId,
  })

  const recordMutation = useMutation({
    mutationFn: async (req: RecordPaymentRequest) =>
      (await apiClient.post('/payments', req)).data,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['payments', selectedApartmentId] })
      void queryClient.invalidateQueries({ queryKey: ['ledger', selectedApartmentId] })
      setAddOpen(false)
      form.resetFields()
      message.success('Tahsilat kaydedildi.')
    },
  })

  const cancelMutation = useMutation({
    mutationFn: async ({ paymentId, reason }: { paymentId: string; reason: string }) =>
      (await apiClient.post(`/payments/${paymentId}/cancel`, { reason })).data,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['payments', selectedApartmentId] })
      void queryClient.invalidateQueries({ queryKey: ['ledger', selectedApartmentId] })
      setCancelOpen(null)
      cancelForm.resetFields()
      message.success('Tahsilat iptal edildi.')
    },
  })

  const apartmentOptions =
    apartmentsQuery.data?.map((a) => ({
      value: a.id,
      label: `${a.doorNumber} — ${a.title}`,
    })) ?? []

  const columns: ColumnsType<PaymentListItem> = [
    {
      title: 'Tarih',
      dataIndex: 'paymentDate',
      width: 110,
      render: (v: string) => dateFmt(v),
    },
    {
      title: 'Tutar',
      dataIndex: 'amount',
      width: 130,
      align: 'right',
      render: (v: number) => tl(v),
    },
    { title: 'Açıklama', dataIndex: 'description', render: (v: string | null) => v ?? '—' },
    { title: 'Girişi Yapan', dataIndex: 'receivedBy', width: 140 },
    {
      title: 'Durum',
      dataIndex: 'isCancelled',
      width: 100,
      render: (v: boolean) =>
        v ? <Tag color="red">İptal</Tag> : <Tag color="green">Aktif</Tag>,
    },
    {
      title: 'İşlem',
      width: 90,
      render: (_: unknown, r: PaymentListItem) =>
        !r.isCancelled ? (
          <Button
            size="small"
            danger
            icon={<StopOutlined />}
            onClick={() => setCancelOpen(r.id)}
          >
            İptal Et
          </Button>
        ) : (
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            {r.cancellationReason}
          </Typography.Text>
        ),
    },
  ]

  return (
    <>
      <Row gutter={12} style={{ marginBottom: 16 }}>
        <Col span={14}>
          <Select
            style={{ width: '100%' }}
            placeholder="Daire seçin…"
            showSearch
            optionFilterProp="label"
            options={apartmentOptions}
            loading={apartmentsQuery.isLoading}
            onChange={(v: string) => setSelectedApartmentId(v)}
          />
        </Col>
        <Col span={10}>
          <Button
            type="primary"
            icon={<PlusOutlined />}
            disabled={!selectedApartmentId}
            onClick={() => {
              form.setFieldValue('apartmentId', selectedApartmentId)
              setAddOpen(true)
            }}
          >
            Tahsilat Ekle
          </Button>
        </Col>
      </Row>

      <Table
        rowKey="id"
        size="small"
        columns={columns}
        dataSource={paymentsQuery.data}
        loading={paymentsQuery.isLoading}
        pagination={{ pageSize: 10 }}
        locale={{ emptyText: selectedApartmentId ? 'Tahsilat kaydı yok.' : 'Önce daire seçin.' }}
      />

      {/* Tahsilat Ekleme Modal */}
      <Modal
        title="Tahsilat Ekle"
        open={addOpen}
        onCancel={() => { setAddOpen(false); form.resetFields() }}
        confirmLoading={recordMutation.isPending}
        okText="Kaydet"
        cancelText="İptal"
        onOk={() =>
          form.validateFields().then((values) => {
            recordMutation.mutate({
              apartmentId: selectedApartmentId!,
              paymentDate: (values.paymentDate as Dayjs).format('YYYY-MM-DD'),
              amount: values.amount as number,
              description: values.description as string | null,
              receivedBy: values.receivedBy as string | null,
            })
          })
        }
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="paymentDate"
            label="Tahsilat Tarihi"
            rules={[{ required: true, message: 'Tarih zorunludur.' }]}
          >
            <DatePicker
              style={{ width: '100%' }}
              format="DD.MM.YYYY"
              disabledDate={(d) => d.isAfter(dayjs(), 'day')}
            />
          </Form.Item>
          <Form.Item
            name="amount"
            label="Tutar (TL)"
            rules={[{ required: true, message: 'Tutar zorunludur.' }]}
          >
            <InputNumber
              style={{ width: '100%' }}
              min={0.01}
              precision={2}
              decimalSeparator=","
              addonAfter="TL"
            />
          </Form.Item>
          <Form.Item name="description" label="Açıklama / Dekont No">
            <Input maxLength={500} placeholder="İsteğe bağlı" />
          </Form.Item>
          <Form.Item name="receivedBy" label="Tahsilat Yapan">
            <Input maxLength={100} placeholder="İsteğe bağlı — Faz 7'de JWT'den otomatik dolar" />
          </Form.Item>
        </Form>
      </Modal>

      {/* İptal Modal */}
      <Modal
        title="Tahsilatı İptal Et"
        open={!!cancelOpen}
        onCancel={() => { setCancelOpen(null); cancelForm.resetFields() }}
        confirmLoading={cancelMutation.isPending}
        okText="İptal Et"
        okButtonProps={{ danger: true }}
        cancelText="Vazgeç"
        onOk={() =>
          cancelForm.validateFields().then((values) => {
            cancelMutation.mutate({ paymentId: cancelOpen!, reason: values.reason as string })
          })
        }
      >
        <Alert
          type="warning"
          message="Tahsilat iptal edildiğinde cari hesaptan düşürülür. Bu işlem geri alınamaz."
          showIcon
          style={{ marginBottom: 16 }}
        />
        <Form form={cancelForm} layout="vertical">
          <Form.Item
            name="reason"
            label="İptal Gerekçesi"
            rules={[{ required: true, message: 'Gerekçe zorunludur (denetim izi).' }]}
          >
            <Input.TextArea rows={3} maxLength={500} showCount />
          </Form.Item>
        </Form>
      </Modal>
    </>
  )
}

// ────────────────────────────────────────────────────────────────────────────
// Tab 2 — Cari Hesap (Ledger)
// ────────────────────────────────────────────────────────────────────────────

function LedgerTab() {
  const [selectedApartmentId, setSelectedApartmentId] = useState<string | null>(null)
  const [asOfDate, setAsOfDate] = useState<Dayjs | null>(null)

  const apartmentsQuery = useQuery({
    queryKey: ['apartments'],
    queryFn: async () => (await apiClient.get<ApartmentListItem[]>('/apartments')).data,
  })

  const ledgerQuery = useQuery({
    queryKey: ['ledger', selectedApartmentId, asOfDate?.format('YYYY-MM-DD')],
    queryFn: async () => {
      const params = asOfDate ? `?asOfDate=${asOfDate.format('YYYY-MM-DD')}` : ''
      return (
        await apiClient.get<LedgerResult>(
          `/payments/ledger/${selectedApartmentId}${params}`,
        )
      ).data
    },
    enabled: !!selectedApartmentId,
  })

  const ledger = ledgerQuery.data

  // Yıllık faiz oranı (Ayarlar) — faiz detay notu için
  const settingsQuery = useQuery({
    queryKey: ['settings'],
    queryFn: async () => (await apiClient.get<{ annualInterestRate?: number }>('/settings')).data,
    enabled: !!selectedApartmentId,
  })
  const annualRate = settingsQuery.data?.annualInterestRate
  const rateLabel = typeof annualRate === 'number' ? annualRate.toFixed(2).replace('.', ',') : 'Oran'

  // Hesap hareketleri: kronolojik Devir (Anapara+Faiz) / Fatura / Faiz / Tahsilat
  // (FIFO mahsup edilmiş haliyle). Devir ANAPARA satırında faiz detay notu gösterilir;
  // Devir FAİZ satırı kapalı borçtur — ona faiz işlemez ve hesap notu konmaz.
  const movements: MovementRow[] = []
  if (ledger) {
    for (const l of ledger.lines) {
      if (l.type === 'OpeningBalanceInterest') {
        movements.push({ key: l.sourceId, date: l.dueDate, type: 'Devir Faiz', description: 'Devreden Geçmiş Faiz', debit: l.principalAmount, credit: 0, interestDays: null })
        continue
      }
      if (l.type === 'OpeningBalancePrincipal') {
        const devirDays = Math.max(0, dayjs(ledger.asOfDate).diff(dayjs(l.dueDate), 'day'))
        movements.push({ key: l.sourceId, date: l.dueDate, type: 'Devir Anapara', description: 'Devir Anapara (Devreden Bakiye)', debit: l.principalAmount, credit: 0, interestDays: devirDays })
        if (l.interestAccrued > 0) {
          movements.push({ key: `faiz-${l.sourceId}`, date: ledger.asOfDate, type: 'Faiz', description: 'Devir Anapara — Gecikme Faizi', debit: l.interestAccrued, credit: 0, interestDays: devirDays })
        }
        continue
      }
      movements.push({ key: `fatura-${l.sourceId}`, date: l.dueDate, type: 'Fatura', description: translatePeriodLabel(l.label), debit: l.principalAmount, credit: 0, interestDays: null })
      if (l.interestAccrued > 0) {
        const days = Math.max(0, dayjs(ledger.asOfDate).diff(dayjs(l.dueDate), 'day'))
        movements.push({ key: `faiz-${l.sourceId}`, date: ledger.asOfDate, type: 'Faiz', description: `${translatePeriodLabel(l.label)} — Gecikme Faizi`, debit: l.interestAccrued, credit: 0, interestDays: days })
      }
      for (const al of l.allocations) {
        movements.push({ key: `tahsilat-${al.paymentId}-${l.sourceId}`, date: al.paymentDate, type: 'Tahsilat', description: `Tahsilat — ${al.target}`, debit: 0, credit: al.allocatedAmount, interestDays: null })
      }
    }
  }
  movements.sort((a, b) => {
    const isDevir = (t: MovementRow['type']) => t === 'Devir Faiz' || t === 'Devir Anapara'
    if (isDevir(a.type) && !isDevir(b.type)) return -1
    if (!isDevir(a.type) && isDevir(b.type)) return 1
    if (isDevir(a.type) && isDevir(b.type) && a.type !== b.type) return a.type === 'Devir Faiz' ? -1 : 1
    return dayjs(a.date).valueOf() - dayjs(b.date).valueOf()
  })
  let runningTotal = 0
  const movementRows: MovementRowWithBalance[] = movements.map((m) => {
    runningTotal += m.debit - m.credit
    return { ...m, runningBalance: runningTotal }
  })

  // Bakiye özeti kartları
  const openingBalance = ledger
    ? ledger.lines
        .filter((l) => l.type === 'OpeningBalancePrincipal' || l.type === 'OpeningBalanceInterest')
        .reduce((s, l) => s + l.lineBalance, 0)
    : 0
  const periodAccrual = ledger ? ledger.lines.filter((l) => l.type === 'Invoice').reduce((s, l) => s + l.principalAmount + l.interestAccrued, 0) : 0
  const totalCollected = ledger ? ledger.lines.reduce((s, l) => s + l.allocations.reduce((a, al) => a + al.allocatedAmount, 0), 0) : 0

  const apartmentOptions =
    apartmentsQuery.data?.map((a) => ({
      value: a.id,
      label: `${a.doorNumber} — ${a.title}`,
    })) ?? []

  return (
    <>
      <Row gutter={12} style={{ marginBottom: 16 }}>
        <Col span={12}>
          <Select
            style={{ width: '100%' }}
            placeholder="Daire seçin…"
            showSearch
            optionFilterProp="label"
            options={apartmentOptions}
            loading={apartmentsQuery.isLoading}
            onChange={(v: string) => setSelectedApartmentId(v)}
          />
        </Col>
        <Col span={8}>
          <DatePicker
            style={{ width: '100%' }}
            format="DD.MM.YYYY"
            placeholder="Bugün (varsayılan)"
            onChange={(d) => setAsOfDate(d)}
          />
        </Col>
      </Row>

      {ledger && (
        <>
          <Row gutter={16} style={{ marginBottom: 16 }}>
            <Col span={6}>
              <Card size="small">
                <Statistic
                  title="Devreden Bakiye"
                  value={openingBalance}
                  precision={2}
                  suffix="TL"
                  valueStyle={{ color: openingBalance > 0 ? '#cf1322' : '#3f8600' }}
                />
              </Card>
            </Col>
            <Col span={6}>
              <Card size="small">
                <Statistic
                  title="Bu Dönem Tahakkuk (Fatura + Faiz)"
                  value={periodAccrual}
                  precision={2}
                  suffix="TL"
                  valueStyle={{ color: '#cf1322' }}
                />
              </Card>
            </Col>
            <Col span={6}>
              <Card size="small">
                <Statistic
                  title="Yapılan Tahsilat"
                  value={totalCollected}
                  precision={2}
                  suffix="TL"
                  valueStyle={{ color: '#3f8600' }}
                />
              </Card>
            </Col>
            <Col span={6}>
              <Card size="small">
                <Statistic
                  title="Güncel Net Bakiye"
                  value={ledger.totalBalance}
                  precision={2}
                  suffix="TL"
                  valueStyle={{ color: ledger.totalBalance > 0 ? '#cf1322' : '#3f8600' }}
                />
                <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                  Hesaplama: {dateFmt(ledger.asOfDate)}
                </Typography.Text>
              </Card>
            </Col>
          </Row>

          <Table<MovementRowWithBalance>
            rowKey="key"
            size="small"
            dataSource={movementRows}
            pagination={{ pageSize: 15, showSizeChanger: false }}
            columns={[
              { title: 'Tarih', dataIndex: 'date', width: 110, render: (v: string) => dateFmt(v) },
              {
                title: 'İşlem Türü',
                dataIndex: 'type',
                width: 110,
                render: (v: MovementRow['type']) =>
                  v === 'Devir Anapara' ? <Tag color="purple">Devir Anapara</Tag>
                  : v === 'Devir Faiz' ? <Tag color="volcano">Devir Faiz</Tag>
                  : v === 'Fatura' ? <Tag color="blue">Fatura</Tag>
                  : v === 'Faiz' ? <Tag color="orange">Faiz</Tag>
                  : <Tag color="green">Tahsilat</Tag>,
              },
              {
                title: 'Açıklama',
                dataIndex: 'description',
                render: (v: string, r) => (
                  <div>
                    <div>{v}</div>
                    {(r.type === 'Faiz' || r.type === 'Devir Anapara') && r.interestDays !== null && (
                      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {r.interestDays} Gün × (Yıllık %{rateLabel} / 365) — günlük basit faiz: Anapara × Oran × Gün / 365
                      </Typography.Text>
                    )}
                  </div>
                ),
              },
              {
                title: 'Borç (+)',
                dataIndex: 'debit',
                width: 130,
                align: 'right',
                render: (v: number) =>
                  v > 0 ? <Typography.Text type="danger">{tl(v)}</Typography.Text> : <Typography.Text type="secondary">—</Typography.Text>,
              },
              {
                title: 'Alacak (−)',
                dataIndex: 'credit',
                width: 130,
                align: 'right',
                render: (v: number) =>
                  v > 0 ? <Typography.Text type="success">{tl(v)}</Typography.Text> : <Typography.Text type="secondary">—</Typography.Text>,
              },
              {
                title: 'Yürüyen Bakiye',
                dataIndex: 'runningBalance',
                width: 140,
                align: 'right',
                render: (v: number) => (
                  <Typography.Text strong type={v > 0 ? 'danger' : 'success'}>{tl(v)}</Typography.Text>
                ),
              },
            ]}
          />
        </>
      )}

      {!selectedApartmentId && (
        <Typography.Text type="secondary">Cari hesap görüntülemek için daire seçin.</Typography.Text>
      )}
    </>
  )
}

// ────────────────────────────────────────────────────────────────────────────
// Tab 3 — Nakit Akış Raporu
// ────────────────────────────────────────────────────────────────────────────

function CashFlowTab() {
  const [dateRange, setDateRange] = useState<[Dayjs, Dayjs] | null>(null)
  const [cashFlow, setCashFlow] = useState<CashFlowResult | null>(null)
  const [loading, setLoading] = useState(false)

  const handleQuery = async () => {
    if (!dateRange) { message.warning('Tarih aralığı seçin.'); return }
    setLoading(true)
    try {
      const res = await apiClient.get<CashFlowResult>('/payments/cash-flow', {
        params: {
          dateFrom: dateRange[0].format('YYYY-MM-DD'),
          dateTo: dateRange[1].format('YYYY-MM-DD'),
        },
      })
      setCashFlow(res.data)
    } finally {
      setLoading(false)
    }
  }

  return (
    <>
      <Row gutter={12} style={{ marginBottom: 16 }}>
        <Col>
          <DatePicker.RangePicker
            format="DD.MM.YYYY"
            onChange={(vals) => setDateRange(vals as [Dayjs, Dayjs] | null)}
          />
        </Col>
        <Col>
          <Button
            type="primary"
            icon={<SwapOutlined />}
            loading={loading}
            onClick={() => void handleQuery()}
          >
            Raporu Getir
          </Button>
        </Col>
      </Row>

      {cashFlow && (
        <>
          <Row gutter={16} style={{ marginBottom: 16 }}>
            <Col span={6}>
              <Card size="small">
                <Statistic
                  title="Toplam Gelir (Tahsilat)"
                  value={cashFlow.totalIncome}
                  precision={2}
                  suffix="TL"
                  valueStyle={{ color: '#3f8600' }}
                />
              </Card>
            </Col>
            <Col span={6}>
              <Card size="small">
                <Statistic
                  title="Toplam Gider (Doğalgaz)"
                  value={cashFlow.totalExpense}
                  precision={2}
                  suffix="TL"
                  valueStyle={{ color: '#cf1322' }}
                />
              </Card>
            </Col>
            <Col span={6}>
              <Card size="small">
                <Statistic
                  title="Net Bakiye"
                  value={cashFlow.netBalance}
                  precision={2}
                  suffix="TL"
                  valueStyle={{ color: cashFlow.netBalance >= 0 ? '#3f8600' : '#cf1322' }}
                />
              </Card>
            </Col>
            <Col span={6}>
              <Card size="small">
                <Descriptions size="small" column={1}>
                  <Descriptions.Item label="Dönem Başı">{dateFmt(cashFlow.dateFrom)}</Descriptions.Item>
                  <Descriptions.Item label="Dönem Sonu">{dateFmt(cashFlow.dateTo)}</Descriptions.Item>
                </Descriptions>
              </Card>
            </Col>
          </Row>

          <Table
            rowKey={(r, i) => `${r.date}-${i}`}
            size="small"
            dataSource={cashFlow.lines}
            pagination={{ pageSize: 20 }}
            columns={[
              { title: 'Tarih', dataIndex: 'date', width: 110, render: (v: string) => dateFmt(v) },
              {
                title: 'Yön',
                dataIndex: 'direction',
                width: 100,
                render: (v: string) =>
                  v === 'Income'
                    ? <Tag color="green" icon={<DollarOutlined />}>GELİR</Tag>
                    : <Tag color="red" icon={<MinusCircleOutlined />}>GİDER</Tag>,
              },
              { title: 'Açıklama', dataIndex: 'description' },
              {
                title: 'Tutar',
                dataIndex: 'amount',
                width: 130,
                align: 'right',
                render: (v: number, r) => (
                  <Typography.Text type={r.direction === 'Income' ? 'success' : 'danger'}>
                    {tl(v)}
                  </Typography.Text>
                ),
              },
              { title: 'Referans', dataIndex: 'reference', render: (v: string | null) => v ?? '—' },
            ]}
          />
        </>
      )}
    </>
  )
}
