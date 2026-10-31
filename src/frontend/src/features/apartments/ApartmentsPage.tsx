import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert,
  Button,
  Card,
  DatePicker,
  Dropdown,
  Form,
  Input,
  InputNumber,
  List,
  message,
  Modal,
  Select,
  Space,
  Table,
  Tag,
  Tooltip,
  Typography,
  Upload,
} from 'antd'
import { DownloadOutlined, EditOutlined, FormOutlined, MoreOutlined, PlusOutlined, SearchOutlined, UploadOutlined } from '@ant-design/icons'
import type { ColumnsType } from 'antd/es/table'
import { apiClient } from '../../api/client'
import { formatDate } from '../../utils/dateUtils'
import { downloadFile } from '../../api/download'
import type {
  AddManualMeterReadingRequest,
  AidatTariffItem,
  ApartmentListItem,
  ImportMetersFromExcelResult,
  ManualReadingResult,
  SetApartmentTariffResult,
} from '../../types/api'
import dayjs, { type Dayjs } from 'dayjs'
import { AnalysisModal } from './AnalysisModal'
import { ReadingsModal } from './ReadingsModal'
import { OwnerEditModal, OwnerHistoryModal } from './OwnerModals'
import { AccountBookOutlined, HistoryOutlined, LineChartOutlined, MoneyCollectOutlined, TableOutlined, UserOutlined } from '@ant-design/icons'

// Türkçe sayı biçimi: binlik ayracı nokta, ondalık ayracı virgül (Intl 'tr-TR')
const fmtIndex = new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 3, maximumFractionDigits: 3 })
const fmtM2 = new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const tl = (v: number) => v.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })
const fmtM2One = (v: number) => v.toLocaleString('tr-TR', { minimumFractionDigits: 1 })

/** Excel içe aktarma sonucu: satır bazlı başarı/atlanan/hatalı raporu. */
function ImportResultModal({
  result,
  onClose,
}: {
  result: ImportMetersFromExcelResult | null
  onClose: () => void
}) {
  return (
    <Modal
      title="Excel İçe Aktarma Sonucu"
      open={result !== null}
      onCancel={onClose}
      footer={<Button type="primary" onClick={onClose}>Kapat</Button>}
      width={640}
    >
      {result && (
        <List
          size="small"
          dataSource={[
            { label: 'İçe aktarılan sayaç', value: result.importedCount, ok: true },
            { label: 'Zaten kayıtlı (atlandı)', value: result.skippedExistingCount, ok: true },
            { label: 'Daire eşleşmeyen', value: result.unmatchedApartmentCount, ok: false },
            { label: 'Oluşturulan başlangıç okuması', value: result.initialReadingsCreated, ok: true },
            { label: 'Kat maliki kaydı (SCD)', value: result.ownersSetCount, ok: true },
            { label: 'm² güncellemesi (SCD)', value: result.dimensionsUpdatedCount, ok: true },
          ]}
          renderItem={(item) => (
            <List.Item>
              <Typography.Text>{item.label}</Typography.Text>
              <Tag color={item.value === 0 && item.label.startsWith('Daire') ? 'orange' : item.ok ? 'green' : 'red'}>
                {item.value}
              </Tag>
            </List.Item>
          )}
        />
      )}
      {result && result.rowErrors.length > 0 && (
        <>
          <Typography.Text type="danger" strong>
            Hatalı satırlar ({result.rowErrors.length}):
          </Typography.Text>
          <List
            size="small"
            dataSource={result.rowErrors}
            renderItem={(error) => (
              <List.Item>
                <Typography.Text type="danger">{error}</Typography.Text>
              </List.Item>
            )}
          />
        </>
      )}
    </Modal>
  )
}


/**
 * Daireler sayfası: 72 dairenin listesi + m² güncelleme (SCD Type 2 akışı).
 * m² güncellemesi eski kaydı kapatıp yenisini açar — geçmiş asla ezilmez.
 */
