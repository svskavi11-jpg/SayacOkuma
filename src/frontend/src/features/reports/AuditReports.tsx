import { useQuery } from '@tanstack/react-query'
import {
  Button,
  Card,
  Col,
  DatePicker,
  Row,
  Select,
  Space,
  Statistic,
  Table,
  Tag,
  Typography,
} from 'antd'
import type { ColumnsType } from 'antd/es/table'
import {
  CalendarOutlined,
  FileExcelOutlined,
  FilePdfOutlined,
  SafetyCertificateOutlined,
} from '@ant-design/icons'
import { useEffect, useState } from 'react'
import dayjs, { type Dayjs } from 'dayjs'
import { apiClient } from '../../api/client'
import { downloadFile } from '../../api/download'
import type {
  ApartmentTrackingReport,
  AuditIncomeExpenseReport,
  BankAccountListItem,
  BankReconciliationReport,
  FuelBreakdownReport,
  GasInvoiceListItem,
  GeneralAssemblyApartmentMatrixRow,
  GeneralAssemblyMonthlySummaryRow,
  GeneralAssemblyReport,
  ManagementPeriodSettingsDTO,
} from '../../types/api'

const { RangePicker } = DatePicker

const tl = (value: number) =>
  value.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })

const dateText = (value: string) => dayjs(value).format('DD.MM.YYYY')

export type RangeValue = [Dayjs, Dayjs]

export function useManagementPeriodRange(enabled = true) {
  const [range, setRange] = useState<RangeValue>([dayjs().startOf('year'), dayjs().endOf('year')])
  const [initialized, setInitialized] = useState(false)

  const periodQuery = useQuery({
    queryKey: ['management-period-settings'],
    queryFn: async () =>
      (await apiClient.get<ManagementPeriodSettingsDTO>('/settings/management-period')).data,
    enabled,
  })

  useEffect(() => {
    if (periodQuery.data && !initialized) {
      setRange([dayjs(periodQuery.data.startDate), dayjs(periodQuery.data.endDate)])
      setInitialized(true)
    }
  }, [periodQuery.data, initialized])

  const resetToActivePeriod = () => {
    if (periodQuery.data) {
      setRange([dayjs(periodQuery.data.startDate), dayjs(periodQuery.data.endDate)])
    }
  }

  return { range, setRange, resetToActivePeriod, periodData: periodQuery.data }
}

function moneyColumn<T>(title: string, dataIndex: string, color?: string, width = 130) {
  return {
    title,
    dataIndex,
    width,
    align: 'right' as const,
    render: (value: number) => <span style={color ? { color } : undefined}>{tl(value ?? 0)}</span>,
  } satisfies ColumnsType<T>[number]
}

function netText(value: number) {
  const color = value > 0 ? '#cf1322' : value < 0 ? '#389e0d' : undefined
  return <span style={{ color, fontWeight: 600 }}>{tl(value)}</span>
}

function ExcelButton({ url, fileName, label = 'Excel İndir' }: { url: string; fileName: string; label?: string }) {
  return (
    <Button
      icon={<FileExcelOutlined />}
      style={{ color: '#217346', borderColor: '#b7eb8f' }}
      onClick={() => void downloadFile(url, fileName)}
    >
      {label}
    </Button>
  )
}

