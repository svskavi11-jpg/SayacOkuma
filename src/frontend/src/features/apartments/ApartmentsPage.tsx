import { AppModal } from '../../components/AppModal'
import { useEffect, useRef, useState } from 'react'
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
  Row,
  Col,
  Select,
  Space,
  Table,
  Tabs,
  Tag,
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
  ApartmentOwnerHistoryItem,
  ResidentDocumentItem,
  ResidentDocumentType,
  SetApartmentTariffResult,
  UpdateResidentsResult,
} from '../../types/api'
import { ResidentHistoryBlock, StayFields, uploadResidentFiles } from './ResidentManageExtras'
import type { UploadFile } from 'antd/es/upload/interface'
import dayjs, { type Dayjs } from 'dayjs'
import { AnalysisModal } from './AnalysisModal'
import { RoleAssignModal } from './RoleAssignModal'
import { PortalAccountModal } from './PortalAccountModal'
import { ReadingsModal } from './ReadingsModal'
import { OwnerEditModal, OwnerHistoryModal } from './OwnerModals'
import { AccountBookOutlined, HistoryOutlined, KeyOutlined, LineChartOutlined, MoneyCollectOutlined, SafetyCertificateOutlined, TableOutlined, TeamOutlined, UserOutlined } from '@ant-design/icons'
import { useAuth } from '../auth/AuthContext'

// Türkçe sayı biçimi: binlik ayracı nokta, ondalık ayracı virgül (Intl 'tr-TR')
const fmtIndex = new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 3, maximumFractionDigits: 3 })
const fmtM2 = new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
const tl = (v: number) => v.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })

function renderDebt(v: number, color: string) {
  if (!v) return <Typography.Text type="secondary">—</Typography.Text>
  return (
    <Typography.Text strong style={{ color }}>
      {tl(v)}
    </Typography.Text>
  )
}
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
    <AppModal
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
    </AppModal>
  )
}


/**
 * Daireler sayfası: 72 dairenin listesi + m² güncelleme (SCD Type 2 akışı).
 * m² güncellemesi eski kaydı kapatıp yenisini açar — geçmiş asla ezilmez.
 */
