import { AppModal } from '../../components/AppModal'
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
  Popconfirm,
  Row,
  Segmented,
  Select,
  Space,
  Statistic,
  Table,
  Tag,
  Tabs,
  Typography,
  message,
} from 'antd'
import {
  DeleteOutlined,
  DollarOutlined,
  EditOutlined,
  FilePdfOutlined,
  MinusCircleOutlined,
  PlusOutlined,
  StopOutlined,
  SwapOutlined,
} from '@ant-design/icons'
import type { ColumnsType } from 'antd/es/table'
import dayjs, { type Dayjs } from 'dayjs'
import { apiClient } from '../../api/client'
import { downloadFile } from '../../api/download'
import { useAuth } from '../auth/AuthContext'
import { BankAccountSelect } from '../bankAccounts/BankAccountSelect'
import { formatDate as dateFmt } from '../../utils/dateUtils'
import type {
  ApartmentListItem,
  CashFlowResult,
  LedgerResult,
  PaymentListItem,
  PaymentTargetDebtType,
  RecordPaymentRequest,
  UpdatePaymentRequest,
} from '../../types/api'

const tl = (v: number) => v.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })

/** Tahsilat modalındaki ödeme hedefi. Auto dışındakiler backend LedgerDebtType adlarıdır. */
const PaymentTarget = {
  Auto: 'Auto',
  Aidat: 'Aidat',
  Heating: 'Invoice',
  OpeningBalance: 'OpeningBalancePrincipal',
} as const

type PaymentTargetValue = (typeof PaymentTarget)[keyof typeof PaymentTarget]

const PAYMENT_TARGET_OPTIONS: { value: PaymentTargetValue; label: string }[] = [
  { value: PaymentTarget.Auto, label: 'Otomatik (en eski borç)' },
  { value: PaymentTarget.Aidat, label: 'Aidat' },
  { value: PaymentTarget.Heating, label: 'Isınma' },
  { value: PaymentTarget.OpeningBalance, label: 'Devir' },
]

function resolveTargetDebtType(target: PaymentTargetValue): RecordPaymentRequest['targetDebtType'] {
  if (target === PaymentTarget.Auto) return null
  return target
}

function toFormTarget(type: PaymentTargetDebtType | null | undefined): PaymentTargetValue {
  if (type === PaymentTarget.Aidat || type === PaymentTarget.Heating || type === PaymentTarget.OpeningBalance) {
    return type
  }
  if (type === 'OpeningBalanceInterest') return PaymentTarget.OpeningBalance
  return PaymentTarget.Auto
}

function paymentTargetLabel(type: PaymentTargetDebtType | null | undefined): string {
  return PAYMENT_TARGET_OPTIONS.find((option) => option.value === toFormTarget(type))?.label ?? 'Otomatik (en eski borç)'
}

/**
 * Faz 7.1: Daire seçici etiketi — sakin adı ve rolüyle birlikte.
 * Örnek: "1 — Daire 1 (Ahmet Yılmaz · Kiracı)" / "2 — Daire 2 (Ayşe Kaya · Ev Sahibi)".
 */
function apartmentLabel(a: ApartmentListItem): string {
  const person = a.kiraciName ?? a.ownerName
  const role = a.kiraciName ? 'Kiracı' : a.ownerName ? 'Ev Sahibi' : null
  return person && role
    ? `${a.doorNumber} — ${a.title} (${person} · ${role})`
    : `${a.doorNumber} — ${a.title}`
}

// Backend'den İngilizce ay adı gelirse (kısa veya tam) TAM Türkçe ay adına çevrilir.
const EN_TO_TR_MONTHS: Record<string, string> = {
  January: 'Ocak', February: 'Şubat', March: 'Mart', April: 'Nisan', May: 'Mayıs', June: 'Haziran',
  July: 'Temmuz', August: 'Ağustos', September: 'Eylül', October: 'Ekim', November: 'Kasım', December: 'Aralık',
  Jan: 'Ocak', Feb: 'Şubat', Mar: 'Mart', Apr: 'Nisan', Jun: 'Haziran',
  Jul: 'Temmuz', Aug: 'Ağustos', Sep: 'Eylül', Sept: 'Eylül', Oct: 'Ekim', Nov: 'Kasım', Dec: 'Aralık',
}

