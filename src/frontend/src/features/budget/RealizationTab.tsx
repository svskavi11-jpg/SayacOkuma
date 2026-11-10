import { useQuery } from '@tanstack/react-query'
import { Button, Space, Statistic, Table, Tag } from 'antd'
import { FileExcelOutlined, FilePdfOutlined } from '@ant-design/icons'
import type { ColumnsType } from 'antd/es/table'
import { apiClient } from '../../api/client'
import { useAuth } from '../auth/AuthContext'
import { downloadFile } from '../../api/download'
import type { BudgetRealizationReport, BudgetRealizationRow } from '../../types/api'

const tl = (v: number) => v.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })
const pct = (v: number) =>
  `${v.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}%`

/** Faz 6.10: Tahmini, gerçekleşen, kalan ve yüzde. */
export function RealizationTab({ year }: { year: number }) {
  const { hasPermission } = useAuth()
  const canDownload = hasPermission('Budget.View')
  const query = useQuery({
    queryKey: ['budget-realization', year],
    queryFn: async () =>
      (await apiClient.get<BudgetRealizationReport>('/budget/realization', { params: { year } })).data,
  })

  const columns: ColumnsType<BudgetRealizationRow> = [
    { title: 'Kategori', dataIndex: 'category' },
    { title: 'Tahmini', dataIndex: 'estimatedAmount', align: 'right', render: (v: number) => tl(v) },
    {
      title: 'Virman',
      align: 'right',
      render: (_: unknown, row: BudgetRealizationRow) => {
        const net = row.transferIn - row.transferOut
        return <span style={{ color: net < 0 ? '#cf1322' : undefined }}>{tl(net)}</span>
      },
    },
    { title: 'Gerçekleşen', dataIndex: 'actualAmount', align: 'right', render: (v: number) => tl(v) },
    {
      title: 'Kalan',
      dataIndex: 'remainingAmount',
      align: 'right',
      render: (v: number) => <span style={{ color: v < 0 ? '#cf1322' : undefined }}>{tl(v)}</span>,
    },
    {
      title: '%',
      dataIndex: 'utilizationPercent',
      width: 110,
      align: 'right',
      render: (v: number) => <Tag color={v >= 100 ? 'red' : v >= 90 ? 'gold' : 'green'}>{pct(v)}</Tag>,
    },
  ]

  const data = query.data

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Space wrap>
        <Statistic title="Tahmini" value={data?.totalEstimated ?? 0} precision={2} groupSeparator="." decimalSeparator="," suffix="TL" />
        <Statistic title="Gerçekleşen" value={data?.totalActual ?? 0} precision={2} groupSeparator="." decimalSeparator="," suffix="TL" />
        <Statistic title="Kalan" value={data?.totalRemaining ?? 0} precision={2} groupSeparator="." decimalSeparator="," suffix="TL" />
        <Statistic title="Oran" value={data?.utilizationPercent ?? 0} precision={2} groupSeparator="." decimalSeparator="," suffix="%" />
        <Statistic title="Tahmini Gelir" value={data?.totalEstimatedIncome ?? 0} precision={2} groupSeparator="." decimalSeparator="," suffix="TL" />
        <Statistic title="Denge" value={data?.balance ?? 0} precision={2} groupSeparator="." decimalSeparator="," suffix="TL" />
      </Space>
      {canDownload && <Space wrap>
        <Button
          type="primary"
          icon={<FilePdfOutlined />}
          onClick={() => void downloadFile(`/budget/reports/realization.pdf?year=${year}`, `ButceGerceklesme-${year}.pdf`)}
        >
          PDF
        </Button>
        <Button
          icon={<FileExcelOutlined />}
          style={{ color: '#217346' }}
          onClick={() => void downloadFile(`/budget/reports/realization.xlsx?year=${year}`, `ButceGerceklesme-${year}.xlsx`)}
        >
          Excel
        </Button>
      </Space>}
      <Table
        scroll={{ x: 'max-content' }}
        rowKey="budgetItemId"
        size="small"
        loading={query.isLoading}
        dataSource={data?.rows ?? []}
        columns={columns}
        pagination={false}
        locale={{ emptyText: `${year} yılı için bütçe kalemi yok.` }}
      />
    </Space>
  )
}