export function GeneralAssemblyReportTab() {
  const { range, setRange, resetToActivePeriod } = useManagementPeriodRange()
  const from = range[0].format('YYYY-MM-DD')
  const to = range[1].format('YYYY-MM-DD')

  const query = useQuery({
    queryKey: ['general-assembly-report', from, to],
    queryFn: async () =>
      (
        await apiClient.get<GeneralAssemblyReport>(
          `/reports/general-assembly?from=${from}&to=${to}`,
        )
      ).data,
  })

  const report = query.data
  const monthColumns = report?.monthColumns ?? []

  const monthlySummaryCols: ColumnsType<GeneralAssemblyMonthlySummaryRow> = [
    {
      title: 'Kalem',
      dataIndex: 'rowTitle',
      width: 190,
      fixed: 'left',
      render: (v: string) => <Typography.Text strong>{v}</Typography.Text>,
    },
    ...monthColumns.map((col) => ({
      title: col.label,
      key: col.key,
      width: 135,
      align: 'right' as const,
      render: (_: unknown, row: GeneralAssemblyMonthlySummaryRow) => {
        const val = row.monthValues[col.key] ?? 0
        const isNet = row.rowTitle.includes('Net')
        const color = isNet ? (val >= 0 ? '#389e0d' : '#cf1322') : undefined
        return <span style={{ color, fontWeight: isNet ? 600 : 400 }}>{tl(val)}</span>
      },
    })),
    {
      title: 'DÖNEM TOPLAMI',
      dataIndex: 'total',
      width: 150,
      align: 'right',
      fixed: 'right',
      render: (val: number) => <Typography.Text strong>{tl(val)}</Typography.Text>,
    },
  ]

  const apartmentMatrixCols: ColumnsType<GeneralAssemblyApartmentMatrixRow> = [
    { title: 'Kapı', dataIndex: 'doorNumber', width: 70, fixed: 'left' },
    { title: 'Daire', dataIndex: 'title', width: 170, fixed: 'left' },
    {
      title: 'Dönem Başı Devir',
      dataIndex: 'openingBalance',
      width: 135,
      align: 'right',
      render: (v: number) => tl(v),
    },
    ...monthColumns.map((col) => ({
      title: `${col.label} Tahsilat`,
      key: col.key,
      width: 145,
      align: 'right' as const,
      render: (_: unknown, row: GeneralAssemblyApartmentMatrixRow) => {
        const val = row.monthlyCollections[col.key] ?? 0
        return (
          <span style={{ color: val > 0 ? '#389e0d' : '#999' }}>
            {val > 0 ? tl(val) : '—'}
          </span>
        )
      },
    })),
    {
      title: 'Toplam Tahakkuk',
      dataIndex: 'totalAccrual',
      width: 140,
      align: 'right',
      render: (v: number) => <span style={{ color: '#722ed1' }}>{tl(v)}</span>,
    },
    {
      title: 'Toplam Tahsilat',
      dataIndex: 'totalCollected',
      width: 140,
      align: 'right',
      render: (v: number) => <span style={{ color: '#389e0d' }}>{tl(v)}</span>,
    },
    {
      title: 'Görev Mahsubu',
      dataIndex: 'totalWaiver',
      width: 135,
      align: 'right',
      render: (v: number) => <span style={{ color: '#13c2c2' }}>{tl(v)}</span>,
    },
    {
      title: 'Dönem Sonu Bakiye',
      dataIndex: 'closingBalance',
      width: 150,
      align: 'right',
      fixed: 'right',
      render: (v: number) => netText(v),
    },
  ]

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Space wrap>
        <Typography.Text strong>Yönetim Dönemi:</Typography.Text>
        <RangePicker
          allowClear={false}
          format="DD.MM.YYYY"
          value={range}
          onChange={(value) => {
            if (value?.[0] && value[1]) setRange([value[0], value[1]])
          }}
        />
        <Button icon={<CalendarOutlined />} onClick={resetToActivePeriod}>
          Aktif Dönem
        </Button>
        <Button
          type="primary"
          icon={<FilePdfOutlined />}
          onClick={() =>
            void downloadFile(
              `/reports/general-assembly.pdf?from=${from}&to=${to}`,
              `GenelKurulDenetimRaporu-${from}-${to}.pdf`,
            )
          }
        >
          Genel Kurul / Denetim Raporu (PDF)
        </Button>
        <ExcelButton
          url={`/reports/general-assembly.xlsx?from=${from}&to=${to}`}
          fileName={`GenelKurulDenetimRaporu-${from}-${to}.xlsx`}
          label="Genel Kurul / Denetim Raporu (Excel)"
        />
        {report && (
          <Tag
            icon={<SafetyCertificateOutlined />}
            color={report.reconciled ? 'success' : 'error'}
          >
            {report.reconciled ? 'Kuruşu Kuruşuna Mutabık' : 'Mutabakatsız'}
          </Tag>
        )}
      </Space>

      {report && (
        <Row gutter={[12, 12]}>
          <Col xs={12} md={4}>
            <Card size="small">
              <Statistic
                title="Toplam Gelir (Tahsilat)"
                value={report.totalIncome}
                precision={2}
                suffix="TL"
                valueStyle={{ color: '#389e0d', fontSize: 18 }}
              />
            </Card>
          </Col>
          <Col xs={12} md={4}>
            <Card size="small">
              <Statistic
                title="Nakit / Banka Gideri"
                value={report.totalCashExpense}
                precision={2}
                suffix="TL"
                valueStyle={{ color: '#cf1322', fontSize: 18 }}
              />
            </Card>
          </Col>
          <Col xs={12} md={4}>
            <Card size="small">
              <Statistic
                title="Görev Mahsubu"
                value={report.totalWaiverExpense}
                precision={2}
                suffix="TL"
                valueStyle={{ color: '#13c2c2', fontSize: 18 }}
              />
            </Card>
          </Col>
          <Col xs={12} md={4}>
            <Card size="small">
              <Statistic
                title="Toplam Gerçekleşen Gider"
                value={report.totalActualExpense}
                precision={2}
                suffix="TL"
                valueStyle={{ color: '#cf1322', fontSize: 18 }}
              />
            </Card>
          </Col>
          <Col xs={12} md={4}>
            <Card size="small">
              <Statistic
                title="Dönem Sonu Banka"
                value={report.totalBankClosingBalance}
                precision={2}
                suffix="TL"
                valueStyle={{ color: '#0958d9', fontSize: 18 }}
              />
            </Card>
          </Col>
          <Col xs={12} md={4}>
            <Card size="small">
              <Statistic
                title="Dairelerden Alacaklar"
                value={report.totalReceivables}
                precision={2}
                suffix="TL"
                valueStyle={{ color: '#d46b08', fontSize: 18 }}
              />
            </Card>
          </Col>
        </Row>
      )}

      <Card
        size="small"
        title={`Dinamik Aylık Gelir - Gider Matrisi (${monthColumns.length} Ay)`}
      >
        <Table
          rowKey="rowTitle"
          size="small"
          loading={query.isLoading}
          dataSource={report?.monthlySummaryRows ?? []}
          columns={monthlySummaryCols}
          pagination={false}
          scroll={{ x: Math.max(900, 360 + monthColumns.length * 140) }}
        />
      </Card>

      <Card
        size="small"
        title="Daire Bazlı Dinamik Aylık Tahsilat ve Dönem Sonu Bakiye Matrisi"
      >
        <Table
          rowKey="apartmentId"
          size="small"
          loading={query.isLoading}
          dataSource={report?.apartmentMatrixRows ?? []}
          columns={apartmentMatrixCols}
          pagination={{ pageSize: 20, showSizeChanger: false }}
          scroll={{ x: Math.max(1200, 950 + monthColumns.length * 145) }}
        />
      </Card>
    </Space>
  )
}

