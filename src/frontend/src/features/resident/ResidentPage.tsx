import { useQuery } from '@tanstack/react-query'
import { Alert, Card, Col, Empty, Row, Space, Spin, Statistic, Table, Tabs, Tag, Typography, theme } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { FilePdfOutlined, WalletOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import { apiClient } from '../../api/client'
import { downloadFile } from '../../api/download'

interface ResidentLastPayment {
  date: string
  amount: number
}

interface ResidentLedgerRow {
  date: string
  kind: string
  description: string
  amount: number
  remaining: number | null
  paymentId?: string | null
}

interface ResidentLedger {
  doorNumber: string
  apartmentTitle: string
  asOfDate: string
  totalBalance: number
  balanceStatus: string
  hasDebt: boolean
  lastPayment: ResidentLastPayment | null
  rows: ResidentLedgerRow[]
}

interface ResidentInvoice {
  id: string
  kind: 'Heating' | 'Aidat'
  periodLabel: string
  dueDate: string
  amount: number
  paid: number
  remaining: number
  pdfUrl: string | null
}

const tl = (value: number) =>
  value.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })

const day = (value: string) => dayjs(value).format('DD.MM.YYYY')

function kindColor(kind: string) {
  if (kind === 'Tahsilat' || kind === 'Mahsup') return 'green'
  if (kind === 'Faiz') return 'orange'
  if (kind === 'Aidat') return 'blue'
  if (kind === 'Isınma') return 'volcano'
  return 'default'
}

