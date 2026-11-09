import { useQuery } from '@tanstack/react-query'
import { Button, Card, Checkbox, DatePicker, Input, Select, Space, Table, Tabs, Tag, Typography } from 'antd'
import {
  AuditOutlined,
  DownloadOutlined,
  FileExcelOutlined,
  FilePdfOutlined,
} from '@ant-design/icons'
import { useMemo, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import dayjs from 'dayjs'
import { apiClient } from '../../api/client'
import { downloadFile } from '../../api/download'
import type {
  CalculationResultResponse,
  GasInvoiceListItem,
  BudgetSettingsDTO,
  NoticeSettingsDTO,
} from '../../types/api'
import {
  AuditApartmentTrackingTab,
  AuditBankTab,
  AuditFuelTab,
  AuditIncomeExpenseTab,
  GeneralAssemblyReportTab,
  useManagementPeriodRange,
} from './AuditReports'

const tl = (v: number) => v.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })

/**
 * Raporlar sayfası: hesaplanmış fatura seç → toplu PDF (72 sayfa), tek daire PDF, Excel indirme,
 * ve aktif yönetim dönemine göre Genel Kurul / Denetim Raporu (PDF / Excel / Dinamik Matris).
 */
export function ReportsPage() {
  const [searchParams] = useSearchParams()
  const [selectedInvoiceId, setSelectedInvoiceId] = useState<string | null>(
    searchParams.get('invoice'),
  )
  const [search, setSearch] = useState('')
  const [onlyFailed, setOnlyFailed] = useState(false)
  const [financeYear, setFinanceYear] = useState(dayjs().year())
  const [showDebtBreakdown, setShowDebtBreakdown] = useState<boolean | null>(null)
  const [showDebtTable, setShowDebtTable] = useState<boolean | null>(null)
  const [noticePageSize, setNoticePageSize] = useState(50)
  const { range } = useManagementPeriodRange()

  const activeFrom = range[0].format('YYYY-MM-DD')
  const activeTo = range[1].format('YYYY-MM-DD')

  const budgetSettingsQuery = useQuery({
    queryKey: ['budget-settings'],
    queryFn: async () => (await apiClient.get<BudgetSettingsDTO>('/settings/budget')).data,
  })
  const budgetEnabled = budgetSettingsQuery.data?.isBudgetTrackingEnabled ?? false

  const noticeSettingsQuery = useQuery({
    queryKey: ['notice-settings'],
    queryFn: async () => (await apiClient.get<NoticeSettingsDTO>('/settings/notice')).data,
  })
  const debtBreakdownOn = showDebtBreakdown ?? noticeSettingsQuery.data?.showDebtBreakdownOnPdf ?? true
  const debtTableOn = showDebtTable ?? noticeSettingsQuery.data?.showDebtTableOnPdf ?? true

  const noticePdfUrl = (invoiceId: string, apartmentId?: string) => {
    const params = new URLSearchParams({
      showDebtBreakdown: String(debtBreakdownOn),
      showDebtTable: String(debtTableOn),
    })
    if (apartmentId) params.set('apartmentId', apartmentId)
    return `/gas-invoices/${invoiceId}/reports/notice.pdf?${params.toString()}`
  }

  const invoicesQuery = useQuery({
    queryKey: ['gas-invoices'],
    queryFn: async () => (await apiClient.get<GasInvoiceListItem[]>('/gas-invoices')).data,
  })

  const calculatedInvoices = (invoicesQuery.data ?? []).filter((i) => i.hasCalculation)

  const selectedInvoice = calculatedInvoices.find((i) => i.id === selectedInvoiceId) ?? null

  const resultQuery = useQuery({
    queryKey: ['calculation-result', selectedInvoiceId],
    enabled: selectedInvoiceId !== null,
    queryFn: async () =>
      (await apiClient.get<CalculationResultResponse>(`/gas-invoices/${selectedInvoiceId}/calculation`)).data,
  })

  const periodLabel = selectedInvoice
    ? `${new Date(selectedInvoice.periodFrom).toLocaleDateString('tr-TR')} - ${new Date(selectedInvoice.periodTo).toLocaleDateString('tr-TR')}`
    : ''

  const filteredRows = useMemo(() => {
    const q = search.trim().toLocaleLowerCase('tr-TR')
    return [...(resultQuery.data?.rows ?? [])]
      .filter((r) => !onlyFailed || r.isMeterReadFailed)
      .filter(
        (r) =>
          q === '' ||
          r.doorNumber.toLocaleLowerCase('tr-TR').includes(q) ||
          r.apartmentTitle.toLocaleLowerCase('tr-TR').includes(q),
      )
      .sort((a, b) => Number(a.doorNumber) - Number(b.doorNumber) || a.doorNumber.localeCompare(b.doorNumber, 'tr'))
  }, [resultQuery.data, search, onlyFailed])

  return (
    <Card
      title="Raporlar ve Denetim Merkezi"
      extra={
        <Space wrap>
          <Tag color="blue">
            Aktif Yönetim Dönemi: {range[0].format('DD.MM.YYYY')} – {range[1].format('DD.MM.YYYY')}
          </Tag>
          <Button
            type="primary"
            icon={<FilePdfOutlined />}
            onClick={() =>
              void downloadFile(
                `/reports/general-assembly.pdf?from=${activeFrom}&to=${activeTo}`,
                `GenelKurulDenetimRaporu-${activeFrom}-${activeTo}.pdf`,
              )
            }
          >
            Genel Kurul / Denetim Raporu (PDF)
          </Button>
          <Button
            icon={<FileExcelOutlined />}
            style={{ color: '#217346', borderColor: '#b7eb8f' }}
            onClick={() =>
              void downloadFile(
                `/reports/general-assembly.xlsx?from=${activeFrom}&to=${activeTo}`,
                `GenelKurulDenetimRaporu-${activeFrom}-${activeTo}.xlsx`,
              )
            }
          >
            Genel Kurul / Denetim Raporu (Excel)
          </Button>
        </Space>
      }
    >
      <Tabs
        defaultActiveKey={searchParams.get('invoice') ? 'notices' : 'general-assembly'}
        items={[
          {
            key: 'general-assembly',
            label: (
              <span>
                <AuditOutlined /> Genel Kurul / Denetim Raporu
              </span>
            ),
            children: <GeneralAssemblyReportTab />,
          },
          {
            key: 'income',
            label: 'Gelir-Gider',
            children: <AuditIncomeExpenseTab />,
          },
          {
            key: 'tracking',
            label: 'Daire Takip',
            children: <AuditApartmentTrackingTab />,
          },
          {
            key: 'fuel',
            label: 'Yakıt Dökümü',
            children: <AuditFuelTab />,
          },
          {
            key: 'bank',
            label: 'Banka Kontrolü',
            children: <AuditBankTab />,
          },
          {
            key: 'notices',
            label: 'Doğalgaz Bildirimleri',
            children: (
              <Space direction="vertical" size={16} style={{ width: '100%' }}>
                <Space wrap>
                  <Typography.Text strong>Fatura:</Typography.Text>
                  <Select
                    style={{ minWidth: 360 }}
                    placeholder="Hesaplanmış fatura seçin"
                    value={selectedInvoiceId ?? undefined}
                    onChange={(value) => setSelectedInvoiceId(value as string)}
                    options={calculatedInvoices.map((i) => ({
                      value: i.id,
                      label: `${i.invoiceNumber} — ${new Date(i.periodFrom).toLocaleDateString('tr-TR')} / ${tl(i.totalAmount)}`,
                    }))}
                    loading={invoicesQuery.isLoading}
                  />
                </Space>

                {selectedInvoice && (
                  <Space wrap>
                    <Checkbox
                      checked={debtBreakdownOn}
                      onChange={(e) => setShowDebtBreakdown(e.target.checked)}
                    >
                      Geçmiş Borç Detaylarını Göster
                    </Checkbox>
                    <Checkbox checked={debtTableOn} onChange={(e) => setShowDebtTable(e.target.checked)}>
                      Faiz Dökümü Tablosunu Göster
                    </Checkbox>
                    <Button
                      type="primary"
                      icon={<FilePdfOutlined />}
                      onClick={() =>
                        void downloadFile(
                          noticePdfUrl(selectedInvoice.id),
                          `BireyselGiderBildirimi-${selectedInvoice.invoiceNumber}.pdf`,
                          300_000,
                        )
                      }
                    >
                      Toplu PDF İndir {resultQuery.data?.rows.length ? `(${resultQuery.data.rows.length} Sayfa)` : ''}
                    </Button>
                    <Button
                      icon={<FileExcelOutlined />}
                      style={{ color: '#217346' }}
                      onClick={() =>
                        void downloadFile(
                          `/gas-invoices/${selectedInvoice.id}/reports/borclar.xlsx`,
                          `Borclistesi-${selectedInvoice.invoiceNumber}.xlsx`,
                        )
                      }
                    >
                      Excel Borç Listesi İndir
                    </Button>
                    <Typography.Text type="secondary">Dönem: {periodLabel}</Typography.Text>
                  </Space>
                )}

                {resultQuery.data && (
                  <Card size="small" title="Tek Daire Bildirimi (PDF)">
                    <Space wrap style={{ marginBottom: 8 }}>
                      <Input.Search
                        allowClear
                        placeholder="Kapı no veya daire adı ara…"
                        style={{ width: 260 }}
                        value={search}
                        onChange={(e) => setSearch(e.target.value)}
                      />
                      <Button
                        type={onlyFailed ? 'primary' : 'default'}
                        onClick={() => setOnlyFailed((v) => !v)}
                      >
                        Sadece okunamayanlar
                      </Button>
                      <Typography.Text type="secondary">
                        {filteredRows.length} / {resultQuery.data.rows.length} daire
                      </Typography.Text>
                    </Space>
                    <Table
                      rowKey="apartmentInvoiceId"
                      size="small"
                      dataSource={filteredRows}
                      pagination={{
                        pageSize: noticePageSize,
                        showSizeChanger: true,
                        pageSizeOptions: ['12', '50', '100'],
                        onChange: (_page, size) => setNoticePageSize(size),
                      }}
                      columns={[
                        {
                          title: 'Kapı',
                          dataIndex: 'doorNumber',
                          width: 80,
                          defaultSortOrder: 'ascend',
                          sorter: (a, b) => Number(a.doorNumber) - Number(b.doorNumber),
                        },
                        { title: 'Daire', dataIndex: 'apartmentTitle' },
                        { title: 'm²', width: 90, render: (_: unknown, r) => r.squareMetersUsed.toFixed(2) },
                        { title: 'kWh', width: 100, render: (_: unknown, r) => r.consumptionKWhUsed.toFixed(2) },
                        {
                          title: 'Toplam',
                          width: 120,
                          align: 'right',
                          render: (_: unknown, r) => tl(r.totalAmount),
                        },
                        {
                          title: 'Bildirim',
                          width: 170,
                          render: (_: unknown, r) => (
                            <Button
                              size="small"
                              icon={<DownloadOutlined />}
                              onClick={() =>
                                void downloadFile(
                                  noticePdfUrl(selectedInvoiceId!, r.apartmentId),
                                  `BireyselGiderBildirimi-Daire${r.doorNumber}.pdf`,
                                  300_000,
                                )
                              }
                            >
                              PDF İndir
                            </Button>
                          ),
                        },
                      ]}
                    />
                    <Typography.Text type="secondary" style={{ display: 'block', marginTop: 8 }}>
                      Σ satır: {tl(resultQuery.data.totalDistributedAmount)} — Fatura:{' '}
                      {tl(resultQuery.data.invoiceTotalAmount)} —{' '}
                      {resultQuery.data.reconciled ? (
                        <Tag color="green">MUTABIK ✔</Tag>
                      ) : (
                        <Tag color="red">MUTABAKATSIZ</Tag>
                      )}
                    </Typography.Text>
                  </Card>
                )}
                <Card size="small" title="Yıllık Bütçe Gerçekleşme Çıktıları">
                  <Space wrap>
                    <Typography.Text strong>Yıl:</Typography.Text>
                    <DatePicker
                      picker="year"
                      allowClear={false}
                      value={dayjs().year(financeYear)}
                      onChange={(value) => {
                        if (value) setFinanceYear(value.year())
                      }}
                    />
                    <Button
                      type="primary"
                      icon={<FilePdfOutlined />}
                      onClick={() =>
                        void downloadFile(
                          `/budget/reports/income-expense.pdf?year=${financeYear}`,
                          `GelirGider-${financeYear}.pdf`,
                        )
                      }
                    >
                      Gelir-Gider PDF
                    </Button>
                    <Button
                      icon={<FileExcelOutlined />}
                      style={{ color: '#217346' }}
                      onClick={() =>
                        void downloadFile(
                          `/budget/reports/income-expense.xlsx?year=${financeYear}`,
                          `GelirGider-${financeYear}.xlsx`,
                        )
                      }
                    >
                      Gelir-Gider Excel
                    </Button>
                    {budgetEnabled && (
                      <>
                        <Button
                          icon={<FilePdfOutlined />}
                          onClick={() =>
                            void downloadFile(
                              `/budget/reports/realization.pdf?year=${financeYear}`,
                              `ButceGerceklesme-${financeYear}.pdf`,
                            )
                          }
                        >
                          Bütçe Gerçekleşme PDF
                        </Button>
                        <Button
                          icon={<FileExcelOutlined />}
                          style={{ color: '#217346' }}
                          onClick={() =>
                            void downloadFile(
                              `/budget/reports/realization.xlsx?year=${financeYear}`,
                              `ButceGerceklesme-${financeYear}.xlsx`,
                            )
                          }
                        >
                          Bütçe Gerçekleşme Excel
                        </Button>
                      </>
                    )}
                  </Space>
                  {!budgetEnabled && (
                    <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
                      Bütçe takibi kapalı. Gerçekleşme raporu, Ayarlar’dan bütçe takibi açılınca indirilebilir.
                    </Typography.Paragraph>
                  )}
                </Card>
              </Space>
            ),
          },
        ]}
      />
    </Card>
  )
}