export function AuditIncomeExpenseTab() {
  const { range, setRange, resetToActivePeriod } = useManagementPeriodRange()
  const from = range[0].format('YYYY-MM-DD')
  const to = range[1].format('YYYY-MM-DD')

  const query = useQuery({
    queryKey: ['audit-income-expense', from, to],
    queryFn: async () =>
      (
        await apiClient.get<AuditIncomeExpenseReport>(
          `/reports/income-expense?from=${from}&to=${to}`,
        )
      ).data,
  })
  const report = query.data

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Space wrap>
        <Typography.Text strong>Yönetim Dönemi:</Typography.Text>
        <RangePicker
          allowClear={false}
          format="DD.MM.YYYY"
          value={range}
          onChange={(value) => {
            if (value?.[0] && value[1]) setRange([value[0], value[1]])
          }}
        />
        <Button icon={<CalendarOutlined />} onClick={resetToActivePeriod}>
          Aktif Dönem
        </Button>
        <ExcelButton
          url={`/reports/income-expense.xlsx?from=${from}&to=${to}`}
          fileName={`GelirGiderOzet-${from}-${to}.xlsx`}
        />
      </Space>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
        Tahsilatlar cari hesabın dağıtım sırasına göre aidat, yakıt, devir ve bağlanmamış olarak ayrılır.
        Görev mahsubu gider satırının içinde kalır. Banka tablosu yalnızca bankası seçilmiş hareketleri sayar;
        nakit tahsilat gelire girer, banka bakiyesine girmez.
      </Typography.Paragraph>
      <Table
        rowKey="category"
        size="small"
        loading={query.isLoading}
        dataSource={report?.expenses ?? []}
        pagination={false}
        scroll={{ x: 900 }}
        columns={[
          { title: 'Kalem', dataIndex: 'category' },
          moneyColumn('Tahmini', 'estimated'),
          moneyColumn('Gider', 'expenseAmount'),
          moneyColumn('Mahsup', 'waiverAmount', '#13c2c2'),
          moneyColumn('Gerçekleşen', 'actual'),
          moneyColumn('Kalan', 'remaining'),
        ]}
        summary={() =>
          report ? (
            <Table.Summary.Row>
              <Table.Summary.Cell index={0}>Toplam</Table.Summary.Cell>
              <Table.Summary.Cell index={1} align="right">{tl(report.totalEstimated)}</Table.Summary.Cell>
              <Table.Summary.Cell index={2} />
              <Table.Summary.Cell index={3} />
              <Table.Summary.Cell index={4} align="right">{tl(report.totalActual)}</Table.Summary.Cell>
              <Table.Summary.Cell index={5} align="right">{tl(report.totalRemaining)}</Table.Summary.Cell>
            </Table.Summary.Row>
          ) : null
        }
      />
      <Table
        scroll={{ x: 'max-content' }}
        rowKey="title"
        size="small"
        loading={query.isLoading}
        dataSource={report?.income ?? []}
        pagination={false}
        columns={[
          { title: 'Gelir', dataIndex: 'title' },
          {
            title: 'Tutar',
            dataIndex: 'amount',
            align: 'right',
            width: 160,
            render: (value: number, row) => (
              <span
                style={{
                  color: row.title.startsWith('Aidat')
                    ? '#722ed1'
                    : row.title.startsWith('Yakıt')
                      ? '#1677ff'
                      : undefined,
                }}
              >
                {tl(value)}
              </span>
            ),
          },
        ]}
        summary={() =>
          report ? (
            <Table.Summary.Row>
              <Table.Summary.Cell index={0}>Net (gelir − gerçekleşen gider)</Table.Summary.Cell>
              <Table.Summary.Cell index={1} align="right">{netText(report.net)}</Table.Summary.Cell>
            </Table.Summary.Row>
          ) : null
        }
      />
      <Table
        rowKey="bankAccountId"
        size="small"
        loading={query.isLoading}
        dataSource={report?.banks ?? []}
        pagination={false}
        scroll={{ x: 800 }}
        columns={[
          { title: 'Banka', dataIndex: 'label' },
          moneyColumn('Açılış', 'opening'),
          moneyColumn('Giren', 'moneyIn', '#389e0d'),
          moneyColumn('Çıkan', 'moneyOut', '#cf1322'),
          moneyColumn('Kapanış', 'closing'),
        ]}
      />
    </Space>
  )
}