/** "Apr 2026 - May 2026" → "Nisan 2026 - Mayıs 2026" (Türkçe etiketler olduğu gibi kalır). */
function translatePeriodLabel(label: string): string {
  return label.replace(
    /(?<![\p{L}])(January|February|March|April|May|June|July|August|September|October|November|December|Jan|Feb|Mar|Apr|Jun|Jul|Aug|Sept|Sep|Oct|Nov|Dec)(?![\p{L}])/gu,
    (m) => EN_TO_TR_MONTHS[m] ?? m,
  )
}

// Cari hesap hareket satırı (Devir Anapara / Devir Faiz / Fatura / Aidat / Faiz / Tahsilat)
interface MovementRow {
  key: string
  date: string
  type: 'Devir Anapara' | 'Devir Alacağı' | 'Devir Faiz' | 'Fatura' | 'Aidat' | 'Faiz' | 'Tahsilat' | 'Görev Mahsubu'
  description: string
  debit: number
  credit: number
  interestDetail: string | null
  isCancelled?: boolean
  originalAmount?: number | null
  performedBy?: string | null
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
  const { hasPermission } = useAuth()
  const canCreate = hasPermission('Payments.Create')
  const canCancel = hasPermission('Payments.Cancel')
  const [selectedApartmentId, setSelectedApartmentId] = useState<string | null>(null)
  const [addOpen, setAddOpen] = useState(false)
  const [editingPayment, setEditingPayment] = useState<PaymentListItem | null>(null)
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
      setEditingPayment(null)
      form.resetFields()
      message.success('Tahsilat kaydedildi.')
    },
  })

  const updateMutation = useMutation({
    mutationFn: async ({ paymentId, body }: { paymentId: string; body: UpdatePaymentRequest }) =>
      (await apiClient.put(`/payments/${paymentId}`, body)).data,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['payments', selectedApartmentId] })
      void queryClient.invalidateQueries({ queryKey: ['ledger', selectedApartmentId] })
      setAddOpen(false)
      setEditingPayment(null)
      form.resetFields()
      message.success('Tahsilat güncellendi.')
    },
  })

  const deleteMutation = useMutation({
    mutationFn: async (paymentId: string) =>
      (await apiClient.delete(`/payments/${paymentId}`)).data,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['payments', selectedApartmentId] })
      void queryClient.invalidateQueries({ queryKey: ['ledger', selectedApartmentId] })
      message.success('Tahsilat silindi.')
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
      label: apartmentLabel(a),
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
    {
      title: 'Banka',
      dataIndex: 'bankAccountLabel',
      width: 180,
      render: (v: string | null) => v || '—',
    },
    {
      title: 'Ödeme Hedefi',
      dataIndex: 'targetDebtType',
      width: 160,
      render: (v: PaymentListItem['targetDebtType']) => paymentTargetLabel(v),
    },
    {
      title: 'Girişi Yapan',
      dataIndex: 'receivedBy',
      width: 140,
      render: (v: string | null) => v?.trim() ? v : '—',
    },
    {
      title: 'İşlemi Yapan',
      dataIndex: 'createdBy',
      width: 180,
      render: (v: string | null) => v?.trim() ? v : '—',
    },
    {
      title: 'Durum',
      dataIndex: 'isCancelled',
      width: 110,
      render: (v: boolean, r: PaymentListItem) =>
        v ? (
          <Tag color="red" title={r.lastModifiedBy ? `İptal eden: ${r.lastModifiedBy}` : undefined}>
            İptal
          </Tag>
        ) : (
          <Tag color="green">Aktif</Tag>
        ),
    },
    {
      title: 'İşlem',
      fixed: 'right',
      width: 250,
      render: (_: unknown, r: PaymentListItem) => (
        <Space size={4} wrap>
          <Button
            size="small"
            icon={<FilePdfOutlined />}
            onClick={() => downloadFile(`/payments/${r.id}/receipt.pdf`, 'Makbuz.pdf')}
          >
            Makbuz
          </Button>
          {canCreate && !r.isCancelled && (
            <Button
              size="small"
              icon={<EditOutlined />}
              onClick={() => {
                setEditingPayment(r)
                form.setFieldsValue({
                  paymentDate: dayjs(r.paymentDate),
                  amount: r.amount,
                  paymentTarget: toFormTarget(r.targetDebtType),
                  description: r.description ?? undefined,
                  receivedBy: r.receivedBy,
                  receiptNumber: r.receiptNumber ?? undefined,
                  bankAccountId: r.bankAccountId ?? undefined,
                })
                setAddOpen(true)
              }}
            >
              Düzenle
            </Button>
          )}
          {canCancel && <Popconfirm
            title="Tahsilat silinsin mi?"
            description="Kayıt listeden ve cari hesaptan kalkar."
            okText="Sil"
            cancelText="Vazgeç"
            okButtonProps={{ danger: true }}
            onConfirm={() => deleteMutation.mutate(r.id)}
          >
            <Button size="small" danger icon={<DeleteOutlined />} loading={deleteMutation.isPending}>
              Sil
            </Button>
          </Popconfirm>}
          {canCancel && !r.isCancelled && (
            <Button
              size="small"
              icon={<StopOutlined />}
              onClick={() => setCancelOpen(r.id)}
            >
              İptal
            </Button>
          )}
          {r.isCancelled && (
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {r.cancellationReason}
            </Typography.Text>
          )}
        </Space>
      ),
    },
  ]

  return (
    <>
      <Row gutter={12} style={{ marginBottom: 16 }}>
        <Col xs={24} md={14}>
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
        <Col xs={24} md={10}>
          {canCreate && <Button
            type="primary"
            icon={<PlusOutlined />}
            disabled={!selectedApartmentId}
            onClick={() => {
              setEditingPayment(null)
              form.resetFields()
              setAddOpen(true)
            }}
          >
            Tahsilat Ekle
          </Button>}
        </Col>
      </Row>

      <Table
        scroll={{ x: 'max-content' }}
        rowKey="id"
        size="small"
        columns={columns}
        dataSource={paymentsQuery.data}
        loading={paymentsQuery.isLoading}
        pagination={{ pageSize: 10 }}
        locale={{ emptyText: selectedApartmentId ? 'Tahsilat kaydı yok.' : 'Önce daire seçin.' }}
      />

      {/* Tahsilat Ekleme Modal */}
      <AppModal
        title={editingPayment ? 'Tahsilatı Düzenle' : 'Tahsilat Ekle'}
        open={addOpen}
        onCancel={() => { setAddOpen(false); setEditingPayment(null); form.resetFields() }}
        confirmLoading={recordMutation.isPending || updateMutation.isPending}
        okText="Kaydet"
        cancelText="İptal"
        onOk={() =>
          form.validateFields().then((values) => {
            const body = {
              paymentDate: (values.paymentDate as Dayjs).format('YYYY-MM-DD'),
              amount: values.amount as number,
              description: (values.description as string | null) ?? null,
              receivedBy: (values.receivedBy as string | null) ?? null,
              receiptNumber: ((values.receiptNumber as string | null) ?? '').trim() || null,
              targetDebtType: resolveTargetDebtType(values.paymentTarget as PaymentTargetValue),
              bankAccountId: (values.bankAccountId as string | null) || null,
            }
            if (editingPayment) {
              updateMutation.mutate({ paymentId: editingPayment.id, body })
              return
            }
            recordMutation.mutate({
              apartmentId: selectedApartmentId!,
              ...body,
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
          <Form.Item
            name="paymentTarget"
            label="Ödeme Hedefi"
            rules={[{ required: true, message: 'Ödeme hedefi seçilmelidir.' }]}
            extra="Seçilen türe mahsup edilir. Aidat yalnızca aidat borçlarından, ısınma yalnızca doğalgaz faturalarından, devir yalnızca devir bakiyesinden düşer."
          >
            <Select
              placeholder="Ödeme hedefi seçin"
              options={PAYMENT_TARGET_OPTIONS}
            />
          </Form.Item>
          <Form.Item name="bankAccountId" label="Banka Hesabı">
            <BankAccountSelect includeId={editingPayment?.bankAccountId} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="description" label="Açıklama / Dekont No">
            <Input maxLength={500} placeholder="İsteğe bağlı" />
          </Form.Item>
          <Form.Item name="receivedBy" label="Tahsilat Yapan">
            <Input maxLength={100} placeholder="İsteğe bağlı — Faz 7'de JWT'den otomatik dolar" />
          </Form.Item>
          <Form.Item name="receiptNumber" label="Makbuz No" extra="Boş bırakılırsa yıl sırasından üretilir. Örnek: 2026-0001">
            <Input maxLength={30} placeholder="Otomatik" />
          </Form.Item>
        </Form>
      </AppModal>

      {/* İptal Modal */}
      <AppModal
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
      </AppModal>
    </>
  )
}

// ────────────────────────────────────────────────────────────────────────────
// Tab 2 — Cari Hesap (Ledger)
// ────────────────────────────────────────────────────────────────────────────

function LedgerTab() {
  const [selectedApartmentId, setSelectedApartmentId] = useState<string | null>(null)
  const [asOfDate, setAsOfDate] = useState<Dayjs | null>(null)
  const [residentFilter, setResidentFilter] = useState<string | null>(null)
  const [typeFilter, setTypeFilter] = useState<'all' | 'isinma' | 'aidat' | 'devir'>('all')

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
    queryKey: ['settings-interest'],
    queryFn: async () => (await apiClient.get<{ interestRate?: number }>('/settings/interest')).data,
    enabled: !!selectedApartmentId,
  })
  const annualRate = settingsQuery.data?.interestRate
  const rateLabel = typeof annualRate === 'number' ? annualRate.toFixed(2).replace('.', ',') : 'Oran'
  // Backend CalculatePiecewiseInterest ile aynı kronolojik parçalama: her tahsilat tarihinde faiz
  // durur, kalan anapara için işleme devam eder. Ödenen kısım "ödeme tarihine kadar", ödenmemiş
  // kalan kısım "bugüne kadar" gün sayısıyla açıklanır.
  const interestDetail = (line: LedgerResult['lines'][number], asOfDate: string) => {
    const asOf = dayjs(asOfDate)
    const due = dayjs(line.dueDate)
    const allocations = [...(line.allocations ?? [])].sort(
      (a, b) => dayjs(a.paymentDate).valueOf() - dayjs(b.paymentDate).valueOf(),
    )
    let running = line.remainingPrincipal + line.paidAmount
    let prev = due
    const parts: string[] = []
    for (const al of allocations) {
      let payDay = dayjs(al.paymentDate)
      if (payDay.isBefore(due)) payDay = due
      if (!payDay.isBefore(asOf)) break
      const days = payDay.diff(prev, 'day')
      if (days > 0 && running > 0) {
        parts.push(`Vade ${dateFmt(prev.format('YYYY-MM-DD'))} · ${dateFmt(al.paymentDate)} tahsilatı ile kapanmıştır (${days} gün faiz işlemiştir) · anapara ${tl(running)}`)
      }
      running = Math.max(0, running - al.allocatedAmount)
      prev = payDay
    }
    if (running > 0 && asOf.isAfter(prev)) {
      const days = asOf.diff(prev, 'day')
      parts.push(
        allocations.length > 0
          ? `Kalan ${tl(running)} için bugüne kadar ${days} gün faiz işlemiştir`
          : `Vade ${dateFmt(line.dueDate)} · kalan anapara ${tl(running)} · bugüne kadar ${days} gün`,
      )
    }
    parts.push(`yıllık %${rateLabel} / 365`)
    return parts.join(' · ')
  }

  const activeResidentFilter = residentFilter && residentFilter !== '__all__' ? residentFilter : null

  const typeMatches = (t: LedgerResult['lines'][number]['type']) =>
    typeFilter === 'all'
      ? true
      : typeFilter === 'isinma'
        ? t === 'Invoice'
        : typeFilter === 'aidat'
          ? t === 'Aidat' || t === 'WaiverOffset'
          : t === 'OpeningBalancePrincipal' || t === 'OpeningBalanceInterest'

  // Hesap hareketleri: kronolojik Devir (Anapara+Faiz) / Fatura / Faiz / Tahsilat
  // (FIFO mahsup edilmiş haliyle). Devir ANAPARA satırında faiz detay notu gösterilir;
  // Devir FAİZ satırı kapalı borçtur — ona faiz işlemez ve hesap notu konmaz.
  const movements: MovementRow[] = []
  if (ledger) {
    // Faz 7.1: Sakin + borç türü filtreleri satır bazında uygulanır (Faiz/Tahsilat alt
    // satırları kendi borç satırlarıyla birlikte filtrelenir).
    const filteredLines = ledger.lines.filter(
      (l) => typeMatches(l.type) && (!activeResidentFilter || l.ownerId === activeResidentFilter),
    )
    for (const l of filteredLines) {
      if (l.type === 'WaiverOffset') {
        continue
      }
      if (l.type === 'OpeningBalanceInterest') {
        movements.push({ key: l.sourceId, date: l.dueDate, type: 'Devir Faiz', description: 'Devreden Geçmiş Faiz', debit: l.principalAmount, credit: 0, interestDetail: null })
        continue
      }
      if (l.type === 'OpeningBalancePrincipal') {
        if (l.principalAmount < 0) {
          const creditAmt = Math.abs(l.principalAmount)
          const usedAmt = l.paidAmount
          const remainAmt = Math.abs(l.remainingPrincipal)
          movements.push({
            key: l.sourceId,
            date: l.dueDate,
            type: 'Devir Alacağı',
            description: 'Devir Alacağı (Devreden Fazla Ödeme)',
            debit: 0,
            credit: creditAmt,
            interestDetail: usedAmt > 0
              ? `Dönem borçlarına mahsup edilen: ${tl(usedAmt)} · Kalan alacak: ${tl(remainAmt)}`
              : `Kullanılabilir devir alacağı: ${tl(remainAmt)}`,
          })
          continue
        }
        movements.push({ key: l.sourceId, date: l.dueDate, type: 'Devir Anapara', description: 'Devir Anapara (Devreden Bakiye)', debit: l.principalAmount, credit: 0, interestDetail: interestDetail(l, ledger.asOfDate) })
        if (l.interestAccrued > 0) {
          movements.push({ key: `faiz-${l.sourceId}`, date: l.dueDate, type: 'Faiz', description: 'Devir Anapara — Gecikme Faizi', debit: l.interestAccrued, credit: 0, interestDetail: interestDetail(l, ledger.asOfDate) })
        }
        continue
      }
      // Faz 6.9: Aidat satırları kendi tipiyle (mor etiket) gösterilir.
      movements.push({
        key: `${l.type === 'Aidat' ? 'aidat' : 'fatura'}-${l.sourceId}`,
        date: l.type === 'Aidat' && l.periodStart ? l.periodStart : l.dueDate,
        type: l.type === 'Aidat' ? 'Aidat' : 'Fatura',
        description: translatePeriodLabel(l.label),
        debit: l.principalAmount,
        credit: 0,
        interestDetail: l.isCancelled && l.originalPrincipalAmount
          ? `İptal öncesi tutar: ${tl(l.originalPrincipalAmount)} · Denetim izi (bakiye etkisi ₺0,00)`
          : null,
        isCancelled: l.isCancelled,
        originalAmount: l.originalPrincipalAmount,
      })
      if (l.type === 'Aidat') {
        for (const waiver of filteredLines) {
          if (
            waiver.type === 'WaiverOffset' &&
            (waiver.sourceId === `waiver:${l.sourceId}` || waiver.sourceId.startsWith(`waiver:${l.sourceId}:`))
          ) {
            const excessCredit = waiver.remainingPrincipal < 0 ? Math.abs(waiver.remainingPrincipal) : 0
            const waiverNote =
              waiver.isCancelled && waiver.originalPrincipalAmount
                ? `İptal edilen mahsup: ${tl(waiver.originalPrincipalAmount)} · Borç daireye geri eklendi (alacak etkisi ₺0,00)`
                : excessCredit > 0
                  ? `Akıllı Mahsup — Borçlardan düşülen (Aidat + Isınma): ${tl(waiver.paidAmount)} · Artan Cari Hesap Alacağı: ${tl(excessCredit)}`
                  : waiver.principalAmount > l.principalAmount
                    ? `Akıllı Mahsup — Aidat ve Isınma borçlarından toplam ${tl(waiver.paidAmount)} mahsup edildi`
                    : null

            movements.push({
              key: `mahsup-${waiver.sourceId}`,
              date: waiver.periodStart ?? waiver.dueDate,
              type: 'Görev Mahsubu',
              description: waiver.label,
              debit: 0,
              credit: waiver.principalAmount,
              interestDetail: waiverNote,
              isCancelled: waiver.isCancelled,
              originalAmount: waiver.originalPrincipalAmount,
            })
          }
        }
      }
      if (l.interestAccrued > 0) {
        const label = translatePeriodLabel(l.label)
        movements.push({
          key: `faiz-${l.sourceId}`,
          date: l.dueDate,
          type: 'Faiz',
          description: `${label} — Gecikme Faizi`,
          debit: l.interestAccrued,
          credit: 0,
          interestDetail: interestDetail(l, ledger.asOfDate),
        })
      }
      for (const al of l.allocations ?? []) {
        // Negatif devir alacağından otomatik yapılan mahsup (paymentId == Guid.Empty) zaten en üstte
        // "Devir Alacağı" olarak yürüyen bakiyeden düşüldüğü için ikinci kez Tahsilat satırı eklenmez.
        if (al.paymentId === '00000000-0000-0000-0000-000000000000') {
          continue
        }
        const targetLabel = al.target === 'Invoice'
          ? 'Isınma'
          : al.target === 'Aidat'
            ? 'Aidat'
            : al.target === 'OpeningBalance'
              ? 'Devir Anapara'
              : al.target === 'Interest'
                ? 'Devir Faiz'
                : al.target
        movements.push({
          key: `tahsilat-${al.paymentId}-${l.sourceId}`,
          date: al.paymentDate,
          type: 'Tahsilat',
          description: `Tahsilat — ${targetLabel}`,
          debit: 0,
          credit: al.allocatedAmount,
          interestDetail: null,
          performedBy: al.performedBy,
        })
      }
    }
    if (typeFilter === 'all' && !activeResidentFilter) {
      for (const credit of ledger.credits ?? []) {
        movements.push({
          key: `alacak-${credit.paymentId}`,
          date: credit.paymentDate,
          type: 'Tahsilat',
          description: 'Tahsilat — Alacak',
          debit: 0,
          credit: credit.amount,
          interestDetail: null,
          performedBy: credit.performedBy,
        })
      }
    }
  }
  movements.sort((a, b) => {
    const isDevir = (t: MovementRow['type']) => t === 'Devir Faiz' || t === 'Devir Anapara' || t === 'Devir Alacağı'
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
        .reduce((s, l) => s + l.principalAmount, 0)
    : 0
  // Faz 6.9: Aidat tahakkukları da dönem tahakkuk özetine dahil.
  const periodAccrual = ledger
    ? ledger.lines
        .filter((l) => l.type === 'Invoice' || l.type === 'Aidat')
        .reduce((s, l) => s + l.principalAmount + l.interestAccrued, 0)
    : 0
  const totalCollected = ledger
    ? ledger.lines.reduce(
        (s, l) =>
          s +
          (l.allocations ?? [])
            .filter((al) => al.paymentId !== '00000000-0000-0000-0000-000000000000')
            .reduce((a, al) => a + al.allocatedAmount, 0),
        0,
      ) + (ledger.credits ?? []).reduce((s, c) => s + c.amount, 0)
    : 0

  // Faz 7.1: Sakin seçici + borç türü filtresi + tür bazlı özet kartları.
  const residentOptions = ledger
    ? [
        { value: '__all__', label: 'Tüm Sakinler' },
        ...Array.from(
          new Map(
            ledger.lines
              .filter((l) => l.ownerId)
              .map((l) => [l.ownerId as string, l.ownerName ?? 'Sakin']),
          ).entries(),
        ).map(([id, name]) => ({ value: id, label: name })),
      ]
    : [{ value: '__all__', label: 'Tüm Sakinler' }]

  const apartmentOptions =
    apartmentsQuery.data?.map((a) => ({
      value: a.id,
      label: apartmentLabel(a),
    })) ?? []

  return (
    <>
      <Row gutter={12} style={{ marginBottom: 16 }}>
        <Col xs={24} md={10}>
          <Select
            style={{ width: '100%' }}
            placeholder="Daire seçin…"
            showSearch
            optionFilterProp="label"
            options={apartmentOptions}
            loading={apartmentsQuery.isLoading}
            onChange={(v: string) => {
              setSelectedApartmentId(v)
              setResidentFilter(null)
              setTypeFilter('all')
            }}
          />
        </Col>
        <Col xs={24} md={5}>
          <DatePicker
            style={{ width: '100%' }}
            format="DD.MM.YYYY"
            placeholder="Bugün (varsayılan)"
            onChange={(d) => setAsOfDate(d)}
          />
        </Col>
        <Col xs={24} md={5}>
          <Select
            value={residentFilter ?? '__all__'}
            options={residentOptions}
            onChange={(v: string) => setResidentFilter(v)}
            placeholder="Sakin seçici"
            disabled={!ledger}
          />
        </Col>
        <Col xs={24} md={4}>
          <Segmented
            block
            value={typeFilter}
            onChange={(v) => setTypeFilter(v as typeof typeFilter)}
            options={[
              { label: 'Tümü', value: 'all' },
              { label: 'Isınma', value: 'isinma' },
              { label: 'Aidat', value: 'aidat' },
              { label: 'Devir', value: 'devir' },
            ]}
            disabled={!ledger}
          />
        </Col>
      </Row>

      {ledgerQuery.isError && (
        <Alert type="error" showIcon message="Cari hesap yüklenemedi." style={{ marginBottom: 16 }} />
      )}

      {ledger && (
        <>
          {/* Faz 7.1: Tür bazlı özet kartları (LedgerResult.Summary) */}
          <Row gutter={16} style={{ marginBottom: 16 }}>
            <Col xs={24} md={8}>
              <Card size="small">
                <Statistic
                  title="Aidat Borcu (Anapara + Faiz)"
                  value={(ledger.summary?.aidatPrincipalDue ?? 0) + (ledger.summary?.aidatInterestAccrued ?? 0)}
                  precision={2}
                  suffix="TL"
                  valueStyle={{ color: '#722ed1' }}
                />
              </Card>
            </Col>
            <Col xs={24} md={8}>
              <Card size="small">
                <Statistic
                  title="Isınma Borcu (Anapara + Faiz)"
                  value={(ledger.summary?.invoicePrincipalDue ?? 0) + (ledger.summary?.invoiceInterestAccrued ?? 0)}
                  precision={2}
                  suffix="TL"
                  valueStyle={{ color: '#cf1322' }}
                />
              </Card>
            </Col>
            <Col xs={24} md={8}>
              <Card size="small">
                <Statistic
                  title="Devir Bakiyesi (Anapara + Faiz)"
                  value={(ledger.summary?.openingBalancePrincipalDue ?? 0) + (ledger.summary?.openingBalanceInterestDue ?? 0)}
                  precision={2}
                  suffix="TL"
                  valueStyle={{ color: '#d46b08' }}
                />
              </Card>
            </Col>
          </Row>

          <Row gutter={16} style={{ marginBottom: 16 }}>
            <Col xs={24} md={6}>
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
            <Col xs={24} md={6}>
              <Card size="small">
                <Statistic
                  title="Bu Dönem Tahakkuk (Fatura + Aidat + Faiz)"
                  value={periodAccrual}
                  precision={2}
                  suffix="TL"
                  valueStyle={{ color: '#cf1322' }}
                />
              </Card>
            </Col>
            <Col xs={24} md={6}>
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
            <Col xs={24} md={6}>
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
            scroll={{ x: 'max-content' }}
            rowKey="key"
            size="small"
            dataSource={movementRows}
            pagination={{ pageSize: 15, showSizeChanger: false }}
            columns={[
              { title: 'Tarih', dataIndex: 'date', width: 110, render: (v: string) => dateFmt(v) },
              {
                title: 'İşlem Türü',
                dataIndex: 'type',
                width: 140,
                render: (v: MovementRow['type'], r) =>
                  r.isCancelled ? <Tag color="red">İptal Edildi</Tag>
                  : v === 'Devir Alacağı' ? <Tag color="green">Devir Alacağı</Tag>
                  : v === 'Devir Anapara' ? <Tag color="purple">Devir Anapara</Tag>
                  : v === 'Devir Faiz' ? <Tag color="volcano">Devir Faiz</Tag>
                  : v === 'Fatura' ? <Tag color="blue">Fatura</Tag>
                  : v === 'Aidat' ? <Tag color="purple">Aidat</Tag>
                  : v === 'Faiz' ? <Tag color="orange">Faiz</Tag>
                  : v === 'Görev Mahsubu' ? <Tag color="cyan">Görev Mahsubu</Tag>
                  : <Tag color="green">Tahsilat</Tag>,
              },
              {
                title: 'İşlemi Yapan',
                dataIndex: 'performedBy',
                width: 180,
                render: (v: string | null) => v?.trim() ? v : '—',
              },
              {
                title: 'Açıklama',
                dataIndex: 'description',
                render: (v: string, r) => (
                  <div>
                    <div>
                      {r.isCancelled ? <Typography.Text delete type="secondary">{v}</Typography.Text> : v}
                    </div>
                    {r.interestDetail && (
                      <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                        {r.interestDetail}
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
                render: (v: number, r) =>
                  r.isCancelled && r.type !== 'Görev Mahsubu' ? <Typography.Text type="secondary">{tl(0)}</Typography.Text>
                  : v > 0 ? <Typography.Text type="danger">{tl(v)}</Typography.Text> : <Typography.Text type="secondary">—</Typography.Text>,
              },
              {
                title: 'Alacak (−)',
                dataIndex: 'credit',
                width: 130,
                align: 'right',
                render: (v: number, r) =>
                  r.isCancelled && r.type === 'Görev Mahsubu' ? <Typography.Text type="secondary">{tl(0)}</Typography.Text>
                  : v > 0 ? <Typography.Text type="success">{tl(v)}</Typography.Text> : <Typography.Text type="secondary">—</Typography.Text>,
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
            <Col xs={24} md={6}>
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
            <Col xs={24} md={6}>
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
            <Col xs={24} md={6}>
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
            <Col xs={24} md={6}>
              <Card size="small">
                <Descriptions size="small" column={1}>
                  <Descriptions.Item label="Dönem Başı">{dateFmt(cashFlow.dateFrom)}</Descriptions.Item>
                  <Descriptions.Item label="Dönem Sonu">{dateFmt(cashFlow.dateTo)}</Descriptions.Item>
                </Descriptions>
              </Card>
            </Col>
          </Row>

          <Table
            scroll={{ x: 'max-content' }}
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
