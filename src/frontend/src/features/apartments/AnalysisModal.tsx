import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Col, DatePicker, Empty, Modal, Row, Space, Spin, Statistic, Table, Tag } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import dayjs, { type Dayjs } from 'dayjs'
import { Line } from '@ant-design/plots'
import { apiClient } from '../../api/client'
import type {
  AnalysisReadingItem,
  ApartmentConsumptionAnalysis,
  ApartmentListItem,
  MonthlyConsumptionItem,
} from '../../types/api'

const monthColumns: ColumnsType<MonthlyConsumptionItem> = [
  { title: 'Ay', dataIndex: 'monthName', width: 90 },
  { title: 'Yıl', dataIndex: 'year', width: 70 },
  {
    title: 'Tüketim (kWh)',
    dataIndex: 'consumptionKWh',
    align: 'right',
    render: (v: number) => v.toFixed(3),
  },
  { title: 'Okuma', dataIndex: 'readingCount', width: 80, align: 'right' },
]

const readingColumns: ColumnsType<AnalysisReadingItem> = [
  {
    title: 'Okuma Tarihi',
    dataIndex: 'readingDateUtc',
    width: 150,
    render: (v: string) => dayjs(v).format('DD.MM.YYYY HH:mm'),
  },
  {
    title: 'Endeks (kWh)',
    dataIndex: 'indexValue',
    align: 'right',
    render: (v: number) => v.toFixed(3),
  },
  {
    title: 'Tüketim (kWh)',
    dataIndex: 'consumptionKWh',
    align: 'right',
    render: (v: number) => v.toFixed(3),
  },
  { title: 'Kaynak', dataIndex: 'source', width: 110 },
  {
    title: 'Tip',
    dataIndex: 'isInitialReading',
    width: 110,
    render: (v: boolean) => (v ? <Tag color="blue">Başlangıç</Tag> : <Tag>Dönem</Tag>),
  },
]

/**
 * Etap 6.7: Daire tüketim analizi — yıl VEYA tarih aralığı filtreli; aylık trend grafiği
 * (@ant-design/plots Line), özet kartları ve okuma detay tablosu.
 */
export function AnalysisModal({
  apartment,
  onClose,
}: {
  apartment: ApartmentListItem | null
  onClose: () => void
}) {
  const currentYear = dayjs().year()
  const [year, setYear] = useState<number | null>(currentYear)
  const [range, setRange] = useState<[Dayjs | null, Dayjs | null] | null>(null)

  const yearOptions = Array.from({ length: 6 }, (_, i) => currentYear - i)

  const buildUrl = () => {
    if (!apartment) return null
    const base = `/apartments/${apartment.id}/consumption-analysis`
    if (range && range[0] && range[1]) {
      return `${base}?from=${encodeURIComponent(range[0].toISOString())}&to=${encodeURIComponent(range[1].toISOString())}`
    }
    return `${base}?year=${year ?? currentYear}`
  }

  const url = buildUrl()

  const analysisQuery = useQuery({
    queryKey: ['consumption-analysis', apartment?.id, url],
    queryFn: async () => (await apiClient.get<ApartmentConsumptionAnalysis>(url!)).data,
    enabled: url !== null,
  })

  const isRangeMode = Boolean(range && range[0] && range[1])

  const chartData =
    analysisQuery.data?.monthly.map((m) => ({
      label: m.monthName,
      value: m.consumptionKWh,
    })) ?? []
  return (
    <Modal
      title={`Tüketim Analizi — Daire ${apartment?.doorNumber ?? ''}${apartment?.ownerName ? ` (${apartment.ownerName})` : ''}`}
      open={apartment !== null}
      onCancel={onClose}
      footer={null}
      width={860}
    >
      <Space direction="vertical" size={12} style={{ width: '100%' }}>
        <Space wrap>
          <span>Filtre:</span>
          <select
            value={isRangeMode ? '' : String(year ?? currentYear)}
            onChange={(e) => {
              setYear(Number(e.target.value))
              setRange(null)
            }}
            style={{ padding: '4px 8px', borderRadius: 6 }}
          >
            {isRangeMode && <option value="">Tarih Aralığı</option>}
            {yearOptions.map((y) => (
              <option key={y} value={y}>
                {y}
              </option>
            ))}
          </select>
          <DatePicker.RangePicker
            value={range && range[0] && range[1] ? [range[0], range[1]] : null}
            onChange={(dates) => {
              const d = dates as [Dayjs | null, Dayjs | null] | null
              setRange(d ? [d[0] ?? null, d[1] ?? null] : null)
            }}
            format="DD.MM.YYYY"
            allowClear
            placeholder={['Başlangıç', 'Bitiş']}
          />
        </Space>



        {analysisQuery.isLoading && (
          <div style={{ textAlign: 'center', padding: 40 }}>
            <Spin tip="Analiz yükleniyor..." />
          </div>
        )}


        {analysisQuery.data && (
          <>
            <Row gutter={16}>
              <Col span={8}>
                <Statistic
                  title="Toplam Tüketim"
                  value={analysisQuery.data.totalConsumptionKWh}
                  precision={3}
                  suffix="kWh"
                />
              </Col>
              <Col span={8}>
                <Statistic
                  title="Aylık Ortalama"
                  value={analysisQuery.data.averageMonthlyConsumptionKWh}
                  precision={3}
                  suffix="kWh/ay"
                />
              </Col>
              <Col span={8}>
                <Statistic title="Okuma Sayısı" value={analysisQuery.data.readingCount} />
              </Col>
            </Row>

            {chartData.length > 0 ? (
              <Line data={chartData} xField="label" yField="value" height={260} smooth />
            ) : (
              <Empty description="Seçili dönemde okuma verisi yok." />
            )}

            <Table
              rowKey={(r) => `${r.year}-${r.month}`}
              columns={monthColumns}
              dataSource={analysisQuery.data.monthly}
              size="small"
              pagination={false}
              scroll={{ y: 220 }}
            />

            <Table
              rowKey="readingId"
              columns={readingColumns}
              dataSource={analysisQuery.data.readings}
              size="small"
              pagination={{ pageSize: 5 }}
            />
          </>
        )}
      </Space>
    </Modal>
  )
}
