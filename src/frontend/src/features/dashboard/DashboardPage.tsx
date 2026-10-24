import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Card,
  Col,
  Modal,
  Row,
  Statistic,
  Typography,
  Button,
  Space,
  Tag,
} from 'antd'
import {
  FilePdfOutlined,
  TableOutlined,
  ThunderboltOutlined,
} from '@ant-design/icons'
import { useNavigate } from 'react-router-dom'
import { apiClient } from '../../api/client'
import { downloadFile } from '../../api/download'
import type { DashboardSummary, MeterReadingCycleResult } from '../../types/api'

/** S3: kayıp-kaçak oranı renk eşiği (yeşil < %10, sarı < %25, kırmızı ≥ %25). */
function lossColor(percent: number): string {
  if (percent < 10) return '#3f8600'
  if (percent < 25) return '#d4b106'
  return '#cf1322'
}

export function DashboardPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  // M-Bus okuma döngüsü: gerçek donanımda 72 sayaç x ~2-3 sn sürebilir -> bu cagriya ozel 10 dk timeout.
  const readingMutation = useMutation({
    mutationFn: async () =>
      (
        await apiClient.post<MeterReadingCycleResult>('/meter-readings/trigger', null, {
          timeout: 600_000,
        })
      ).data,
    onSuccess: (r) => {
      void queryClient.invalidateQueries({ queryKey: ['dashboard-summary'] })
      Modal.success({
        title: `Okuma Tamamlandı: ${r.readSuccessCount} sayaç okundu, ${r.readFailureCount} sayaç yanıt vermedi`,
        content: `Bağlantı: ${r.connectionType} · Kaydedilen okuma: ${r.insertedCount} · Duplicate: ${r.skippedDuplicateCount} · Eşleşmeyen seri no: ${r.unmatchedSerialCount}`,
        okText: 'Tamam',
      })
    },
  })

  const { data, isLoading } = useQuery({
    queryKey: ['dashboard-summary'],
    queryFn: async () => (await apiClient.get<DashboardSummary>('/dashboard/summary')).data,
  })

  if (isLoading || !data) return <Card loading style={{ maxWidth: 900 }} />

  const hasInvoice = data.lastInvoiceId !== null
  const lossPercent = data.lastLossPercent ?? 0

  return (
    <div>
      <Typography.Title level={4}>Panel</Typography.Title>
      <Row gutter={[16, 16]}>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic title="Bina" value={data.buildingName || '-'} />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic title="Daire Sayısı" value={data.apartmentCount} />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic title="Kayıtlı Sayaç" value={data.activeMeterCount} />
          </Card>
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <Card>
            <Statistic
              title="Kayıp-Kaçak Oranı (S3)"
              value={lossPercent}
              suffix="%"
              precision={2}
              valueStyle={{ color: lossColor(lossPercent) }}
            />
            <Typography.Text type="secondary" style={{ fontSize: 11 }}>
              Ana sayaç kWh − Σ daire kWh
            </Typography.Text>
          </Card>
        </Col>
      </Row>

      <Card title="M-Bus Okuma" style={{ marginTop: 16, maxWidth: 900 }}>
        <Space direction="vertical" size={8} style={{ width: '100%' }}>
          <Typography.Text type="secondary">
            Izar Center'a bağlanıp tüm sayaçları okur (bağlantı tipi Ayarlar → M-Bus Donanım'dan seçilir).
          </Typography.Text>
          <Button
            type="primary"
            size="large"
            icon={<ThunderboltOutlined />}
            loading={readingMutation.isPending}
            onClick={() => readingMutation.mutate()}
          >
            Sayaçları Şimdi Oku
          </Button>
        </Space>
      </Card>

      <Card
        title="Son Fatura"
        style={{ marginTop: 16, maxWidth: 900 }}
        extra={
          hasInvoice && data.lastInvoiceId && (
            <Space>
              <Button
                size="small"
                icon={<FilePdfOutlined />}
                onClick={() =>
                  void downloadFile(
                    `/gas-invoices/${data.lastInvoiceId}/reports/notice.pdf`,
                    'BireyselGiderBildirimi-72-sayfa.pdf',
                  )
                }
              >
                Raporu İndir (PDF)
              </Button>
              <Button
                size="small"
                icon={<TableOutlined />}
                onClick={() => navigate(`/reports?invoice=${data.lastInvoiceId}`)}
              >
                Hesaplamayı Gör
              </Button>
            </Space>
          )
        }
      >
        {hasInvoice ? (
          <Space size={32} wrap>
            <Statistic title="Fatura No" value={data.lastInvoiceNumber ?? '-'} />
            <Statistic
              title="Dönem"
              value={
                data.lastInvoicePeriodFrom
                  ? `${new Date(data.lastInvoicePeriodFrom).toLocaleDateString('tr-TR')} - ${new Date(data.lastInvoicePeriodTo ?? 0).toLocaleDateString('tr-TR')}`
                  : '-'
              }
              valueStyle={{ fontSize: 16 }}
            />
            <Statistic
              title="Toplam Tutar"
              value={data.lastInvoiceTotalAmount ?? 0}
              precision={2}
              suffix="TL"
            />
            <div>
              <Typography.Text type="secondary">Mutabakat</Typography.Text>
              <div>
                {data.lastInvoiceReconciled === true && <Tag color="green">KURUŞU KURUŞUNA ✔</Tag>}
                {data.lastInvoiceReconciled === false && <Tag color="red">MUTABAKATSIZ</Tag>}
                {data.lastInvoiceReconciled === null && <Tag>Hesaplanmadı</Tag>}
              </div>
            </div>
            <div>
              <Typography.Text type="secondary">Okunamayan Daire</Typography.Text>
              <div>
                {data.lastCalculationFailedMeterCount > 0 ? (
                  <Tag color="orange">{data.lastCalculationFailedMeterCount} daire</Tag>
                ) : (
                  <Tag color="green">Tümü okundu</Tag>
                )}
              </div>
            </div>
          </Space>
        ) : (
          <Typography.Text type="secondary">
            Henüz fatura girilmedi. "Faturalar" sayfasından ilk faturayı girebilirsiniz.
          </Typography.Text>
        )}
      </Card>
    </div>
  )
}
