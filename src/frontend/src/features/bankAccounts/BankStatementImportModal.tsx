import { AppModal } from '../../components/AppModal'
import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert,
  Button,
  Col,
  Empty,
  Modal,
  Row,
  Select,
  Space,
  Statistic,
  Table,
  Tag,
  Tooltip,
  Typography,
  Upload,
  message,
} from 'antd'
import {
  CheckCircleOutlined,
  ExclamationCircleOutlined,
  FileExcelOutlined,
  InboxOutlined,
  StopOutlined,
  UploadOutlined,
  UserOutlined,
} from '@ant-design/icons'
import type { ColumnsType } from 'antd/es/table'
import dayjs from 'dayjs'
import { apiClient } from '../../api/client'
import type {
  ApartmentListItem,
  BankAccountListItem,
  BankStatementParseResult,
  BankStatementRowStatus,
  CreateBulkPaymentRequest,
  CreateBulkPaymentResult,
  PaymentTargetDebtType,
} from '../../types/api'
import { BankAccountSelect } from './BankAccountSelect'

const tl = (v: number) =>
  `${v.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} TL`

type EditableDebtTarget = PaymentTargetDebtType | 'OTHER'

interface EditableStatementRow {
  key: number
  rowNumber: number
  paymentDate: string | null
  description: string
  documentNumber: string | null
  amount: number
  apartmentId: string | null
  debtTarget: EditableDebtTarget
  originalStatus: BankStatementRowStatus
  warningMessage: string | null
}

interface BankStatementImportModalProps {
  open: boolean
  onClose: () => void
  defaultBankAccountId?: string | null
}