export function AuditApartmentTrackingTab() {
  const { range, setRange, resetToActivePeriod } = useManagementPeriodRange()
  const from = range[0].format('YYYY-MM-DD')
  const to = range[1].format('YYYY-MM-DD')
  const query = useQuery({
    queryKey: ['audit-apartment-tracking', from, to],
    queryFn: async () =>
      (await apiClient.get<ApartmentTrackingReport>(`/reports/apartment-tracking?from=${from}&to=${to}`)).data,
  })
  const report = query.data
  const bridge = report
    && (report.periodOpening !== 0 || report.openingEntered !== 0 || report.openingCollected !== 0 || report.unallocated !== 0)

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Space wrap>
        <Typography.Text strong>Yönetim Dönemi:</Typography.Text>
        <RangePicker
          allowClear={false}
          format="DD.MM.YYYY"
          value={range}
          onChange={(value) => {
            if (value?.[0] && value[1]) setRange([value[0], value[1]])
          }}
        />
        <Button icon={<CalendarOutlined />} onClick={resetToActivePeriod}>
          Aktif Dönem
        </Button>
        <ExcelButton
          url={`/reports/apartment-tracking.xlsx?from=${from}&to=${to}`}
          fileName={`DaireTakip-${from}-${to}.xlsx`}
        />
        {report && (
          <Tag color={report.reconciled ? 'green' : 'red'}>
            {report.reconciled ? 'Mutabık' : 'Mutabakatsız'}
          </Tag>
        )}
      </Space>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
        Devir sütunu dönem başı cari bakiyedir. Dönemde girilen devir bu sütuna eklenir; devir borcuna düşen
        tahsilat ve bağlanmamış tutar düşülür. Net bakiye, dönem sonundaki cari hesap bakiyesidir. Gecikme faizi,
        dönem içinde işleyen faizdir.
      </Typography.Paragraph>
      {bridge && report && (
        <Typography.Text type="secondary">
          Dönem başı {tl(report.periodOpening)} · dönemde girilen devir {tl(report.openingEntered)} · devir
          tahsilatı {tl(report.openingCollected)} · bağlanmamış {tl(report.unallocated)}
        </Typography.Text>
      )}
      <Table
        rowKey="apartmentId"
        size="small"
        loading={query.isLoading}
        dataSource={report?.rows ?? []}
        pagination={{ pageSize: 20, showSizeChanger: false }}
        scroll={{ x: 1500 }}
        columns={[
          { title: 'Kapı', dataIndex: 'doorNumber', width: 70, fixed: 'left' },
          { title: 'Daire', dataIndex: 'title', width: 180, fixed: 'left' },
          moneyColumn('Devir', 'devir'),
          moneyColumn('Ek Aidat', 'extraAidat', '#722ed1'),
          moneyColumn('Aidat Tahakkuku', 'aidatAccrual', '#722ed1', 150),
          moneyColumn('Yakıt Tahakkuku', 'fuelAccrual', '#1677ff', 150),
          moneyColumn('Aidat Tahsilatı', 'aidatCollected', '#722ed1', 150),
          moneyColumn('Yakıt Tahsilatı', 'fuelCollected', '#1677ff', 150),
          moneyColumn('Görev Mahsubu', 'waiver', '#13c2c2', 140),
          moneyColumn('Gecikme Faizi', 'interest', '#fa8c16', 140),
          {
            title: 'Net Bakiye',
            dataIndex: 'net',
            width: 140,
            align: 'right',
            fixed: 'right',
            render: (value: number) => netText(value),
          },
        ]}
        summary={() =>
          report ? (
            <Table.Summary.Row>
              <Table.Summary.Cell index={0} colSpan={2}>Toplam</Table.Summary.Cell>
              <Table.Summary.Cell index={2} align="right">{tl(report.totals.devir)}</Table.Summary.Cell>
              <Table.Summary.Cell index={3} align="right">{tl(report.totals.extraAidat)}</Table.Summary.Cell>
              <Table.Summary.Cell index={4} align="right">{tl(report.totals.aidatAccrual)}</Table.Summary.Cell>
              <Table.Summary.Cell index={5} align="right">{tl(report.totals.fuelAccrual)}</Table.Summary.Cell>
              <Table.Summary.Cell index={6} align="right">{tl(report.totals.aidatCollected)}</Table.Summary.Cell>
              <Table.Summary.Cell index={7} align="right">{tl(report.totals.fuelCollected)}</Table.Summary.Cell>
              <Table.Summary.Cell index={8} align="right">{tl(report.totals.waiver)}</Table.Summary.Cell>
              <Table.Summary.Cell index={9} align="right">{tl(report.totals.interest)}</Table.Summary.Cell>
              <Table.Summary.Cell index={10} align="right">{netText(report.totals.net)}</Table.Summary.Cell>
            </Table.Summary.Row>
          ) : null
        }
      />
    </Space>
  )
}