export function ApartmentsPage() {
  const queryClient = useQueryClient()
  const { hasPermission } = useAuth()
  const canCreate = hasPermission('Apartments.Create')
  const canEdit = hasPermission('Apartments.Edit')
  const canManageResident = hasPermission('Apartments.ManageResident')
  const canManual = hasPermission('MeterReadings.ManualEntry')
  const canReadings = hasPermission('Apartments.Readings') || hasPermission('MeterReadings.View')
  const canAnalysis = hasPermission('Apartments.Analysis')
  const canAssignTariff = hasPermission('Invoices.Edit')
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
  const [roleFor, setRoleFor] = useState<ApartmentListItem | null>(null)
  const [portalFor, setPortalFor] = useState<ApartmentListItem | null>(null)

  // Etap 6.8: Devir bakiyesi girişi (Anapara + Geçmiş Faiz + Devir Tarihi).
  const [devirFor, setDevirFor] = useState<ApartmentListItem | null>(null)
  const [devirForm] = Form.useForm<{ principalAmount: number; interestAmount: number; balanceDate: Dayjs; ownerId?: string }>()
  const devirPrincipal = Form.useWatch('principalAmount', devirForm)
  const devirMutation = useMutation({
    mutationFn: async (input: {
      apartmentId: string
      principalAmount: number
      interestAmount: number
      balanceDate: string
      ownerId?: string | null
    }) =>
      (
        await apiClient.put(
          `/apartments/${input.apartmentId}/opening-balance`,
          {
            principalAmount: input.principalAmount,
            interestAmount: input.interestAmount,
            balanceDate: input.balanceDate,
            ownerId: input.ownerId ?? null,
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

  // Faz 7.1: Sakin yönetimi (Ev Sahibi + Kiracı) — tekli modal.
  const [residentsFor, setResidentsFor] = useState<ApartmentListItem | null>(null)
  const [evFiles, setEvFiles] = useState<UploadFile[]>([])
  const [kiFiles, setKiFiles] = useState<UploadFile[]>([])
  const [evDocType, setEvDocType] = useState<ResidentDocumentType>('Tapu')
  const [kiDocType, setKiDocType] = useState<ResidentDocumentType>('KiraSozlesmesi')
  const stayFilledFor = useRef<string | null>(null)
  const [residentsForm] = Form.useForm<{
    evName: string; evPhone?: string; evEmail?: string; evTckn?: string; evNotes?: string
    evValidFrom?: Dayjs; evValidTo?: Dayjs; evLeaseStart?: Dayjs; evLeaseEnd?: Dayjs
    kiName?: string; kiPhone?: string; kiEmail?: string; kiTckn?: string; kiNotes?: string
    kiValidFrom?: Dayjs; kiValidTo?: Dayjs; kiLeaseStart?: Dayjs; kiLeaseEnd?: Dayjs
  }>()
  const stayApartmentId = residentsFor?.id ?? devirFor?.id ?? null
  const stayQuery = useQuery({
    queryKey: ['apartment-owner-history', stayApartmentId],
    enabled: stayApartmentId !== null,
    queryFn: async () =>
      (await apiClient.get<ApartmentOwnerHistoryItem[]>(`/apartments/${stayApartmentId}/owner/history`)).data,
  })
  const documentQuery = useQuery({
    queryKey: ['resident-documents', residentsFor?.id],
    enabled: residentsFor !== null,
    queryFn: async () =>
      (await apiClient.get<ResidentDocumentItem[]>(`/residents/documents?apartmentId=${residentsFor!.id}`)).data,
  })
  const residentsMutation = useMutation({
    mutationFn: async (input: { apartmentId: string; body: Record<string, unknown> }) =>
      (await apiClient.put<UpdateResidentsResult>(
        `/apartments/${input.apartmentId}/residents`,
        input.body,
      )).data,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['apartments'] })
      void queryClient.invalidateQueries({ queryKey: ['apartment-owner-history'] })
      void queryClient.invalidateQueries({ queryKey: ['resident-history'] })
      void queryClient.invalidateQueries({ queryKey: ['resident-documents'] })
      message.success('Sakin kaydı yazıldı.')
    },
  })
  useEffect(() => {
    if (!residentsFor || !stayQuery.data || stayFilledFor.current === residentsFor.id) return
    stayFilledFor.current = residentsFor.id
    const active = (type: ApartmentOwnerHistoryItem['residentType']) =>
      stayQuery.data.find((row) => row.residentType === type && row.validTo === null)
    const ev = active('EvSahibi')
    const ki = active('Kiraci')
    const asDay = (value: string | null | undefined) => (value ? dayjs(value) : undefined)
    residentsForm.setFieldsValue({
      evValidFrom: ev ? dayjs(ev.validFrom) : dayjs(),
      evValidTo: undefined,
      evLeaseStart: asDay(ev?.leaseStartDate),
      evLeaseEnd: asDay(ev?.leaseEndDate),
      kiValidFrom: ki ? dayjs(ki.validFrom) : undefined,
      kiValidTo: undefined,
      kiLeaseStart: asDay(ki?.leaseStartDate),
      kiLeaseEnd: asDay(ki?.leaseEndDate),
    })
  }, [residentsFor, stayQuery.data, residentsForm])

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
    { title: 'Daire', dataIndex: 'title', width: 108, ellipsis: true },
    {
      title: 'Kat Maliki',
      dataIndex: 'ownerName',
      width: 160,
      render: (v: string | null) =>
        v ? <Typography.Text style={{ fontWeight: 500 }}>{v}</Typography.Text> : <Tag>YOK</Tag>,
    },
    {
      title: 'Güncel Bakiye',
      dataIndex: 'currentBalance',
      width: 150,
      align: 'right',
      sorter: (a, b) => a.currentBalance - b.currentBalance,
      render: (v: number) => {
        if (v === 0) {
          return <Typography.Text type="secondary">Borcu Yok</Typography.Text>
        }
        return (
          <Typography.Text strong style={{ color: v > 0 ? '#cf1322' : '#389e0d' }}>
            {tl(v)}
          </Typography.Text>
        )
      },
    },
    {
      title: 'Aidat Borcu',
      dataIndex: 'aidatDebt',
      width: 130,
      align: 'right',
      sorter: (a, b) => a.aidatDebt - b.aidatDebt,
      render: (v: number) => renderDebt(v, '#722ed1'),
    },
    {
      title: 'Isınma Borcu',
      dataIndex: 'heatingDebt',
      width: 130,
      align: 'right',
      sorter: (a, b) => a.heatingDebt - b.heatingDebt,
      render: (v: number) => renderDebt(v, '#cf1322'),
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
      title: 'İşlem',
      fixed: 'right',
      width: 72,
      align: 'center',
      render: (_: unknown, record: ApartmentListItem) => (
        <Dropdown
            menu={{
              items: [
                ...(canManual ? [{
                  key: 'manual',
                  icon: <FormOutlined />,
                  label: 'Manuel Okuma',
                  disabled: record.meters.filter((m) => m.isActive).length === 0,
                }] : []),
                ...(canEdit ? [{ key: 'dimension', icon: <EditOutlined />, label: 'm² Düzenle' }] : []),
                ...(canAssignTariff ? [{ key: 'tariff', icon: <MoneyCollectOutlined />, label: 'Tarife Ata' }] : []),
                ...(canManageResident ? [
                  { key: 'residents', icon: <TeamOutlined />, label: 'Sakin Yönetimi' },
                  { key: 'portal', icon: <KeyOutlined />, label: 'Portal hesabı' },
                  { key: 'role', icon: <SafetyCertificateOutlined />, label: 'Görev Ata' },
                ] : []),
                ...(canEdit ? [
                  { key: 'owner', icon: <UserOutlined />, label: 'Malik Değiştir' },
                  { key: 'openingBalance', icon: <AccountBookOutlined />, label: 'Devir Bakiye Gir' },
                ] : []),
                { key: 'ownerHistory', icon: <HistoryOutlined />, label: 'Malik Geçmişi' },
                ...(canReadings ? [{ key: 'readings', icon: <TableOutlined />, label: 'Okuma Listesi', disabled: !record.activeMeterId }] : []),
                ...(canAnalysis ? [{ key: 'analysis', icon: <LineChartOutlined />, label: 'Tüketim Analizi' }] : []),
              ],
              onClick: ({ key }) => {
                if (key === 'manual') {
                  setManualFor(record)
                  manualForm.resetFields()
                  const activeMeters = record.meters.filter((m) => m.isActive)
                  if (activeMeters.length === 1) {
                    manualForm.setFieldsValue({ meterId: activeMeters[0].id })
                  }
                } else if (key === 'dimension') {
                  setEditing(record)
                  form.setFieldsValue({ squareMeters: record.activeSquareMeters ?? undefined })
                } else if (key === 'tariff') {
                  setTariffFor(record)
                  tariffForm.setFieldsValue({ tariffId: record.aidatTariffId ?? '__none__' })
                } else if (key === 'residents') {
                  setResidentsFor(record)
                  residentsForm.resetFields()
                  residentsForm.setFieldsValue({
                    evName: record.ownerName ?? undefined,
                    evPhone: record.evSahibiPhoneNumber ?? undefined,
                    evEmail: record.evSahibiEmail ?? undefined,
                    evTckn: record.evSahibiIdentityNumber ?? undefined,
                    evNotes: record.evSahibiNotes ?? undefined,
                    kiName: record.kiraciName ?? undefined,
                    kiPhone: record.kiraciPhoneNumber ?? undefined,
                    kiEmail: record.kiraciEmail ?? undefined,
                    kiTckn: record.kiraciIdentityNumber ?? undefined,
                    kiNotes: record.kiraciNotes ?? undefined,
                  })
                } else if (key === 'portal') {
                  setPortalFor(record)
                } else if (key === 'owner') {
                  setOwnerEditFor(record)
                } else if (key === 'ownerHistory') {
                  setOwnerHistoryFor(record)
                } else if (key === 'role') {
                  setRoleFor(record)
                } else if (key === 'openingBalance') {
                  setDevirFor(record)
                  devirForm.setFieldsValue({
                    principalAmount: record.openingBalancePrincipal ?? undefined,
                    interestAmount: record.openingBalanceInterest ?? undefined,
                    balanceDate: record.openingBalanceDate ? dayjs(record.openingBalanceDate) : undefined,
                    ownerId: record.openingBalanceOwnerId ?? undefined,
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
          {canCreate && (
            <Button size="small" type="primary" icon={<PlusOutlined />} onClick={() => { setAddCount(1); setAddOpen(true) }}>
              Daire Ekle
            </Button>
          )}
          <Button
            size="small"
            icon={<DownloadOutlined />}
            onClick={() => void downloadFile('/apartments/import-template', 'Sayac-Import-Sablon.xlsx')}
          >
            Örnek Şablon İndir
          </Button>
          {canCreate && (
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
          )}
        </Space>
      }
    >
      <Table
        rowKey="id"
        columns={columns}
        dataSource={filteredApartments}
        loading={apartmentsQuery.isLoading}
        size="small"
        scroll={{ x: 'max-content' }}
        pagination={{ pageSize: 15, showSizeChanger: false }}
      />

      <AppModal
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
      </AppModal>

      <ImportResultModal result={importResult} onClose={() => setImportResult(null)} />

      {/* Etap 6.7: Analiz / Okumalar / Kat Maliki modalları */}
      <AnalysisModal apartment={analysisFor} onClose={() => setAnalysisFor(null)} />
      <ReadingsModal apartment={readingsFor} onClose={() => setReadingsFor(null)} />
      <OwnerEditModal apartment={ownerEditFor} onClose={() => setOwnerEditFor(null)} />
      <OwnerHistoryModal apartment={ownerHistoryFor} onClose={() => setOwnerHistoryFor(null)} />
      <RoleAssignModal apartment={roleFor} onClose={() => setRoleFor(null)} />
      <PortalAccountModal apartment={portalFor} onClose={() => setPortalFor(null)} />

      {/* Etap 6.8: Devir bakiyesi girişi — Anapara + Geçmiş Faiz + Devir Tarihi */}
      <AppModal
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
              ownerId: values.ownerId ?? null,
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
          {typeof devirPrincipal === 'number' && devirPrincipal < 0 && (
            <Alert
              type="success"
              showIcon
              style={{ marginBottom: 12 }}
              message={`Bu tutar (${tl(Math.abs(devirPrincipal))}) Alacak olarak işlenecektir. Yeni borç tahakkuk ettiğinde öncelikle bu alacaktan düşülür.`}
            />
          )}
          <Form.Item
            name="interestAmount"
            label="Devreden Faiz (TL)"
            rules={[
              { type: 'number', message: 'Sayısal bir tutar giriniz.' },
              {
                validator: (_, val: number | null | undefined) =>
                  typeof val === 'number' && val < 0
                    ? Promise.reject('Geçmiş dönem gecikme faizi negatif olamaz.')
                    : Promise.resolve(),
              },
            ]}
          >
            <InputNumber style={{ width: '100%' }} min={0} max={10000000} step={0.01} decimalSeparator="," addonAfter="TL" />
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
          <Form.Item name="ownerId" label="Borcun sahibi">
            <Select
              allowClear
              placeholder="Sakin seçin"
              loading={stayQuery.isLoading}
              options={(stayQuery.data ?? []).map((row) => ({
                value: row.id,
                label: `${row.ownerName} — ${row.residentType === 'Kiraci' ? 'Kiracı' : 'Ev Sahibi'} — ${formatDate(row.validFrom)} / ${row.validTo ? formatDate(row.validTo) : 'Devam Ediyor'}`,
              }))}
            />
          </Form.Item>
        </Form>
      </AppModal>

      <AppModal
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
      </AppModal>

      {/* Faz 7.1: Sakin yönetimi — Ev Sahibi + Kiracı sekmeleri */}
      <AppModal
        title={`Sakin Yönetimi — Daire ${residentsFor?.doorNumber ?? ''}`}
        open={residentsFor !== null}
        onCancel={() => {
          stayFilledFor.current = null
          setEvFiles([])
          setKiFiles([])
          setResidentsFor(null)
        }}
        confirmLoading={residentsMutation.isPending}
        okText="Kaydet"
        cancelText="İptal"
        width={640}
        onOk={() =>
          residentsForm.validateFields().then((values) => {
            if (residentsFor === null) return
            const day = (value?: Dayjs) => (value ? value.format('YYYY-MM-DD') : null)
            const ev = values.evName
              ? {
                  name: values.evName as string,
                  phoneNumber: (values.evPhone as string) || null,
                  email: (values.evEmail as string) || null,
                  identityNumber: (values.evTckn as string) || null,
                  notes: (values.evNotes as string) || null,
                  validFrom: day(values.evValidFrom),
                  validTo: day(values.evValidTo),
                  leaseStartDate: day(values.evLeaseStart),
                  leaseEndDate: day(values.evLeaseEnd),
                }
              : null
            const ki = values.kiName
              ? {
                  name: values.kiName as string,
                  phoneNumber: (values.kiPhone as string) || null,
                  email: (values.kiEmail as string) || null,
                  identityNumber: (values.kiTckn as string) || null,
                  notes: (values.kiNotes as string) || null,
                  validFrom: day(values.kiValidFrom),
                  validTo: day(values.kiValidTo),
                  leaseStartDate: day(values.kiLeaseStart),
                  leaseEndDate: day(values.kiLeaseEnd),
                }
              : null
            return residentsMutation.mutateAsync({
              apartmentId: residentsFor.id,
              body: {
                validFrom: (values.evValidFrom ?? values.kiValidFrom ?? dayjs()).toISOString(),
                evSahibi: ev,
                kiraci: ki,
              },
            }).then(async (saved) => {
              if (saved.evSahibiId) await uploadResidentFiles(saved.evSahibiId, evFiles, evDocType)
              if (saved.kiraciId) await uploadResidentFiles(saved.kiraciId, kiFiles, kiDocType)
              stayFilledFor.current = null
              setEvFiles([])
              setKiFiles([])
              setResidentsFor(null)
            })
          })
        }
      >
        <Form form={residentsForm} layout="vertical">
          <Tabs
            items={[
              {
                key: 'ev',
                label: '🏠 Ev Sahibi',
                forceRender: true,
                children: (
                  <>
                    <Form.Item name="evName" label="Ad Soyad" rules={[{ required: true, message: 'Ev sahibi adı zorunludur.' }]}>
                      <Input maxLength={100} placeholder="örn: Ahmet Yılmaz" />
                    </Form.Item>
                    <Row gutter={12}>
                      <Col xs={24} md={12}>
                        <Form.Item name="evPhone" label="Telefon">
                          <Input maxLength={20} placeholder="örn: 0532 123 45 67" />
                        </Form.Item>
                      </Col>
                      <Col xs={24} md={12}>
                        <Form.Item name="evEmail" label="E-posta">
                          <Input maxLength={100} placeholder="örn: ahmet@ornek.com" />
                        </Form.Item>
                      </Col>
                    </Row>
                    <Row gutter={12}>
                      <Col xs={24} md={12}>
                        <Form.Item name="evTckn" label="TCKN (opsiyonel)">
                          <Input maxLength={11} placeholder="11 hane" />
                        </Form.Item>
                      </Col>
                      <Col xs={24} md={12}>
                        <Form.Item name="evNotes" label="Notlar">
                          <Input maxLength={500} />
                        </Form.Item>
                      </Col>
                    </Row>
                    <StayFields
                      prefix="ev"
                      startRequired
                      files={evFiles}
                      onFiles={setEvFiles}
                      documentType={evDocType}
                      onDocumentType={setEvDocType}
                    />
                  </>
                ),
              },
              {
                key: 'kiraci',
                label: '🔑 Kiracı',
                forceRender: true,
                children: (
                  <>
                    <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block', marginBottom: 12 }}>
                      Kiracı adı boş bırakılırsa kiracı kaydı güncellenmez. Aidat/Isınma borçları
                      kiracı varsa kiracıya yazılır; ev sahibi müteselsil sorumlu tutulur (KMK m.22).
                    </Typography.Text>
                    <Form.Item name="kiName" label="Ad Soyad">
                      <Input maxLength={100} placeholder="örn: Ayşe Demir (boş = kiracı yok)" />
                    </Form.Item>
                    <Row gutter={12}>
                      <Col xs={24} md={12}>
                        <Form.Item name="kiPhone" label="Telefon">
                          <Input maxLength={20} placeholder="örn: 0533 987 65 43" />
                        </Form.Item>
                      </Col>
                      <Col xs={24} md={12}>
                        <Form.Item name="kiEmail" label="E-posta">
                          <Input maxLength={100} />
                        </Form.Item>
                      </Col>
                    </Row>
                    <Row gutter={12}>
                      <Col xs={24} md={12}>
                        <Form.Item name="kiTckn" label="TCKN (opsiyonel)">
                          <Input maxLength={11} placeholder="11 hane" />
                        </Form.Item>
                      </Col>
                      <Col xs={24} md={12}>
                        <Form.Item name="kiNotes" label="Notlar">
                          <Input maxLength={500} />
                        </Form.Item>
                      </Col>
                    </Row>
                    <StayFields
                      prefix="ki"
                      startRequired={false}
                      files={kiFiles}
                      onFiles={setKiFiles}
                      documentType={kiDocType}
                      onDocumentType={setKiDocType}
                    />
                  </>
                ),
              },
            ]}
          />
          <ResidentHistoryBlock
            rows={stayQuery.data ?? []}
            documents={documentQuery.data ?? []}
            loading={stayQuery.isLoading}
          />
        </Form>
      </AppModal>

      {/* Faz 6.9: Aidat tarife atama (tekli) */}
      <AppModal
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
      </AppModal>

      {/* Etap 6.6: Manuel okuma girişi */}
      <AppModal
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
      </AppModal>

      {/* Manuel okuma sonucu: hesaplanan tüketim + dönem dışı uyarıları */}
      <AppModal
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
      </AppModal>
    </Card>
  )
}
