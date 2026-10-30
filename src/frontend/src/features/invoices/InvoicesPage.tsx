import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Button,
  Card,
  Col,
  DatePicker,
  Form,
  Input,
  InputNumber,
  List,
  message,
  Modal,
  Row,
  Space,
  Statistic,
  Table,
  Tag,
  Typography,
  Upload,
} from 'antd'
import {
  CalculatorOutlined,
  DownloadOutlined,
  EditOutlined,
  LockOutlined,
  PlusOutlined,
  UnlockOutlined,
  UploadOutlined,
} from '@ant-design/icons'
import type { ColumnsType } from 'antd/es/table'
import dayjs, { type Dayjs } from 'dayjs'
import { apiClient } from '../../api/client'
import { downloadFile } from '../../api/download'
import type {
  AddGasInvoiceRequest,
  CalculateDistributionResult,
  CalculationResultResponse,
  GasInvoiceDetail,
  GasInvoiceListItem,
  ImportReadingsResult,
  InvoiceFinanceResult,
  UpdateGasInvoiceResult,
} from '../../types/api'

const tl = (v: number) => v.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })

/** Akmercan fatura giriş formu + liste + hesaplama (Kural #1 mutabakatlı). */
export function InvoicesPage() {
  const queryClient = useQueryClient()
  const [addOpen, setAddOpen] = useState(false)
  const [form] = Form.useForm()
  const [result, setResult] = useState<CalculationResultResponse | null>(null)
  // Etap 6.6: Excel okuma içe aktarma + dönem kilitleme
  const [importResult, setImportResult] = useState<ImportReadingsResult | null>(null)
  // Etap 6.7: Fatura düzenleme (null = yeni fatura modu)
  const [editingId, setEditingId] = useState<string | null>(null)
  // Faz 6.8: Vade tarihi düzenleme
  const [dueDateEditId, setDueDateEditId] = useState<string | null>(null)
  const [dueDateValue, setDueDateValue] = useState<Dayjs | null>(null)

  // Etap 6.6: Excel okuma içe aktarma (Source=Import; kısmi başarı modeli).
  const importReadingsMutation = useMutation({
    mutationFn: async (file: File) => {
      const formData = new FormData()
      formData.append('file', file)
      return (
        await apiClient.post<ImportReadingsResult>('/meter-readings/import-excel', formData, {
          headers: { 'Content-Type': 'multipart/form-data' },
        })
      ).data
    },
    onSuccess: (res) => {
      void queryClient.invalidateQueries({ queryKey: ['dashboard-summary'] })
      setImportResult(res)
    },
    onError: () => {
      message.error('İçe aktarma başarısız oldu — dosya formatını kontrol edin.')
    },
  })

  // Etap 6.6: Dönem kilitleme/açma.
  const lockMutation = useMutation({
    mutationFn: async (id: string) =>
      (await apiClient.post(`/gas-invoices/${id}/lock`)).data,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['gas-invoices'] })
      message.success('Dönem kilitlendi — bu döneme artık okuma eklenemez.')
    },
  })

  const unlockMutation = useMutation({
    mutationFn: async (id: string) =>
      (await apiClient.post(`/gas-invoices/${id}/unlock`)).data,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['gas-invoices'] })
      message.success('Dönem kilidi açıldı.')
    },
  })

  // Faz 6.8: Vade tarihi güncelleme
  const setDueDateMutation = useMutation({
    mutationFn: async ({ id, dueDate }: { id: string; dueDate: string }) =>
      (await apiClient.put<InvoiceFinanceResult>(`/gas-invoices/${id}/due-date`, { dueDate })).data,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['gas-invoices'] })
      setDueDateEditId(null)
      message.success('Vade tarihi güncellendi.')
    },
  })

  // Faz 6.8: Ödendi / Ödenmedi işaretleme
  const setPaidMutation = useMutation({
    mutationFn: async ({ id, isPaid, paidOn }: { id: string; isPaid: boolean; paidOn?: string }) =>
      (
        await apiClient.put<InvoiceFinanceResult>(`/gas-invoices/${id}/paid`, {
          isPaid,
          paidOn: paidOn ?? null,
        })
      ).data,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['gas-invoices'] })
      message.success('Ödeme durumu güncellendi.')
    },
  })

  const invoicesQuery = useQuery({
    queryKey: ['gas-invoices'],
    queryFn: async () => (await apiClient.get<GasInvoiceListItem[]>('/gas-invoices')).data,
  })

  const addMutation = useMutation({
    mutationFn: async (req: AddGasInvoiceRequest) =>
      (await apiClient.post<string>('/gas-invoices', req)).data,
    onSuccess: (invoiceId) => {
      void queryClient.invalidateQueries({ queryKey: ['gas-invoices'] })
      setAddOpen(false)
      form.resetFields()
      Modal.success({
        title: 'Fatura kaydedildi',
        content: `Şimdi listedeki "Hesapla" butonuyla 30/70 dağıtımı tetikleyebilirsiniz. (ID: ${invoiceId})`,
        okText: 'Tamam',
      })
    },
  })

  // Etap 6.7: Fatura düzenleme — detayı çek, formu doldur, düzenleme modunda aç.
  const openEdit = async (invoiceId: string) => {
    const detail = (await apiClient.get<GasInvoiceDetail>(`/gas-invoices/${invoiceId}`)).data
    form.setFieldsValue({
      invoiceNumber: detail.invoiceNumber,
      subscriberNumber: detail.subscriberNumber,
      masterMeterSerial: detail.masterMeterSerial,
      readingDateFrom: dayjs(detail.readingDateFrom),
      readingDateTo: dayjs(detail.readingDateTo),
      issuedOn: dayjs(detail.issuedOn),
      correctedVolumeM3: detail.correctedVolumeM3,
      totalEnergyKWh: detail.totalEnergyKWh,
      pressureCorrectionFactor: detail.pressureCorrectionFactor,
      consumptionAmount: detail.consumptionAmount,
      consumptionVat: detail.consumptionVat,
      distributionAmount: detail.distributionAmount,
      distributionVat: detail.distributionVat,
      totalAmount: detail.totalAmount,
      note: detail.note ?? undefined,
    })
    setEditingId(invoiceId)
    setAddOpen(true)
  }

  // Etap 6.7: Fatura güncelleme — hesaplaması varsa "yeniden hesaplayın" uyarısı gösterilir.
  const updateMutation = useMutation({
    mutationFn: async (req: AddGasInvoiceRequest & { note?: string | null }) =>
      (
        await apiClient.put<UpdateGasInvoiceResult>(`/gas-invoices/${editingId}`, req)
      ).data,
    onSuccess: (res) => {
      void queryClient.invalidateQueries({ queryKey: ['gas-invoices'] })
      setAddOpen(false)
      setEditingId(null)
      form.resetFields()
      if (res.hasCalculationWarning) {
        Modal.warning({
          title: 'Fatura güncellendi — YENİDEN HESAPLAMALISINIZ!',
          content:
            'Bu fatura için 30/70 dağıtım hesaplaması mevcut. Tutar/değerler değiştiği için ' +
            'mevcut hesaplama artık güncel değil — lütfen "Hesapla" butonuyla yeniden hesaplayın.',
          okText: 'Anladım',
        })
      } else {
        message.success('Fatura güncellendi.')
      }
    },
    onError: () => {
      message.error('Fatura güncellenemedi — alanları kontrol edin.')
    },
  })

  const calculateMutation = useMutation({
    mutationFn: async (invoiceId: string) =>
      (
        await apiClient.post<CalculateDistributionResult>(
          `/gas-invoices/${invoiceId}/calculate`,
        )
      ).data,
    onSuccess: (calc, invoiceId) => {
      void queryClient.invalidateQueries({ queryKey: ['gas-invoices'] })
      // Kural #1: mutabakat bilgisi yeşil bildirimle gösterilir.
      Modal.success({
        title: calc.reconciled ? 'Hesaplama tamamlandı — MUTABIK ✔' : 'Hesaplama tamamlandı — MUTABAKATSIZ!',
        content: `${calc.apartmentCount} daireye dağıtıldı. Dağıtılan: ${tl(calc.totalDistributedAmount)} / Fatura: ${tl(calc.invoiceTotalAmount)}${calc.failedMeterCount > 0 ? ` — ${calc.failedMeterCount} dairenin okuması alınamadı (tüketim 0 kabul edildi).` : ''}`,
        okText: 'Sonuçları göster',
        onOk: () => void showResult(invoiceId),
      })
    },
  })

  const showResult = async (invoiceId: string) => {
    const response = await apiClient.get<CalculationResultResponse>(
      `/gas-invoices/${invoiceId}/calculation`,
    )
    setResult(response.data)
  }

  const columns: ColumnsType<GasInvoiceListItem> = [
    { title: 'Fatura No', dataIndex: 'invoiceNumber', width: 190 },
    {
      title: 'Dönem',
      width: 230,
      render: (_: unknown, r: GasInvoiceListItem) =>
        `${dayjs(r.periodFrom).format('DD.MM.YYYY')} - ${dayjs(r.periodTo).format('DD.MM.YYYY')}`,
    },
    {
      title: 'Toplam Tutar',
      dataIndex: 'totalAmount',
      width: 150,
      align: 'right',
      render: (v: number) => tl(v),
    },
    {
      title: 'Durum',
      dataIndex: 'hasCalculation',
      width: 170,
      render: (v: boolean, r: GasInvoiceListItem) => (
        <Space size={4} wrap>
          {v ? <Tag color="green">Hesaplandı</Tag> : <Tag color="orange">Hesaplanmadı</Tag>}
          {r.isLocked && (
            <Tag color="red" icon={<LockOutlined />}>
              Kilitli
            </Tag>
          )}
        </Space>
      ),
    },
    {
      title: 'Vade / Ödeme',
      width: 210,
      render: (_: unknown, r: GasInvoiceListItem) => (
        <Space direction="vertical" size={2}>
          <Space size={4}>
            <Typography.Text style={{ fontSize: 12 }}>Vade:</Typography.Text>
            <Button
              type="link"
              size="small"
              style={{ padding: 0, fontSize: 12 }}
              onClick={() => { setDueDateEditId(r.id); setDueDateValue(dayjs(r.dueDate)) }}
            >
              {dayjs(r.dueDate).format('DD.MM.YYYY')}
            </Button>
          </Space>
          {r.isPaid ? (
            <Tag
              color="green"
              style={{ cursor: 'pointer', fontSize: 11 }}
              onClick={() =>
                Modal.confirm({
                  title: 'Ödeme kaydı kaldırılsın mı?',
                  content: `${r.invoiceNumber} faturasının ödeme kaydı silinecek.`,
                  okText: 'Evet',
                  cancelText: 'Hayır',
                  onOk: () => setPaidMutation.mutateAsync({ id: r.id, isPaid: false }),
                })
              }
            >
              ✓ Ödendi ({r.paidOn ? dayjs(r.paidOn).format('DD.MM.YYYY') : '—'})
            </Tag>
          ) : (
            <Button
              size="small"
              type="dashed"
              style={{ fontSize: 11, height: 20, padding: '0 6px' }}
              onClick={() =>
                Modal.confirm({
                  title: `${r.invoiceNumber} faturası ödendi olarak işaretlensin mi?`,
                  content: (
                    <>
                      <Typography.Text>Ödeme tarihi (boş bırakırsanız bugün kaydedilir):</Typography.Text>
                      <DatePicker
                        format="DD.MM.YYYY"
                        style={{ marginTop: 8, width: '100%' }}
                        onChange={(d) => setDueDateValue(d)}
                      />
                    </>
                  ),
                  okText: 'Ödendi İşaretle',
                  cancelText: 'Vazgeç',
                  onOk: () =>
                    setPaidMutation.mutateAsync({
                      id: r.id,
                      isPaid: true,
                      paidOn: (dueDateValue ?? dayjs()).format('YYYY-MM-DD'),
                    }),
                })
              }
            >
              Ödendi İşaretle
            </Button>
          )}
        </Space>
      ),
    },
    {
      title: 'İşlemler',
      width: 300,
      render: (_: unknown, r: GasInvoiceListItem) => (
        <Space wrap>
          <Button
            size="small"
            icon={<CalculatorOutlined />}
            loading={calculateMutation.isPending && calculateMutation.variables === r.id}
            disabled={r.isLocked}
            title={r.isLocked ? 'Kilitli dönem yeniden hesaplanamaz' : undefined}
            onClick={() => calculateMutation.mutate(r.id)}
          >
            Hesapla
          </Button>
          <Button size="small" type="link" disabled={!r.hasCalculation} onClick={() => void showResult(r.id)}>
            Sonuç
          </Button>
          {!r.isLocked && (
            <Button
              size="small"
              type="link"
              icon={<EditOutlined />}
              onClick={() => void openEdit(r.id)}
            >
              Düzenle
            </Button>
          )}
          {!r.isLocked && (
            <Button
              size="small"
              icon={<LockOutlined />}
              disabled={!r.hasCalculation}
              title={r.hasCalculation ? 'Dönemi kilitle' : 'Yalnız hesaplanmış fatura kilitlenebilir'}
              onClick={() =>
                Modal.confirm({
                  title: `Dönem kilirlensin mi? (${r.invoiceNumber})`,
                  content: 'Kilitli döneme okuma eklenemez, Excel içe aktarılamaz ve yeniden hesaplama yapılamaz.',
                  okText: 'Kilitle',
                  cancelText: 'Vazgeç',
                  onOk: () => lockMutation.mutateAsync(r.id),
                })
              }
            >
              Kilitle
            </Button>
          )}
          {r.isLocked && (
            <Button
              size="small"
              danger
              icon={<UnlockOutlined />}
              onClick={() =>
                Modal.confirm({
                  title: 'Dönem kilidi açılsın mı?',
                  content: 'Kilit açıldığında döneme tekrar okuma eklenebilir ve yeniden hesaplama yapılabilir (Faz 7\'de TenantAdmin yetkisi gerekecektir).',
                  okText: 'Kilidi Aç',
                  cancelText: 'Vazgeç',
                  onOk: () => unlockMutation.mutateAsync(r.id),
                })
              }
            >
              Kilidi Aç
            </Button>
          )}
        </Space>
      ),
    },
  ]

  // __MODALS__

  return (
    <Card
      title="Faturalar"
      extra={
        <Space wrap>
          <Button
            size="small"
            icon={<DownloadOutlined />}
            onClick={() => void downloadFile('/meter-readings/import-template', 'Okuma-Import-Sablon.xlsx')}
          >
            Okuma Şablonu İndir
          </Button>
          <Upload
            accept=".xlsx"
            showUploadList={false}
            maxCount={1}
            customRequest={({ file }) => importReadingsMutation.mutate(file as File)}
          >
            <Button size="small" icon={<UploadOutlined />} loading={importReadingsMutation.isPending}>
              Okuma Verilerini İçe Aktar
            </Button>
          </Upload>
          <Button type="primary" icon={<PlusOutlined />} onClick={() => setAddOpen(true)}>
            Yeni Fatura
          </Button>
        </Space>
      }
    >
      <Table
        rowKey="id"
        columns={columns}
        dataSource={invoicesQuery.data}
        loading={invoicesQuery.isLoading}
        size="small"
        pagination={{ pageSize: 10 }}
      />
      <Modal
        title={editingId ? 'Fatura Düzenle' : 'Yeni Doğalgaz Faturası (Akmercan)'}
        open={addOpen}
        onCancel={() => {
          setAddOpen(false)
          setEditingId(null)
        }}
        confirmLoading={editingId ? updateMutation.isPending : addMutation.isPending}
        width={760}
        okText="Kaydet"
        cancelText="İptal"
        onOk={() =>
          form.validateFields().then((values) => {
            const p = values as Record<string, Dayjs | number | string>
            const payload = {
              invoiceNumber: (p.invoiceNumber as string).trim(),
              subscriberNumber: (p.subscriberNumber as string).trim(),
              masterMeterSerial: (p.masterMeterSerial as string).trim(),
              readingDateFrom: (p.readingDateFrom as Dayjs).toISOString(),
              readingDateTo: (p.readingDateTo as Dayjs).toISOString(),
              issuedOn: (p.issuedOn as Dayjs).toISOString(),
              correctedVolumeM3: p.correctedVolumeM3 as number,
              totalEnergyKWh: p.totalEnergyKWh as number,
              pressureCorrectionFactor: p.pressureCorrectionFactor as number,
              consumptionAmount: p.consumptionAmount as number,
              consumptionVat: p.consumptionVat as number,
              distributionAmount: p.distributionAmount as number,
              distributionVat: p.distributionVat as number,
              totalAmount: p.totalAmount as number,
              note: (p.note as string | undefined) ?? null,
            }
            if (editingId) {
              updateMutation.mutate(payload)
            } else {
              addMutation.mutate(payload)
            }
          })
        }
      >
        <Form
          form={form}
          layout="vertical"
          initialValues={{
            subscriberNumber: '10060828',
            masterMeterSerial: '2160191',
            pressureCorrectionFactor: 1.22588,
          }}
        >
          <Row gutter={12}>
            <Col span={8}>
              <Form.Item name="invoiceNumber" label="Fatura No" rules={[{ required: true }]}>
                <Input placeholder="T6772024..." />
              </Form.Item>
            </Col>
            <Col span={8}>
              <Form.Item name="subscriberNumber" label="Abone No" rules={[{ required: true }]}>
                <Input />
              </Form.Item>
            </Col>
            <Col span={8}>
              <Form.Item name="masterMeterSerial" label="Ana Sayaç Seri No" rules={[{ required: true }]}>
                <Input />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={12}>
            <Col span={8}>
              <Form.Item name="readingDateFrom" label="İlk Okuma Tarihi" rules={[{ required: true }]}>
                <DatePicker style={{ width: '100%' }} showTime format="DD.MM.YYYY HH:mm" />
              </Form.Item>
            </Col>
            <Col span={8}>
              <Form.Item name="readingDateTo" label="Son Okuma Tarihi" rules={[{ required: true }]}>
                <DatePicker style={{ width: '100%' }} showTime format="DD.MM.YYYY HH:mm" />
              </Form.Item>
            </Col>
            <Col span={8}>
              <Form.Item name="issuedOn" label="Fatura Tarihi" rules={[{ required: true }]}>
                <DatePicker style={{ width: '100%' }} showTime format="DD.MM.YYYY HH:mm" />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={12}>
            <Col span={8}>
              <Form.Item name="correctedVolumeM3" label="Düzeltilmiş Tüketim (m³)" rules={[{ required: true }]}>
                <InputNumber style={{ width: '100%' }} min={0} decimalSeparator="," />
              </Form.Item>
            </Col>
            <Col span={8}>
              <Form.Item name="totalEnergyKWh" label="Tüketilen Enerji (kWh)" rules={[{ required: true }]}>
                <InputNumber style={{ width: '100%' }} min={0} decimalSeparator="," />
              </Form.Item>
            </Col>
            <Col span={8}>
              <Form.Item name="pressureCorrectionFactor" label="K Katsayısı" rules={[{ required: true }]}>
                <InputNumber style={{ width: '100%' }} min={0} step={0.000001} decimalSeparator="," />
              </Form.Item>
            </Col>
          </Row>
          <Row gutter={12}>
            <Col span={6}>
              <Form.Item name="consumptionAmount" label="Tüketim Bedeli" rules={[{ required: true }]}>
                <InputNumber style={{ width: '100%' }} min={0} decimalSeparator="," />
              </Form.Item>
            </Col>
            <Col span={6}>
              <Form.Item name="consumptionVat" label="Tüketim KDV" rules={[{ required: true }]}>
                <InputNumber style={{ width: '100%' }} min={0} decimalSeparator="," />
              </Form.Item>
            </Col>
            <Col span={6}>
              <Form.Item name="distributionAmount" label="Gecikme Bedeli" rules={[{ required: true }]}>
                <InputNumber style={{ width: '100%' }} min={0} decimalSeparator="," />
              </Form.Item>
            </Col>
            <Col span={6}>
              <Form.Item name="distributionVat" label="Gecikme KDV" rules={[{ required: true }]}>
                <InputNumber style={{ width: '100%' }} min={0} decimalSeparator="," />
              </Form.Item>
            </Col>
          </Row>
          <Form.Item
            name="totalAmount"
            label="Toplam Ödenecek Tutar (G.Toplam — 30/70 dağıtımın temeli)"
            rules={[{ required: true, message: 'Toplam tutar zorunludur.' }]}
          >
            <InputNumber style={{ width: '100%' }} min={0.01} decimalSeparator="," addonAfter="TL" />
          </Form.Item>
          <Typography.Text type="secondary">
            Not: Gecikme bedeli KDV dahil brüt gösterilebilir — kalem toplamı ile G.Toplam
            eşleşmese bile dağıtım G.Toplam üzerinden yapılır (kuruşu kuruşuna mutabakat garantili).
          </Typography.Text>
          <Form.Item name="note" label='Duyuru — PDF "Ek Açıklamalar" kutusuna basılır'>
            <Input.TextArea rows={3} maxLength={1000} showCount placeholder="Örn: Kayıp-kaçak %30 sabit gider içinde eritilmiştir." />
          </Form.Item>
        </Form>
      </Modal>
      <Modal
        title={`Hesaplama Sonucu — ${result?.invoiceNumber ?? ''}`}
        open={result !== null}
        onCancel={() => setResult(null)}
        footer={null}
        width={860}
      >
        {result && (
          <>
            <Row gutter={16} style={{ marginBottom: 16 }}>
              <Col span={8}>
                <Card size="small">
                  <Statistic title="Fatura Tutarı" value={result.invoiceTotalAmount} precision={2} suffix="TL" />
                </Card>
              </Col>
              <Col span={8}>
                <Card size="small">
                  <Statistic
                    title="Dağıtılan"
                    value={result.totalDistributedAmount}
                    precision={2}
                    suffix="TL"
                    valueStyle={{ color: result.reconciled ? '#3f8600' : '#cf1322' }}
                  />
                </Card>
              </Col>
              <Col span={8}>
                <Card size="small">
                  <Statistic
                    title="Mutabakat"
                    value={result.reconciled ? 'KURUŞU KURUŞUNA ✔' : 'HATA'}
                    valueStyle={{ color: result.reconciled ? '#3f8600' : '#cf1322', fontSize: 18 }}
                  />
                </Card>
              </Col>
            </Row>
            <Table
              rowKey="apartmentInvoiceId"
              size="small"
              dataSource={result.rows}
              pagination={{ pageSize: 10 }}
              scroll={{ y: 320 }}
              columns={[
                { title: 'Kapı', dataIndex: 'doorNumber', width: 70 },
                { title: 'Daire', dataIndex: 'apartmentTitle' },
                { title: 'm²', dataIndex: 'squareMetersUsed', width: 80, render: (v: number) => v.toFixed(2) },
                { title: 'kWh', dataIndex: 'consumptionKWhUsed', width: 100, render: (v: number) => v.toFixed(2) },
                {
                  title: 'Sabit (%30)',
                  dataIndex: 'fixedAmount',
                  width: 110,
                  align: 'right',
                  render: (v: number) => tl(v),
                },
                {
                  title: 'Tüketim (%70)',
                  dataIndex: 'consumptionAmount',
                  width: 110,
                  align: 'right',
                  render: (v: number) => tl(v),
                },
                {
                  title: 'Toplam',
                  dataIndex: 'totalAmount',
                  width: 110,
                  align: 'right',
                  render: (v: number) => tl(v),
                },
                {
                  title: 'Okuma',
                  dataIndex: 'isMeterReadFailed',
                  width: 110,
                  render: (v: boolean) =>
                    v ? <Tag color="orange">Okunamadı</Tag> : <Tag color="green">Okundu</Tag>,
                },
              ]}
            />
          </>
        )}
      </Modal>
      <Modal
        title="Okuma İçe Aktarma Sonucu"
        open={importResult !== null}
        onCancel={() => setImportResult(null)}
        footer={<Button type="primary" onClick={() => setImportResult(null)}>Kapat</Button>}
        width={640}
      >
        {importResult && (
          <>
            <List
              size="small"
              dataSource={[
                { label: 'İçe aktarılan okuma', value: importResult.importedCount, ok: true },
                { label: 'Mükerrer (atlandı)', value: importResult.skippedDuplicateCount, ok: true },
                { label: 'Daire/sayaç eşleşmeyen', value: importResult.unmatchedApartmentCount, ok: false },
              ]}
              renderItem={(item) => (
                <List.Item>
                  <Typography.Text>{item.label}</Typography.Text>
                  <Tag color={!item.ok && item.value > 0 ? 'orange' : 'green'}>{item.value}</Tag>
                </List.Item>
              )}
            />
            {importResult.rowErrors.length > 0 && (
              <>
                <Typography.Text type="danger" strong>
                  Hatalı satırlar ({importResult.rowErrors.length}):
                </Typography.Text>
                <List
                  size="small"
                  dataSource={importResult.rowErrors}
                  renderItem={(error) => (
                    <List.Item>
                      <Typography.Text type="danger">{error}</Typography.Text>
                    </List.Item>
                  )}
                />
              </>
            )}
          </>
        )}
      </Modal>
      {/* Faz 6.8: Vade Tarihi Düzenleme Modal */}
      <Modal
        title="Vade Tarihini Güncelle"
        open={!!dueDateEditId}
        onCancel={() => setDueDateEditId(null)}
        confirmLoading={setDueDateMutation.isPending}
        okText="Kaydet"
        cancelText="İptal"
        onOk={() => {
          if (!dueDateValue) { void message.warning('Lütfen tarih seçin.'); return }
          setDueDateMutation.mutate({ id: dueDateEditId!, dueDate: dueDateValue.format('YYYY-MM-DD') })
        }}
      >
        <DatePicker
          style={{ width: '100%' }}
          format="DD.MM.YYYY"
          value={dueDateValue}
          onChange={(d) => setDueDateValue(d)}
        />
      </Modal>
    </Card>
  )
}