export function AuditFuelTab() {
  const { range, setRange, resetToActivePeriod } = useManagementPeriodRange()
  const [gasInvoiceId, setGasInvoiceId] = useState<string | undefined>()
  const from = range[0].format('YYYY-MM-DD')
  const to = range[1].format('YYYY-MM-DD')
  const invoicesQuery = useQuery({
    queryKey: ['gas-invoices'],
    queryFn: async () => (await apiClient.get<GasInvoiceListItem[]>('/gas-invoices')).data,
  })
  const query = useQuery({
    queryKey: ['audit-fuel', from, to, gasInvoiceId ?? ''],
    queryFn: async () => {
      const invoice = gasInvoiceId ? `&gasInvoiceId=${gasInvoiceId}` : ''
      return (await apiClient.get<FuelBreakdownReport>(`/reports/fuel?from=${from}&to=${to}${invoice}`)).data
    },
  })
  const report = query.data

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Space wrap>
        <Typography.Text strong>Yönetim Dönemi:</Typography.Text>
        <RangePicker
          allowClear={false}
          format="DD.MM.YYYY"
          value={range}
          onChange={(value) => {
            if (value?.[0] && value[1]) setRange([value[0], value[1]])
          }}
        />
        <Button icon={<CalendarOutlined />} onClick={resetToActivePeriod}>
          Aktif Dönem
        </Button>
        <Select
          allowClear
          style={{ minWidth: 280 }}
          placeholder="Tüm yakıt faturaları"
          value={gasInvoiceId}
          onChange={(value) => setGasInvoiceId(value)}
          options={(invoicesQuery.data ?? [])
            .filter((invoice) => invoice.hasCalculation)
            .map((invoice) => ({
              value: invoice.id,
              label: `${invoice.invoiceNumber} — ${dateText(invoice.periodFrom)}`,
            }))}
        />
        <ExcelButton
          url={`/reports/fuel.xlsx?from=${from}&to=${to}${gasInvoiceId ? `&gasInvoiceId=${gasInvoiceId}` : ''}`}
          fileName={`YakitDokumu-${from}-${to}.xlsx`}
        />
        {report && (
          <Tag color={report.reconciled ? 'green' : 'red'}>
            {report.reconciled ? 'Mutabık' : 'Mutabakatsız'}
          </Tag>
        )}
      </Space>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
        Bu liste dairelere borç yazılan yakıt payıdır. Binanın doğalgaz faturası gelir-gider satırında kalır ve
        buradan düşülmez. Toplam, aynı dönemdeki daire takip listesinin yakıt tahakkuku ile aynıdır.
      </Typography.Paragraph>
      <Table
        rowKey={(row) => `${row.gasInvoiceId}-${row.doorNumber}`}
        size="small"
        loading={query.isLoading}
        dataSource={report?.rows ?? []}
        pagination={{ pageSize: 20, showSizeChanger: false }}
        scroll={{ x: 1100 }}
        columns={[
          { title: 'Fatura', dataIndex: 'invoiceNumber', width: 140 },
          {
            title: 'Dönem',
            width: 190,
            render: (_: unknown, row) => `${dateText(row.periodFrom)} - ${dateText(row.periodTo)}`,
          },
          { title: 'Kapı', dataIndex: 'doorNumber', width: 70 },
          { title: 'Daire', dataIndex: 'title', width: 180 },
          { title: 'm²', dataIndex: 'squareMeters', width: 90, align: 'right', render: (value: number) => value.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) },
          { title: 'kWh', dataIndex: 'consumptionKWh', width: 100, align: 'right', render: (value: number) => value.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) },
          moneyColumn('Sabit', 'fixedAmount', '#1677ff'),
          moneyColumn('Tüketim', 'consumptionAmount', '#1677ff'),
          moneyColumn('Daire toplamı', 'totalAmount', '#1677ff', 140),
        ]}
        summary={() =>
          report ? (
            <Table.Summary.Row>
              <Table.Summary.Cell index={0} colSpan={8}>Toplam</Table.Summary.Cell>
              <Table.Summary.Cell index={8} align="right">{tl(report.total)}</Table.Summary.Cell>
            </Table.Summary.Row>
          ) : null
        }
      />
    </Space>
  )
}