export function ResidentPage() {
  const { token } = theme.useToken()
  const ledgerQuery = useQuery({
    queryKey: ['resident-ledger'],
    queryFn: async () => (await apiClient.get<ResidentLedger>('/resident/ledger')).data,
  })
  const invoiceQuery = useQuery({
    queryKey: ['resident-invoices'],
    queryFn: async () => (await apiClient.get<ResidentInvoice[]>('/resident/invoices')).data,
  })

  const ledger = ledgerQuery.data
  const balanceColor = ledger?.hasDebt
    ? token.colorError
    : ledger && ledger.totalBalance < 0
      ? token.colorSuccess
      : token.colorText

  const statementColumns: ColumnsType<ResidentLedgerRow> = [
    { title: 'Tarih', dataIndex: 'date', width: 120, render: (value: string) => day(value) },
    {
      title: 'Tür',
      dataIndex: 'kind',
      width: 110,
      render: (value: string) => <Tag color={kindColor(value)}>{value}</Tag>,
    },
    { title: 'Açıklama', dataIndex: 'description' },
    {
      title: 'Tutar',
      dataIndex: 'amount',
      align: 'right',
      width: 140,
      render: (value: number) => tl(value),
    },
    {
      title: 'Kalan',
      dataIndex: 'remaining',
      align: 'right',
      width: 140,
      render: (value: number | null) => (value === null ? '—' : tl(value)),
    },
    {
      title: '',
      width: 130,
      render: (_: unknown, row) =>
        row.kind === 'Tahsilat' && row.paymentId ? (
          <Typography.Link onClick={() => downloadFile(`/resident/payments/${row.paymentId}/receipt.pdf`, 'Makbuz.pdf')}>
            <FilePdfOutlined /> Makbuz
          </Typography.Link>
        ) : null,
    },
  ]

  const invoiceColumns: ColumnsType<ResidentInvoice> = [
    {
      title: 'Tür',
      dataIndex: 'kind',
      width: 110,
      render: (value: ResidentInvoice['kind']) => (
        <Tag color={value === 'Heating' ? 'volcano' : 'blue'}>{value === 'Heating' ? 'Isınma' : 'Aidat'}</Tag>
      ),
    },
    { title: 'Dönem', dataIndex: 'periodLabel' },
    { title: 'Vade', dataIndex: 'dueDate', width: 120, render: (value: string) => day(value) },
    { title: 'Tutar', dataIndex: 'amount', align: 'right', width: 130, render: (value: number) => tl(value) },
    { title: 'Ödenen', dataIndex: 'paid', align: 'right', width: 130, render: (value: number) => tl(value) },
    { title: 'Kalan', dataIndex: 'remaining', align: 'right', width: 130, render: (value: number) => tl(value) },
    {
      title: '',
      width: 120,
      render: (_: unknown, record) =>
        record.pdfUrl ? (
          <Typography.Link onClick={() => downloadFile(record.pdfUrl!, `${record.periodLabel}.pdf`)}>
            <FilePdfOutlined /> PDF
          </Typography.Link>
        ) : null,
    },
  ]

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <div>
        <Typography.Title level={3} style={{ marginBottom: 0 }}>
          Hesabım
        </Typography.Title>
        <Typography.Text type="secondary">
          {ledger ? `Daire ${ledger.doorNumber} · ${ledger.apartmentTitle}` : 'Borç, tahsilat ve faturalarınız'}
        </Typography.Text>
      </div>

      {ledgerQuery.isLoading ? (
        <div style={{ padding: 48, textAlign: 'center' }}>
          <Spin />
        </div>
      ) : ledgerQuery.isError || !ledger ? (
        <Empty description="Hesap özeti yüklenemedi." />
      ) : (
        <>
          <Row gutter={[16, 16]}>
            <Col xs={24} md={8}>
              <Card style={{ borderRadius: 16, height: '100%' }}>
                <Statistic
                  title="Net bakiye"
                  value={Math.abs(ledger.totalBalance)}
                  precision={2}
                  suffix="TL"
                  prefix={<WalletOutlined />}
                  valueStyle={{ color: balanceColor }}
                />
                <Tag color={ledger.hasDebt ? 'error' : ledger.totalBalance < 0 ? 'success' : 'default'} style={{ marginTop: 12 }}>
                  {ledger.balanceStatus}
                </Tag>
              </Card>
            </Col>
            <Col xs={24} md={8}>
              <Card style={{ borderRadius: 16, height: '100%' }}>
                <Statistic
                  title="Son tahsilat"
                  value={ledger.lastPayment ? ledger.lastPayment.amount : 0}
                  precision={2}
                  suffix="TL"
                />
                <Typography.Text type="secondary">
                  {ledger.lastPayment ? day(ledger.lastPayment.date) : 'Kayıtlı tahsilat yok'}
                </Typography.Text>
              </Card>
            </Col>
            <Col xs={24} md={8}>
              <Card style={{ borderRadius: 16, height: '100%' }}>
                <Typography.Text type="secondary">Hesap tarihi</Typography.Text>
                <Typography.Title level={4} style={{ margin: '8px 0' }}>
                  {day(ledger.asOfDate)}
                </Typography.Title>
                <Typography.Text>Ekstre bugünkü bakiyeyi gösterir.</Typography.Text>
              </Card>
            </Col>
          </Row>

          {ledger.hasDebt && (
            <Alert
              type="warning"
              showIcon
              message="Ödenmemiş bakiyeniz var"
              description={`Güncel borç ${tl(ledger.totalBalance)}. Ödeme için site yönetimiyle iletişime geçebilirsiniz.`}
            />
          )}

          <Card style={{ borderRadius: 16 }}>
            <Tabs
              items={[
                {
                  key: 'statement',
                  label: 'Ekstre',
                  children: (
                    <Table
                      rowKey={(row, index) => `${row.date}-${row.kind}-${index}`}
                      columns={statementColumns}
                      dataSource={ledger.rows}
                      pagination={{ pageSize: 12, hideOnSinglePage: true }}
                      scroll={{ x: 720 }}
                      locale={{ emptyText: 'Hareket yok' }}
                    />
                  ),
                },
                {
                  key: 'invoices',
                  label: 'Faturalarım',
                  children: (
                    <Table
                      rowKey={(row) => `${row.kind}-${row.id}`}
                      loading={invoiceQuery.isLoading}
                      columns={invoiceColumns}
                      dataSource={invoiceQuery.data ?? []}
                      pagination={{ pageSize: 12, hideOnSinglePage: true }}
                      scroll={{ x: 760 }}
                      locale={{ emptyText: 'Tahakkuk yok' }}
                    />
                  ),
                },
              ]}
            />
          </Card>
        </>
      )}
    </Space>
  )
}
