import { useQuery } from '@tanstack/react-query'
import { Button, Card, Select, Space, Table, Tag, Typography } from 'antd'
import { DownloadOutlined, FileExcelOutlined, FilePdfOutlined } from '@ant-design/icons'
import { useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { apiClient } from '../../api/client'
import { downloadFile } from '../../api/download'
import type {
  CalculationResultResponse,
  GasInvoiceListItem,
} from '../../types/api'

const tl = (v: number) => v.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })

/**
 * Raporlar sayfası: hesaplanmış fatura seç → toplu PDF (72 sayfa), tek daire PDF, Excel indirme.
 * Tüm indirmeler blob olarak tarayıcıya gider — sunucuda dosya tutulmaz (Kural #2).
 */
export function ReportsPage() {
  const [searchParams] = useSearchParams()
  const [selectedInvoiceId, setSelectedInvoiceId] = useState<string | null>(
    searchParams.get('invoice'),
  )

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

  return (
    <Card title="Raporlar">
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
            <Button
              type="primary"
              icon={<FilePdfOutlined />}
              onClick={() =>
                void downloadFile(
                  `/gas-invoices/${selectedInvoice.id}/reports/notice.pdf`,
                  'BireyselGiderBildirimi-72-sayfa.pdf',
                )
              }
            >
              Toplu PDF İndir (72 Sayfa)
            </Button>
            <Button
              icon={<FileExcelOutlined />}
              style={{ color: '#217346' }}
              onClick={() =>
                void downloadFile(
                  `/gas-invoices/${selectedInvoice.id}/reports/borclar.xlsx`,
                  'Borclistesii.xlsx',
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
            <Table
              rowKey="apartmentInvoiceId"
              size="small"
              dataSource={resultQuery.data.rows}
              pagination={{ pageSize: 12 }}
              columns={[
                { title: 'Kapı', dataIndex: 'doorNumber', width: 80 },
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
                          `/gas-invoices/${selectedInvoiceId}/reports/notice.pdf?apartmentId=${r.apartmentId}`,
                          `BireyselGiderBildirimi-Daire${r.doorNumber}.pdf`,
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
      </Space>
    </Card>
  )
}