export function BankStatementImportModal({
  open,
  onClose,
  defaultBankAccountId,
}: BankStatementImportModalProps) {
  const queryClient = useQueryClient()
  const [bankAccountId, setBankAccountId] = useState<string | undefined>(
    defaultBankAccountId ?? undefined,
  )
  const [fileName, setFileName] = useState<string | null>(null)
  const [fileErrors, setFileErrors] = useState<string[]>([])
  const [rows, setRows] = useState<EditableStatementRow[]>([])
  const [selectedKeys, setSelectedKeys] = useState<number[]>([])

  const apartmentsQuery = useQuery({
    queryKey: ['apartments'],
    queryFn: async () => (await apiClient.get<ApartmentListItem[]>('/apartments')).data,
    enabled: open,
  })

  const accountsQuery = useQuery({
    queryKey: ['bank-accounts'],
    queryFn: async () => (await apiClient.get<BankAccountListItem[]>('/bank-accounts')).data,
    enabled: open,
  })

  const apartments = useMemo(() => apartmentsQuery.data ?? [], [apartmentsQuery.data])

  const apartmentById = useMemo(() => {
    const map = new Map<string, ApartmentListItem>()
    for (const apt of apartments) {
      map.set(apt.id, apt)
    }
    return map
  }, [apartments])

  const apartmentOptions = useMemo(
    () =>
      [...apartments]
        .sort((a, b) => Number(a.doorNumber) - Number(b.doorNumber))
        .map((a) => {
          const resident = a.kiraciName || a.ownerName
          return {
            value: a.id,
            label: resident ? `Daire ${a.doorNumber} — ${resident}` : `Daire ${a.doorNumber}`,
          }
        }),
    [apartments],
  )

  const effectiveBankAccountId = useMemo(() => {
    if (bankAccountId) return bankAccountId
    const firstActive = (accountsQuery.data ?? []).find((a) => a.isActive)
    return firstActive?.id
  }, [bankAccountId, accountsQuery.data])

  const parseMutation = useMutation({
    mutationFn: async (file: File) => {
      const formData = new FormData()
      formData.append('file', file)
      return (
        await apiClient.post<BankStatementParseResult>('/payments/bank-statement/parse', formData, {
          headers: { 'Content-Type': 'multipart/form-data' },
        })
      ).data
    },
    onSuccess: (res, file) => {
      setFileName(file.name)
      setFileErrors(res.fileErrors ?? [])

      const mapped: EditableStatementRow[] = res.rows.map((r) => ({
        key: r.rowNumber,
        rowNumber: r.rowNumber,
        paymentDate: r.paymentDate,
        description: r.description,
        documentNumber: r.documentNumber,
        amount: r.amount,
        apartmentId: r.matchedApartmentId,
        debtTarget: r.suggestedDebtType ?? 'OTHER',
        originalStatus: r.status,
        warningMessage: r.warningMessage,
      }))

      setRows(mapped)
      // Sadece geçerli ve dairesi eşleşmiş satırları otomatik seç; mükerrerleri dışarıda bırak
      setSelectedKeys(
        mapped
          .filter(
            (r) =>
              r.originalStatus !== 'Duplicate' &&
              r.originalStatus !== 'InvalidRow' &&
              r.apartmentId !== null,
          )
          .map((r) => r.key),
      )

      if (mapped.length === 0 && (res.fileErrors?.length ?? 0) === 0) {
        message.warning('Dosyada içe aktarılabilecek pozitif tahsilat hareketi bulunamadı.')
      } else {
        message.success(`${mapped.length} hareket analiz edildi. Lütfen eşleşmeleri kontrol edin.`)
      }
    },
  })

  const bulkCreateMutation = useMutation({
    mutationFn: async (payload: CreateBulkPaymentRequest) =>
      (await apiClient.post<CreateBulkPaymentResult>('/payments/bulk', payload)).data,
    onSuccess: (res) => {
      void queryClient.invalidateQueries({ queryKey: ['bank-accounts'] })
      void queryClient.invalidateQueries({ queryKey: ['bank-transactions'] })
      void queryClient.invalidateQueries({ queryKey: ['dashboard-banks'] })
      void queryClient.invalidateQueries({ queryKey: ['dashboard-summary'] })
      void queryClient.invalidateQueries({ queryKey: ['apartments'] })
      void queryClient.invalidateQueries({ queryKey: ['payments'] })

      Modal.success({
        title: 'Tahsilatlar Başarıyla Oluşturuldu',
        content: (
          <div>
            <p style={{ marginBottom: 6 }}>
              Toplam <strong>{res.createdCount}</strong> adet tahsilat kaydı (
              <strong>{tl(res.totalCreatedAmount)}</strong>) cari hesaplara ve banka hareketlerine
              işlendi.
            </p>
            {res.skippedDuplicateCount > 0 && (
              <Typography.Text type="warning">
                Mükerrer dekont numarası nedeniyle {res.skippedDuplicateCount} satır atlandı.
              </Typography.Text>
            )}
          </div>
        ),
        okText: 'Tamam',
      })

      handleResetAndClose()
    },
  })

  const handleResetAndClose = () => {
    setFileName(null)
    setFileErrors([])
    setRows([])
    setSelectedKeys([])
    onClose()
  }

  const getEffectiveStatus = (row: EditableStatementRow): BankStatementRowStatus => {
    if (row.originalStatus === 'Duplicate' || row.originalStatus === 'InvalidRow') {
      return row.originalStatus
    }
    return row.apartmentId ? 'Ready' : 'UnmatchedApartment'
  }

  const handleApartmentChange = (rowKey: number, newApartmentId: string | null) => {
    setRows((prev) =>
      prev.map((r) => (r.key === rowKey ? { ...r, apartmentId: newApartmentId } : r)),
    )

    if (newApartmentId) {
      setSelectedKeys((prev) => (prev.includes(rowKey) ? prev : [...prev, rowKey]))
    } else {
      setSelectedKeys((prev) => prev.filter((k) => k !== rowKey))
    }
  }

  const handleDebtTargetChange = (rowKey: number, newTarget: EditableDebtTarget) => {
    setRows((prev) =>
      prev.map((r) => (r.key === rowKey ? { ...r, debtTarget: newTarget } : r)),
    )
  }

  const counts = useMemo(() => {
    let ready = 0
    let unmatched = 0
    let blocked = 0
    for (const r of rows) {
      const st = getEffectiveStatus(r)
      if (st === 'Ready') ready++
      else if (st === 'UnmatchedApartment') unmatched++
      else blocked++
    }
    return { total: rows.length, ready, unmatched, blocked }
  }, [rows])

  const selectedValidRows = useMemo(() => {
    const keySet = new Set(selectedKeys)
    return rows.filter(
      (r) =>
        keySet.has(r.key) &&
        getEffectiveStatus(r) === 'Ready' &&
        r.apartmentId !== null &&
        r.paymentDate !== null &&
        r.amount > 0,
    )
  }, [rows, selectedKeys])

  const selectedTotalAmount = useMemo(
    () => selectedValidRows.reduce((sum, r) => sum + r.amount, 0),
    [selectedValidRows],
  )

  const handleConfirm = () => {
    if (!effectiveBankAccountId) {
      message.error('Lütfen tahsilatların işleneceği banka hesabını seçin.')
      return
    }

    if (selectedValidRows.length === 0) {
      message.warning('Kaydedilecek geçerli ve dairesi seçilmiş satır bulunmuyor.')
      return
    }

    const payload: CreateBulkPaymentRequest = {
      bankAccountId: effectiveBankAccountId,
      receivedBy: 'Banka Ekstresi',
      items: selectedValidRows.map((r) => ({
        apartmentId: r.apartmentId!,
        paymentDate: r.paymentDate!,
        amount: r.amount,
        description: r.description,
        documentNumber: r.documentNumber,
        targetDebtType: r.debtTarget === 'OTHER' ? null : r.debtTarget,
      })),
    }

    bulkCreateMutation.mutate(payload)
  }

  const columns: ColumnsType<EditableStatementRow> = [
    {
      title: 'Tarih',
      dataIndex: 'paymentDate',
      width: 110,
      render: (val: string | null) =>
        val ? (
          <Typography.Text strong>{dayjs(val).format('DD.MM.YYYY')}</Typography.Text>
        ) : (
          <Tag color="error">Geçersiz</Tag>
        ),
    },
    {
      title: 'Açıklama & Dekont',
      dataIndex: 'description',
      render: (desc: string, row) => (
        <div>
          <Typography.Text
            style={{
              fontSize: 13,
              display: 'block',
              textDecoration:
                row.originalStatus === 'Duplicate' ? 'line-through' : undefined,
              color: row.originalStatus === 'Duplicate' ? '#8c8c8c' : '#1f1f1f',
            }}
          >
            {desc || '—'}
          </Typography.Text>
          {row.documentNumber && (
            <Tag
              bordered={false}
              style={{ marginTop: 4, fontSize: 11, color: '#595959', background: '#f5f5f5' }}
            >
              Dekont No: {row.documentNumber}
            </Tag>
          )}
        </div>
      ),
    },
    {
      title: 'Tutar',
      dataIndex: 'amount',
      align: 'right',
      width: 140,
      render: (val: number, row) => (
        <Typography.Text
          strong
          style={{
            fontSize: 14,
            color: row.originalStatus === 'Duplicate' ? '#8c8c8c' : '#389e0d',
            whiteSpace: 'nowrap',
          }}
        >
          {tl(val)}
        </Typography.Text>
      ),
    },
    {
      title: 'Eşleşen Daire',
      dataIndex: 'apartmentId',
      width: 220,
      render: (aptId: string | null, row) => {
        const disabled =
          row.originalStatus === 'Duplicate' || row.originalStatus === 'InvalidRow'
        return (
          <Select
            showSearch
            allowClear
            disabled={disabled}
            placeholder="Daire seçin..."
            optionFilterProp="label"
            value={aptId ?? undefined}
            onChange={(val) => handleApartmentChange(row.key, val ?? null)}
            options={apartmentOptions}
            status={!disabled && !aptId ? 'warning' : undefined}
            style={{ width: '100%' }}
          />
        )
      },
    },
    {
      title: 'Eşleşen Sakin',
      key: 'resident',
      width: 185,
      render: (_: unknown, row) => {
        if (!row.apartmentId) {
          return <Typography.Text type="secondary">—</Typography.Text>
        }
        const apt = apartmentById.get(row.apartmentId)
        if (!apt) {
          return <Typography.Text type="secondary">—</Typography.Text>
        }
        const isTenant = Boolean(apt.kiraciName)
        const residentName = apt.kiraciName || apt.ownerName
        if (!residentName) {
          return <Typography.Text type="secondary">Sakin kaydı yok</Typography.Text>
        }
        return (
          <Space size={6} wrap>
            <UserOutlined style={{ color: '#8c8c8c', fontSize: 12 }} />
            <Typography.Text style={{ fontSize: 13 }}>{residentName}</Typography.Text>
            <Tag
              bordered={false}
              color={isTenant ? 'blue' : 'purple'}
              style={{ fontSize: 11, margin: 0 }}
            >
              {isTenant ? 'Kiracı' : 'Ev Sahibi'}
            </Tag>
          </Space>
        )
      },
    },
    {
      title: 'Borç Türü',
      dataIndex: 'debtTarget',
      width: 185,
      render: (val: EditableDebtTarget, row) => {
        const disabled =
          row.originalStatus === 'Duplicate' || row.originalStatus === 'InvalidRow'
        return (
          <Select
            disabled={disabled}
            value={val}
            onChange={(nextVal) => handleDebtTargetChange(row.key, nextVal)}
            style={{ width: '100%' }}
            options={[
              { value: 'Aidat', label: 'Aidat Borcu' },
              { value: 'Invoice', label: 'Isınma / Doğalgaz' },
              { value: 'OpeningBalancePrincipal', label: 'Devir Anapara' },
              { value: 'OTHER', label: 'Diğer (Genel FIFO)' },
            ]}
          />
        )
      },
    },
    {
      title: 'Durum',
      key: 'status',
      width: 175,
      render: (_: unknown, row) => {
        const status = getEffectiveStatus(row)
        if (status === 'Ready') {
          return (
            <Tag icon={<CheckCircleOutlined />} color="success" style={{ margin: 0 }}>
              Onaya Hazır
            </Tag>
          )
        }
        if (status === 'UnmatchedApartment') {
          return (
            <Tooltip title={row.warningMessage ?? 'Lütfen listeden daire seçin.'}>
              <Tag icon={<ExclamationCircleOutlined />} color="warning" style={{ margin: 0 }}>
                Daire Seçilmeli
              </Tag>
            </Tooltip>
          )
        }
        if (status === 'Duplicate') {
          return (
            <Tooltip title={row.warningMessage ?? 'Bu dekont numarası zaten kayıtlı.'}>
              <Tag icon={<StopOutlined />} color="error" style={{ margin: 0 }}>
                Mükerrer (Atlanacak)
              </Tag>
            </Tooltip>
          )
        }
        return (
          <Tooltip title={row.warningMessage ?? 'Tarih veya tutar geçersiz.'}>
            <Tag icon={<StopOutlined />} color="error" style={{ margin: 0 }}>
              Geçersiz Satır
            </Tag>
          </Tooltip>
        )
      },
    },
  ]

  return (
    <AppModal
      open={open}
      onCancel={handleResetAndClose}
      width={1220}
      title={
        <Space size={10}>
          <FileExcelOutlined style={{ color: '#389e0d', fontSize: 20 }} />
          <span style={{ fontSize: 17, fontWeight: 600 }}>
            Akıllı Banka Ekstresi İçe Aktarma ve Otomatik Eşleştirme
          </span>
        </Space>
      }
      footer={
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            flexWrap: 'wrap',
            gap: 12,
            paddingTop: 8,
          }}
        >
          <div>
            {rows.length > 0 && (
              <Space size={16}>
                <Typography.Text type="secondary">
                  Seçili Tahsilat: <strong>{selectedValidRows.length}</strong> adet
                </Typography.Text>
                <Typography.Text style={{ fontSize: 15 }}>
                  Toplam İşlenecek Tutar:{' '}
                  <strong style={{ color: '#389e0d', fontSize: 16 }}>
                    {tl(selectedTotalAmount)}
                  </strong>
                </Typography.Text>
              </Space>
            )}
          </div>
          <Space size={10}>
            <Button onClick={handleResetAndClose}>Vazgeç</Button>
            <Button
              type="primary"
              size="large"
              icon={<CheckCircleOutlined />}
              disabled={selectedValidRows.length === 0 || !effectiveBankAccountId}
              loading={bulkCreateMutation.isPending}
              onClick={handleConfirm}
            >
              Onayla ve Tahsilatları Oluştur ({selectedValidRows.length})
            </Button>
          </Space>
        </div>
      }
    >
      <Space direction="vertical" size={16} style={{ width: '100%' }}>
        {/* Üst Seçim Alanı: Banka Hesabı ve Dosya Yükleme */}
        <Row gutter={[16, 16]} align="middle">
          <Col xs={24} md={9}>
            <div style={{ marginBottom: 6 }}>
              <Typography.Text strong style={{ fontSize: 13 }}>
                1. Tahsilatların İşleneceği Banka Hesabı
              </Typography.Text>
            </div>
            <BankAccountSelect
              allowClear={false}
              value={effectiveBankAccountId}
              onChange={(val) => setBankAccountId(val ?? undefined)}
              placeholder="Garanti, TEB vb. banka hesabı seçin"
              style={{ width: '100%' }}
            />
          </Col>

          <Col xs={24} md={15}>
            <div style={{ marginBottom: 6 }}>
              <Typography.Text strong style={{ fontSize: 13 }}>
                2. Banka Ekstresi Dosyası (.xlsx, .xls veya .csv)
              </Typography.Text>
            </div>
            <Upload.Dragger
              accept=".xlsx,.xls,.csv"
              multiple={false}
              showUploadList={false}
              beforeUpload={(file) => {
                parseMutation.mutate(file)
                return false
              }}
              style={{ padding: '8px 16px', background: '#fafafa', borderRadius: 10 }}
            >
              <Space size={12} align="center">
                <InboxOutlined style={{ fontSize: 26, color: '#1677ff' }} />
                <div style={{ textAlign: 'left' }}>
                  <Typography.Text strong style={{ display: 'block', fontSize: 13 }}>
                    {fileName
                      ? `Yüklü Dosya: ${fileName} (Değiştirmek için tıklayın veya sürükleyin)`
                      : 'Garanti veya TEB ekstresini buraya sürükleyin ya da seçmek için tıklayın'}
                  </Typography.Text>
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    Dosya doğrudan kaydedilmez; önce daire ve borç türü eşleşmeleri önizlenir.
                  </Typography.Text>
                </div>
                <Button size="small" icon={<UploadOutlined />} loading={parseMutation.isPending}>
                  Dosya Seç
                </Button>
              </Space>
            </Upload.Dragger>
          </Col>
        </Row>

        {fileErrors.length > 0 && (
          <Alert
            type="warning"
            showIcon
            message="Dosya Uyarıları"
            description={fileErrors.join(' · ')}
          />
        )}

        {/* Özet Sayaçlar */}
        {rows.length > 0 && (
          <Row gutter={[12, 12]}>
            <Col xs={12} sm={6}>
              <div
                style={{
                  background: '#f5f5f5',
                  padding: '10px 16px',
                  borderRadius: 10,
                  borderLeft: '4px solid #595959',
                }}
              >
                <Statistic
                  title="Toplam Gelen Hareket"
                  value={counts.total}
                  valueStyle={{ fontSize: 20, fontWeight: 700 }}
                />
              </div>
            </Col>
            <Col xs={12} sm={6}>
              <div
                style={{
                  background: '#f6ffed',
                  padding: '10px 16px',
                  borderRadius: 10,
                  borderLeft: '4px solid #389e0d',
                }}
              >
                <Statistic
                  title="Eşleşen (Hazır)"
                  value={counts.ready}
                  valueStyle={{ fontSize: 20, fontWeight: 700, color: '#389e0d' }}
                />
              </div>
            </Col>
            <Col xs={12} sm={6}>
              <div
                style={{
                  background: '#fffbe6',
                  padding: '10px 16px',
                  borderRadius: 10,
                  borderLeft: '4px solid #d48806',
                }}
              >
                <Statistic
                  title="Daire Seçimi Bekleyen"
                  value={counts.unmatched}
                  valueStyle={{ fontSize: 20, fontWeight: 700, color: '#d48806' }}
                />
              </div>
            </Col>
            <Col xs={12} sm={6}>
              <div
                style={{
                  background: '#fff1f0',
                  padding: '10px 16px',
                  borderRadius: 10,
                  borderLeft: '4px solid #cf1322',
                }}
              >
                <Statistic
                  title="Mükerrer / İşlenmeyecek"
                  value={counts.blocked}
                  valueStyle={{ fontSize: 20, fontWeight: 700, color: '#cf1322' }}
                />
              </div>
            </Col>
          </Row>
        )}

        {/* Önizleme Tablosu */}
        {rows.length > 0 ? (
          <Table<EditableStatementRow>
            rowKey="key"
            size="small"
            loading={parseMutation.isPending}
            dataSource={rows}
            columns={columns}
            pagination={{ pageSize: 10, showSizeChanger: true }}
            scroll={{ x: 1050 }}
            rowSelection={{
              selectedRowKeys: selectedKeys,
              onChange: (keys) => setSelectedKeys(keys as number[]),
              getCheckboxProps: (record) => {
                const status = getEffectiveStatus(record)
                return {
                  disabled: status !== 'Ready',
                }
              },
            }}
          />
        ) : (
          <div style={{ padding: '32px 0' }}>
            <Empty
              description={
                parseMutation.isPending
                  ? 'Ekstre analiz ediliyor...'
                  : 'Önizleme için yukarıdan bir banka ekstresi (.xlsx veya .csv) yükleyin.'
              }
            />
          </div>
        )}
      </Space>
    </AppModal>
  )
}