export function ApartmentsPage() {
  const queryClient = useQueryClient()
  const [editing, setEditing] = useState<ApartmentListItem | null>(null)
  const [form] = Form.useForm<{ squareMeters: number }>()
  const [importResult, setImportResult] = useState<ImportMetersFromExcelResult | null>(null)
  // Tablo içi arama: Kapı No / Daire adı / Kat Maliki / Sayaç Seri No
  const [search, setSearch] = useState('')

  // Etap 6.6: Manuel okuma girişi (saha arızası B planı).
  const [manualFor, setManualFor] = useState<ApartmentListItem | null>(null)
  const [manualForm] = Form.useForm<{ meterId: string; readingDate: Dayjs; indexValue: number }>()
  const [manualResult, setManualResult] = useState<ManualReadingResult | null>(null)

  // Toplu daire ekleme (sayı girilir; kapı numaraları mevcut sonuncudan devam eder).
  const [addOpen, setAddOpen] = useState(false)
  const [addCount, setAddCount] = useState<number | null>(1)
  const addApartmentsMutation = useMutation({
    mutationFn: async (count: number) =>
      (
        await apiClient.post<{
          addedCount: number
          firstDoorNumber: number
          lastDoorNumber: number
          totalApartments: number
        }>('/apartments/bulk', { count })
      ).data,
    onSuccess: (res) => {
      void queryClient.invalidateQueries({ queryKey: ['apartments'] })
      setAddOpen(false)
      Modal.success({
        title: 'Daireler eklendi',
        content: `${res.addedCount} daire eklendi (Kapı No ${res.firstDoorNumber}–${res.lastDoorNumber}). Toplam: ${res.totalApartments}. Varsayılan m² 100'dür; Excel ile veya satırdaki "m²" / "Malik" butonları yerine satır menüsündeki seçeneklerle güncelleyebilirsiniz.`,
        okText: 'Tamam',
      })
    },
  })

  // Etap 6.7: Analiz / Okumalar / Kat Maliki modalları.
  const [analysisFor, setAnalysisFor] = useState<ApartmentListItem | null>(null)
  const [readingsFor, setReadingsFor] = useState<ApartmentListItem | null>(null)
  const [ownerEditFor, setOwnerEditFor] = useState<ApartmentListItem | null>(null)
  const [ownerHistoryFor, setOwnerHistoryFor] = useState<ApartmentListItem | null>(null)

  // Etap 6.8: Devir bakiyesi girişi (Anapara + Geçmiş Faiz + Devir Tarihi).
  const [devirFor, setDevirFor] = useState<ApartmentListItem | null>(null)
  const [devirForm] = Form.useForm<{ principalAmount: number; interestAmount: number; balanceDate: Dayjs }>()
  const devirMutation = useMutation({
    mutationFn: async (input: {
      apartmentId: string
      principalAmount: number
      interestAmount: number
      balanceDate: string
    }) =>
      (
        await apiClient.put(
          `/apartments/${input.apartmentId}/opening-balance`,
          {
            principalAmount: input.principalAmount,
            interestAmount: input.interestAmount,
            balanceDate: input.balanceDate,
          },
        )
      ).data as { openingBalancePrincipal: number; openingBalanceInterest: number },
    onSuccess: (res) => {
      void queryClient.invalidateQueries({ queryKey: ['apartments'] })
      setDevirFor(null)
      Modal.success({
        title: 'Devir bakiyesi kaydedildi (SCD kaydı güncellendi)',
        content:
          `Devreden Anapara: ${res.openingBalancePrincipal.toFixed(2)} TL · ` +
          `Devreden Faiz: ${res.openingBalanceInterest.toFixed(2)} TL. ` +
          'Gecikme faizi artık yalnızca anapara kalanından günlük olarak işler; geçmiş faize faiz işlemez.',
        okText: 'Anladım',
      })
    },
  })

  const apartmentsQuery = useQuery({
    queryKey: ['apartments'],
    queryFn: async () => (await apiClient.get<ApartmentListItem[]>('/apartments')).data,
  })

  const manualMutation = useMutation({
    mutationFn: async (req: AddManualMeterReadingRequest) =>
      (await apiClient.post<ManualReadingResult>('/meter-readings/manual', req)).data,
    onSuccess: (res) => {
      void queryClient.invalidateQueries({ queryKey: ['apartments'] })
      void queryClient.invalidateQueries({ queryKey: ['dashboard-summary'] })
      setManualFor(null)
      setManualResult(res)
    },
  })

  // Excel içe aktarma: dosya FormData ile POST edilir, sonuç satır bazlı raporlanır.
  const importMutation = useMutation({
    mutationFn: async (file: File) => {
      const formData = new FormData()
      formData.append('file', file)
      const response = await apiClient.post<ImportMetersFromExcelResult>(
        '/apartments/import-meters-excel',
        formData,
        { headers: { 'Content-Type': 'multipart/form-data' } },
      )
      return response.data
    },
    onSuccess: (result) => {
      void queryClient.invalidateQueries({ queryKey: ['apartments'] })
      setImportResult(result)
    },
  })

  const updateMutation = useMutation({
    mutationFn: async (input: { apartmentId: string; squareMeters: number }) =>
      (
        await apiClient.put(`/apartments/${input.apartmentId}/dimensions`, {
          apartmentId: input.apartmentId,
          squareMeters: input.squareMeters,
          commonAreaRatio: 0,
        })
      ).data as { apartmentId: string; newSquareMeters: number; validFrom: string },
    onSuccess: (result) => {
      // Kural gereği kullanıcıya SCD geçişi açıkça bildirilir.
      Modal.success({
        title: 'm² güncellendi (SCD Type 2)',
        content: `Eski m² kaydı kapatıldı, yeni m² ${dayjs(result.validFrom).format('DD.MM.YYYY HH:mm')} itibarıyla geçerli oldu. Geçmiş hesaplamalar eski değerlerle korunmaya devam eder.`,
        okText: 'Anladım',
      })
      setEditing(null)
      void queryClient.invalidateQueries({ queryKey: ['apartments'] })
    },
  })

  // Faz 6.9: Aidat tarife atama (tekli) — aktif tarifeler + mutasyon.
  const [tariffFor, setTariffFor] = useState<ApartmentListItem | null>(null)
  const [tariffForm] = Form.useForm<{ tariffId?: string }>()
  const tariffsQuery = useQuery({
    queryKey: ['aidat-tariffs'],
    queryFn: async () => (await apiClient.get<AidatTariffItem[]>('/aidat/tariffs?isActive=true')).data,
  })
  const tariffMutation = useMutation({
    mutationFn: async (input: { apartmentId: string; tariffId: string | null }) =>
      (await apiClient.put<SetApartmentTariffResult>(
        `/aidat/apartments/${input.apartmentId}/tariff`,
        { tariffId: input.tariffId },
      )).data,
    onSuccess: (res) => {
      void queryClient.invalidateQueries({ queryKey: ['apartments'] })
      void queryClient.invalidateQueries({ queryKey: ['aidat-tariffs'] })
      setTariffFor(null)
      message.success(res.tariffId ? `Tarife atandı: ${res.tariffName}` : 'Tarife kaldırıldı (daire artık tarifesiz).')
    },
  })

  // Arama filtresi: Kapı No, Daire adı, Kat Maliki, Sayaç Seri No
  const filteredApartments = (apartmentsQuery.data ?? []).filter((a) => {
    const q = search.trim().toLocaleLowerCase('tr-TR')
    if (!q) return true
    return (
      String(a.doorNumber).toLocaleLowerCase('tr-TR').includes(q) ||
      (a.title ?? '').toLocaleLowerCase('tr-TR').includes(q) ||
      (a.ownerName ?? '').toLocaleLowerCase('tr-TR').includes(q) ||
      a.meters.some((m) => m.serialNumber.toLocaleLowerCase('tr-TR').includes(q))
    )
  })

  const columns: ColumnsType<ApartmentListItem> = [
    {
      title: 'Kapı No',
      dataIndex: 'doorNumber',
      width: 90,
      sorter: (a, b) => Number(a.doorNumber) - Number(b.doorNumber),
      defaultSortOrder: 'ascend',
      render: (v: string) => <Typography.Text strong>{v}</Typography.Text>,
    },
    { title: 'Daire', dataIndex: 'title' },
    {
      title: 'Kat Maliki',
      dataIndex: 'ownerName',
      width: 160,
      render: (v: string | null) =>
        v ? <Typography.Text style={{ fontWeight: 500 }}>{v}</Typography.Text> : <Tag>YOK</Tag>,
    },
    {
      title: 'Aktif m² (SCD)',
      dataIndex: 'activeSquareMeters',
      width: 120,
      align: 'right',
      render: (v: number | null) => (v === null ? <Tag>YOK</Tag> : `${fmtM2.format(v)} m²`),
    },
    {
      title: 'Aidat Tarifesi',
      dataIndex: 'aidatTariffName',
      width: 150,
      render: (v: string | null) => (v ? <Tag color="purple">{v}</Tag> : <Tag>Tarifesiz</Tag>),
    },
    {
      title: 'Son Endeks (kWh)',
      dataIndex: 'lastIndexValue',
      width: 130,
      align: 'right',
      render: (v: number | null) => (v === null ? <Tag>Okuma Yok</Tag> : fmtIndex.format(v)),
    },
    {
      title: 'Son Okuma Tarihi',
      dataIndex: 'lastReadingDateUtc',
      width: 140,
      render: (v: string | null) => {
        if (!v) return <Tag>Okuma Yok</Tag>
        // 30 günden eski okuma kırmızı: yönetici eksik okumayı anında fark etsin.
        const isStale = dayjs().diff(dayjs(v), 'day') > 30
        return (
          <Typography.Text type={isStale ? 'danger' : undefined} strong={isStale}>
            {formatDate(v)}
          </Typography.Text>
        )
      },
    },
    {
      title: 'Sayaç Seri No',
      width: 160,
      render: (_: unknown, record: ApartmentListItem) =>
        record.meters.length > 0 ? record.meters[0].serialNumber : <Tag>Sayaç yok</Tag>,
    },
    {
      title: 'M-Bus Adresi',
      width: 130,
      align: 'right',
      render: (_: unknown, record: ApartmentListItem) =>
        record.meters.length > 0 ? record.meters[0].mbusPrimaryAddress ?? '-' : '-',
    },
    {
      title: 'İşlem',
      width: 220,
      render: (_: unknown, record: ApartmentListItem) => (
        <Space size={4}>
          <Tooltip title="Manuel okuma gir (M-Bus okunamadığında B planı)">
            <Button
              size="small"
              type="primary"
              ghost
              icon={<FormOutlined />}
              disabled={record.meters.filter((m) => m.isActive).length === 0}
              onClick={() => {
                setManualFor(record)
                manualForm.resetFields()
                const activeMeters = record.meters.filter((m) => m.isActive)
                if (activeMeters.length === 1) {
                  manualForm.setFieldsValue({ meterId: activeMeters[0].id })
                }
              }}
            >
              Manuel Okuma
            </Button>
          </Tooltip>
          <Dropdown
            menu={{
              items: [
                { key: 'dimension', icon: <EditOutlined />, label: 'm² Düzenle' },
                { key: 'tariff', icon: <MoneyCollectOutlined />, label: 'Tarife Ata' },
                { key: 'owner', icon: <UserOutlined />, label: 'Malik Değiştir' },
                { key: 'ownerHistory', icon: <HistoryOutlined />, label: 'Malik Geçmişi' },
                { key: 'openingBalance', icon: <AccountBookOutlined />, label: 'Devir Bakiye Gir' },
                { key: 'readings', icon: <TableOutlined />, label: 'Okuma Listesi', disabled: !record.activeMeterId },
                { key: 'analysis', icon: <LineChartOutlined />, label: 'Tüketim Analizi' },
              ],
              onClick: ({ key }) => {
                if (key === 'dimension') {
                  setEditing(record)
                  form.setFieldsValue({ squareMeters: record.activeSquareMeters ?? undefined })
                } else if (key === 'tariff') {
                  setTariffFor(record)
                  tariffForm.setFieldsValue({ tariffId: record.aidatTariffId ?? '__none__' })
                } else if (key === 'owner') {
                  setOwnerEditFor(record)
                } else if (key === 'ownerHistory') {
                  setOwnerHistoryFor(record)
                } else if (key === 'openingBalance') {
                  setDevirFor(record)
                  devirForm.setFieldsValue({
                    principalAmount: record.openingBalancePrincipal ?? undefined,
                    interestAmount: record.openingBalanceInterest ?? undefined,
                    balanceDate: record.openingBalanceDate ? dayjs(record.openingBalanceDate) : undefined,
                  })
                } else if (key === 'readings') {
                  setReadingsFor(record)
                } else if (key === 'analysis') {
                  setAnalysisFor(record)
                }
              },
            }}
            trigger={['click']}
          >
            <Button size="small" icon={<MoreOutlined />} aria-label="Diğer işlemler" />
          </Dropdown>
        </Space>
      ),
    },
  ]

  return (
    <Card
      title={`Daireler (${apartmentsQuery.data?.length ?? 0})`}
      extra={
        <Space wrap>
          <Input
            allowClear
            size="small"
            prefix={<SearchOutlined style={{ color: 'rgba(0,0,0,0.45)' }} />}
            placeholder="Ara: kapı no, daire, malik, seri no"
            style={{ width: 240 }}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
          <Button size="small" type="primary" icon={<PlusOutlined />} onClick={() => { setAddCount(1); setAddOpen(true) }}>
            Daire Ekle
          </Button>
          <Button
            size="small"
            icon={<DownloadOutlined />}
            onClick={() => void downloadFile('/apartments/import-template', 'Sayac-Import-Sablon.xlsx')}
          >
            Örnek Şablon İndir
          </Button>
          <Upload
            accept=".xlsx"
            showUploadList={false}
            maxCount={1}
            customRequest={({ file }) => importMutation.mutate(file as File)}
          >
            <Button size="small" icon={<UploadOutlined />} loading={importMutation.isPending}>
              Excel'den İçe Aktar
            </Button>
          </Upload>
        </Space>
      }
    >
      <Table
        rowKey="id"
        columns={columns}
        dataSource={filteredApartments}
        loading={apartmentsQuery.isLoading}
        size="small"
        pagination={{ pageSize: 15, showSizeChanger: false }}
      />

      <Modal
        title="Daire Ekle"
        open={addOpen}
        onCancel={() => setAddOpen(false)}
        confirmLoading={addApartmentsMutation.isPending}
        okText="Ekle"
        cancelText="İptal"
        okButtonProps={{ disabled: !addCount || addCount < 1 }}
        onOk={() => addCount && addApartmentsMutation.mutate(addCount)}
      >
        <Typography.Paragraph type="secondary">
          Kaç daire eklensin? Kapı numaraları mevcut sonuncudan devam eder (boşsa 1'den başlar).
          Sonrasında bilgileri Excel ile toplu veya satırdaki butonlarla tek tek girebilirsiniz.
        </Typography.Paragraph>
        <InputNumber
          style={{ width: '100%' }}
          min={1}
          max={500}
          precision={0}
          value={addCount}
          onChange={(v) => setAddCount(v)}
          addonAfter="daire"
        />
      </Modal>

      <ImportResultModal result={importResult} onClose={() => setImportResult(null)} />

      {/* Etap 6.7: Analiz / Okumalar / Kat Maliki modalları */}
      <AnalysisModal apartment={analysisFor} onClose={() => setAnalysisFor(null)} />
      <ReadingsModal apartment={readingsFor} onClose={() => setReadingsFor(null)} />
      <OwnerEditModal apartment={ownerEditFor} onClose={() => setOwnerEditFor(null)} />
      <OwnerHistoryModal apartment={ownerHistoryFor} onClose={() => setOwnerHistoryFor(null)} />

      {/* Etap 6.8: Devir bakiyesi girişi — Anapara + Geçmiş Faiz + Devir Tarihi */}
      <Modal
        title={`Devir Bakiye Gir — Daire ${devirFor?.doorNumber ?? ''}`}
        open={devirFor !== null}
        onCancel={() => setDevirFor(null)}
        confirmLoading={devirMutation.isPending}
        okText="Devri Kaydet"
        cancelText="İptal"
        onOk={() =>
          devirForm.validateFields().then((values) => {
            if (devirFor === null) return
            devirMutation.mutate({
              apartmentId: devirFor.id,
              principalAmount: values.principalAmount,
              interestAmount: values.interestAmount ?? 0,
              balanceDate: (values.balanceDate as Dayjs).format('YYYY-MM-DD'),
            })
          })
        }
      >
        <Typography.Paragraph type="secondary">
          Devreden tutarlar cari hesaba iki ayrı "Borç" satırı olarak işlenir. Gecikme faizi
          yalnızca <strong>anapara</strong> kalanından günlük basit faizle işler; geçmiş faize
          tekrar faiz işlemez. Her iki tutar 0 girilirse devir temizlenir.
        </Typography.Paragraph>
        <Form form={devirForm} layout="vertical">
          <Form.Item
            name="principalAmount"
            label="Devreden Anapara (TL)"
            rules={[
              { required: true, message: 'Anapara zorunludur (0 girilebilir).' },
              { type: 'number', message: 'Sayısal bir tutar giriniz.' },
            ]}
          >
            <InputNumber style={{ width: '100%' }} min={-10000000} max={10000000} step={0.01} decimalSeparator="," addonAfter="TL" />
          </Form.Item>
          <Form.Item
            name="interestAmount"
            label="Devreden Faiz (TL)"
            rules={[{ type: 'number', message: 'Sayısal bir tutar giriniz.' }]}
          >
            <InputNumber style={{ width: '100%' }} min={-10000000} max={10000000} step={0.01} decimalSeparator="," addonAfter="TL" />
          </Form.Item>
          <Form.Item
            name="balanceDate"
            label="Devir Tarihi"
            rules={[
              {
                validator: (_, value: Dayjs | null | undefined) => {
                  const p = devirForm.getFieldValue('principalAmount') ?? 0
                  const i = devirForm.getFieldValue('interestAmount') ?? 0
                  if ((p !== 0 || i !== 0) && !value) {
                    return Promise.reject('Devir tarihi zorunludur.')
                  }
                  return Promise.resolve()
                },
              },
            ]}
          >
            <DatePicker style={{ width: '100%' }} format="DD.MM.YYYY" disabledDate={(d) => d.isAfter(dayjs(), 'day')} />
          </Form.Item>
        </Form>
      </Modal>

      <Modal
        title={`m² Güncelle — Daire ${editing?.doorNumber ?? ''}`}
        open={editing !== null}
        onCancel={() => setEditing(null)}
        confirmLoading={updateMutation.isPending}
        onOk={() => form.validateFields().then((values) => {
          if (editing === null) return
          updateMutation.mutate({ apartmentId: editing.id, squareMeters: values.squareMeters })
        })}
        okText="Kaydet (SCD)"
        cancelText="İptal"
      >
        <Typography.Paragraph type="secondary">
          Yeni m², bugünden itibaren geçerli olacak şekilde yeni bir SCD kaydı açar. Geçmiş dönem
          hesaplamaları eski değerlerle korunur.
        </Typography.Paragraph>
        <Form form={form} layout="vertical">
          <Form.Item
            name="squareMeters"
            label="Yeni m²"
            rules={[
              { required: true, message: 'm² zorunludur.' },
              { type: 'number', min: 1, max: 1000, message: 'm² 1-1000 arasında olmalıdır.' },
            ]}
          >
            <InputNumber style={{ width: '100%' }} min={1} max={1000} step={0.5} decimalSeparator="," addonAfter="m²" />
          </Form.Item>
        </Form>
        {editing && (
          <Space direction="vertical" size={0}>
            <Typography.Text type="secondary">
              Mevcut aktif m²: {editing.activeSquareMeters?.toFixed(2) ?? 'YOK'}
            </Typography.Text>
          </Space>
        )}
      </Modal>

      {/* Faz 6.9: Aidat tarife atama (tekli) */}
      <Modal
        title={`Aidat Tarife Ata — Daire ${tariffFor?.doorNumber ?? ''}`}
        open={tariffFor !== null}
        onCancel={() => setTariffFor(null)}
        confirmLoading={tariffMutation.isPending}
        okText="Kaydet"
        cancelText="İptal"
        onOk={() =>
          tariffForm.validateFields().then((values) => {
            if (tariffFor === null) return
            tariffMutation.mutate({
              apartmentId: tariffFor.id,
              tariffId: values.tariffId === '__none__' ? null : (values.tariffId as string),
            })
          })
        }
      >
        <Form form={tariffForm} layout="vertical">
          <Form.Item
            name="tariffId"
            label="Aidat Tarifesi"
            rules={[{ required: true, message: 'Tarife seçin veya Tarifesız bırak seçeneğini kullanın.' }]}
          >
            <Select
              placeholder="Tarife seçin…"
              options={[
                { value: '__none__', label: 'Tarifesız bırak' },
                ...(tariffsQuery.data ?? []).map((t) => ({
                  value: t.id,
                  label: `${t.name} — ${fmtM2One(t.squareMeters)} m² — ${tl(t.amount)}`,
                })),
              ]}
            />
          </Form.Item>
          <Typography.Text type="secondary" style={{ fontSize: 12 }}>
            Tarife-bazlı aidat dağıtımında dairenin borcu, atanmış tarifenin aylık tutarı kadardır.
            Tarifeyi tüm benzer dairelere toplu atamak için Aidat Yönetimi → Tarifeler ekranındaki
            Daireleri Ata butonunu kullanın.
          </Typography.Text>
        </Form>
      </Modal>

      {/* Etap 6.6: Manuel okuma girişi */}
      <Modal
        title={`Manuel Okuma Gir — Daire ${manualFor?.doorNumber ?? ''}`}
        open={manualFor !== null}
        onCancel={() => setManualFor(null)}
        confirmLoading={manualMutation.isPending}
        okText="Okumayı Kaydet"
        cancelText="İptal"
        onOk={() =>
          manualForm.validateFields().then((values) => {
            if (manualFor === null) return
            manualMutation.mutate({
              apartmentId: manualFor.id,
              meterId: values.meterId,
              readingDate: (values.readingDate as Dayjs).toISOString(),
              indexValue: values.indexValue,
            })
          })
        }
      >
        <Typography.Paragraph type="secondary">
          M-Bus okunamayan sayaçlar için elle okuma girişi. Dönem dışı tarih girilirse okuma
          kaydedilir ama uyarı verilir; kilitli dönem ve endeks gerilemesi reddedilir.
        </Typography.Paragraph>
        <Form form={manualForm} layout="vertical">
          <Form.Item
            name="meterId"
            label="Sayaç"
            rules={[{ required: true, message: 'Sayaç seçilmelidir.' }]}
          >
            <Select
              placeholder="Sayaç seçin"
              options={(manualFor?.meters ?? [])
                .filter((m) => m.isActive)
                .map((m) => ({
                  value: m.id,
                  label: `${m.serialNumber} (M-Bus: ${m.mbusPrimaryAddress ?? '-'}, ${m.meterType})`,
                }))}
            />
          </Form.Item>
          <Form.Item
            name="readingDate"
            label="Okuma Tarihi"
            rules={[{ required: true, message: 'Okuma tarihi zorunludur.' }]}
          >
            <DatePicker style={{ width: '100%' }} showTime format="DD.MM.YYYY HH:mm" disabledDate={(d) => d.isAfter(dayjs(), 'day')} />
          </Form.Item>
          <Form.Item
            name="indexValue"
            label="Kümülatif Endeks (kWh)"
            rules={[
              { required: true, message: 'Endeks zorunludur.' },
              { type: 'number', min: 0, message: 'Endeks negatif olamaz.' },
            ]}
          >
            <InputNumber style={{ width: '100%' }} min={0} step={0.001} decimalSeparator="," addonAfter="kWh" />
          </Form.Item>
        </Form>
      </Modal>

      {/* Manuel okuma sonucu: hesaplanan tüketim + dönem dışı uyarıları */}
      <Modal
        title="Manuel Okuma Kaydedildi"
        open={manualResult !== null}
        onCancel={() => setManualResult(null)}
        footer={<Button type="primary" onClick={() => setManualResult(null)}>Tamam</Button>}
      >
        {manualResult && (
          <Space direction="vertical" size={8} style={{ width: '100%' }}>
            <Typography.Text>
              Endeks: <strong>{manualResult.indexValue.toFixed(3)} kWh</strong>
            </Typography.Text>
            <Typography.Text>
              Dönem tüketimi: <strong>{manualResult.periodConsumptionKWh.toFixed(3)} kWh</strong>
              {manualResult.isInitialReading && ' (başlangıç endeksi — tüketim sayılmaz)'}
            </Typography.Text>
            {manualResult.warnings.map((w) => (
              <Alert key={w} type="warning" message={w} showIcon />
            ))}
          </Space>
        )}
      </Modal>
    </Card>
  )
}
