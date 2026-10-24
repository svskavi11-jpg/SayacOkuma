import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert,
  Button,
  Card,
  DatePicker,
  Form,
  InputNumber,
  List,
  Modal,
  Select,
  Space,
  Table,
  Tag,
  Tooltip,
  Typography,
  Upload,
} from 'antd'
import { DownloadOutlined, EditOutlined, FormOutlined, UploadOutlined } from '@ant-design/icons'
import type { ColumnsType } from 'antd/es/table'
import { apiClient } from '../../api/client'
import { downloadFile } from '../../api/download'
import type {
  AddManualMeterReadingRequest,
  ApartmentListItem,
  ImportMetersFromExcelResult,
  ManualReadingResult,
} from '../../types/api'
import dayjs, { type Dayjs } from 'dayjs'
import { AnalysisModal } from './AnalysisModal'
import { ReadingsModal } from './ReadingsModal'
import { OwnerEditModal, OwnerHistoryModal } from './OwnerModals'
import { HistoryOutlined, LineChartOutlined, TableOutlined, UserOutlined } from '@ant-design/icons'

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

  // Etap 6.6: Manuel okuma girişi (saha arızası B planı).
  const [manualFor, setManualFor] = useState<ApartmentListItem | null>(null)
  const [manualForm] = Form.useForm<{ meterId: string; readingDate: Dayjs; indexValue: number }>()
  const [manualResult, setManualResult] = useState<ManualReadingResult | null>(null)

  // Etap 6.7: Analiz / Okumalar / Kat Maliki modalları.
  const [analysisFor, setAnalysisFor] = useState<ApartmentListItem | null>(null)
  const [readingsFor, setReadingsFor] = useState<ApartmentListItem | null>(null)
  const [ownerEditFor, setOwnerEditFor] = useState<ApartmentListItem | null>(null)
  const [ownerHistoryFor, setOwnerHistoryFor] = useState<ApartmentListItem | null>(null)

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
      render: (v: string | null) => (v ? v : <Tag>YOK</Tag>),
    },
    {
      title: 'Aktif m² (SCD)',
      dataIndex: 'activeSquareMeters',
      width: 120,
      render: (v: number | null) => (v === null ? <Tag>YOK</Tag> : `${v.toFixed(2)} m²`),
    },
    {
      title: 'Son Endeks (kWh)',
      dataIndex: 'lastIndexValue',
      width: 130,
      align: 'right',
      render: (v: number | null) => (v === null ? <Tag>Okuma Yok</Tag> : v.toFixed(3)),
    },
    {
      title: 'Son Okuma Tarihi',
      dataIndex: 'lastReadingDateUtc',
      width: 140,
      render: (v: string | null) =>
        v ? dayjs(v).format('DD.MM.YYYY') : <Tag>Okuma Yok</Tag>,
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
      render: (_: unknown, record: ApartmentListItem) =>
        record.meters.length > 0 ? record.meters[0].mbusPrimaryAddress ?? '-' : '-',
    },
    {
      title: 'İşlem',
      width: 430,
      render: (_: unknown, record: ApartmentListItem) => (
        <Space wrap size={4}>
          <Tooltip title="m² güncelle (SCD Type 2)">
            <Button
              size="small"
              icon={<EditOutlined />}
              onClick={() => {
                setEditing(record)
                form.setFieldsValue({ squareMeters: record.activeSquareMeters ?? undefined })
              }}
            >
              m²
            </Button>
          </Tooltip>
          <Tooltip title="Kat maliki güncelle (SCD Type 2)">
            <Button
              size="small"
              icon={<UserOutlined />}
              onClick={() => setOwnerEditFor(record)}
            >
              Malik
            </Button>
          </Tooltip>
          <Tooltip title="Kat maliki geçmişi (SCD Type 2)">
            <Button
              size="small"
              icon={<HistoryOutlined />}
              onClick={() => setOwnerHistoryFor(record)}
            >
              Geçmiş
            </Button>
          </Tooltip>
          <Tooltip title="Manuel okuma gir (M-Bus okunamadığında B planı)">
            <Button
              size="small"
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
          <Tooltip title="Okumaları görüntüle / düzelt (denetim izli)">
            <Button
              size="small"
              icon={<TableOutlined />}
              disabled={!record.activeMeterId}
              onClick={() => setReadingsFor(record)}
            >
              Okumalar
            </Button>
          </Tooltip>
          <Tooltip title="Tüketim analizi (yıl / tarih aralığı)">
            <Button
              size="small"
              icon={<LineChartOutlined />}
              onClick={() => setAnalysisFor(record)}
            >
              Analiz
            </Button>
          </Tooltip>
        </Space>
      ),
    },
  ]

  return (
    <Card
      title={`Daireler (${apartmentsQuery.data?.length ?? 0})`}
      extra={
        <Space wrap>
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
        dataSource={apartmentsQuery.data}
        loading={apartmentsQuery.isLoading}
        size="small"
        pagination={{ pageSize: 15, showSizeChanger: false }}
      />

      <ImportResultModal result={importResult} onClose={() => setImportResult(null)} />

      {/* Etap 6.7: Analiz / Okumalar / Kat Maliki modalları */}
      <AnalysisModal apartment={analysisFor} onClose={() => setAnalysisFor(null)} />
      <ReadingsModal apartment={readingsFor} onClose={() => setReadingsFor(null)} />
      <OwnerEditModal apartment={ownerEditFor} onClose={() => setOwnerEditFor(null)} />
      <OwnerHistoryModal apartment={ownerHistoryFor} onClose={() => setOwnerHistoryFor(null)} />

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