export function AuditBankTab() {
  const { range, setRange, resetToActivePeriod } = useManagementPeriodRange()
  const [bankAccountId, setBankAccountId] = useState<string | undefined>()
  const from = range[0].format('YYYY-MM-DD')
  const to = range[1].format('YYYY-MM-DD')
  const accountsQuery = useQuery({
    queryKey: ['bank-accounts'],
    queryFn: async () => (await apiClient.get<BankAccountListItem[]>('/bank-accounts')).data,
  })
  const query = useQuery({
    queryKey: ['audit-bank', bankAccountId, from, to],
    enabled: Boolean(bankAccountId),
    queryFn: async () =>
      (await apiClient.get<BankReconciliationReport>(
        `/reports/bank?bankAccountId=${bankAccountId}&from=${from}&to=${to}`,
      )).data,
  })
  const report = query.data

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Space wrap>
        <Select
          style={{ minWidth: 320 }}
          placeholder="Banka hesabı seçin"
          value={bankAccountId}
          onChange={(value) => setBankAccountId(value)}
          options={(accountsQuery.data ?? []).map((account) => ({
            value: account.id,
            label: `${account.bankName} — ${account.accountName}`,
          }))}
          loading={accountsQuery.isLoading}
        />
        <RangePicker
          allowClear={false}
          format="DD.MM.YYYY"
          value={range}
          onChange={(value) => {
            if (value?.[0] && value[1]) setRange([value[0], value[1]])
          }}
        />
        <Button icon={<CalendarOutlined />} onClick={resetToActivePeriod}>
          Aktif Dönem
        </Button>
        {bankAccountId && (
          <ExcelButton
            url={`/reports/bank.xlsx?bankAccountId=${bankAccountId}&from=${from}&to=${to}`}
            fileName={`BankaKontrol-${from}-${to}.xlsx`}
          />
        )}
      </Space>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 0 }}>
        Açılış, seçilen günün bir önceki bakiyesidir. İptal edilen tahsilatlar ve görev mahsubu bu listede yoktur.
        Bankası boş olan tahsilat ve giderler de bu hesaba girmez.
      </Typography.Paragraph>
      {report && (
        <Space wrap>
          <Tag>Açılış {tl(report.opening)}</Tag>
          <Tag color="green">Giren {tl(report.moneyIn)}</Tag>
          <Tag color="red">Çıkan {tl(report.moneyOut)}</Tag>
          <Tag color="blue">Kapanış {tl(report.closing)}</Tag>
        </Space>
      )}
      <Table
        scroll={{ x: 'max-content' }}
        rowKey={(row) => `${row.date}-${row.kind}-${row.description}-${row.moneyIn}-${row.moneyOut}`}
        size="small"
        loading={query.isFetching}
        dataSource={report?.movements ?? []}
        pagination={{ pageSize: 20, showSizeChanger: false }}
        locale={{ emptyText: bankAccountId ? 'Bu aralıkta hareket yok' : 'Banka hesabı seçin' }}
        columns={[
          { title: 'Tarih', dataIndex: 'date', width: 120, render: (value: string) => dateText(value) },
          { title: 'Tür', dataIndex: 'kind', width: 140 },
          { title: 'Açıklama', dataIndex: 'description' },
          moneyColumn('Giren', 'moneyIn', '#389e0d'),
          moneyColumn('Çıkan', 'moneyOut', '#cf1322'),
        ]}
      />
    </Space>
  )
}
