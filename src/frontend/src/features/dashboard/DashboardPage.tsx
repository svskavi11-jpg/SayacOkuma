import type { CSSProperties, ReactNode } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Avatar,
  Button,
  Card,
  Col,
  Empty,
  List,
  Modal,
  Progress,
  Row,
  Space,
  Statistic,
  Tag,
  Typography,
} from 'antd'
import {
  AccountBookOutlined,
  AlertOutlined,
  CheckCircleOutlined,
  DashboardOutlined,
  FallOutlined,
  FilePdfOutlined,
  FileTextOutlined,
  FireOutlined,
  HomeOutlined,
  PieChartOutlined,
  RiseOutlined,
  TableOutlined,
  ThunderboltOutlined,
  WarningOutlined,
} from '@ant-design/icons'
import { Pie } from '@ant-design/plots'
import { useNavigate } from 'react-router-dom'
import dayjs from 'dayjs'
import { apiClient } from '../../api/client'
import { downloadFile } from '../../api/download'
import type {
  ApartmentListItem,
  BankDashboard,
  DashboardSummary,
  MeterReadingCycleResult,
} from '../../types/api'
import { BankPanel } from './BankPanel'

const tl = (v: number) => v.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })

const cardBaseStyle: CSSProperties = {
  borderRadius: 12,
  boxShadow: '0 4px 16px rgba(15, 23, 42, 0.06)',
  border: 'none',
  height: '100%',
}

function lossColor(percent: number): string {
  if (percent < 10) return '#389e0d'
  if (percent < 25) return '#d48806'
  return '#cf1322'
}

function KpiCard({
  label,
  value,
  hint,
  color,
  bgTint,
  icon,
}: {
  label: string
  value: string
  hint?: string
  color: string
  bgTint: string
  icon: ReactNode
}) {
  return (
    <Card
      variant="borderless"
      styles={{ body: { padding: '20px 22px' } }}
      style={{
        ...cardBaseStyle,
        borderLeft: `5px solid ${color}`,
      }}
    >
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: 16 }}>
        <div style={{ minWidth: 0, flex: 1 }}>
          <Typography.Text
            type="secondary"
            style={{
              fontSize: 13,
              fontWeight: 600,
              textTransform: 'uppercase',
              letterSpacing: 0.4,
              display: 'block',
              marginBottom: 6,
            }}
          >
            {label}
          </Typography.Text>
          <div
            style={{
              fontSize: 26,
              fontWeight: 700,
              color,
              lineHeight: 1.2,
              marginBottom: 6,
              whiteSpace: 'nowrap',
              overflow: 'hidden',
              textOverflow: 'ellipsis',
            }}
          >
            {value}
          </div>
          {hint && (
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              {hint}
            </Typography.Text>
          )}
        </div>
        <div
          style={{
            width: 52,
            height: 52,
            borderRadius: 14,
            background: bgTint,
            color,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontSize: 24,
            flexShrink: 0,
          }}
        >
          {icon}
        </div>
      </div>
    </Card>
  )
}

export function DashboardPage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()

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

  const summaryQuery = useQuery({
    queryKey: ['dashboard-summary'],
    queryFn: async () => (await apiClient.get<DashboardSummary>('/dashboard/summary')).data,
  })

  const apartmentsQuery = useQuery({
    queryKey: ['apartments'],
    queryFn: async () => (await apiClient.get<ApartmentListItem[]>('/apartments')).data,
  })

  const banksQuery = useQuery({
    queryKey: ['dashboard-banks'],
    queryFn: async () => (await apiClient.get<BankDashboard>('/dashboard/banks')).data,
  })

  const data = summaryQuery.data
  const apartments = apartmentsQuery.data ?? []
  const bankData = banksQuery.data

  if (summaryQuery.isLoading || !data) {
    return (
      <div style={{ background: '#f0f2f5', padding: 24, minHeight: 'calc(100vh - 64px)', margin: -16 }}>
        <Card loading variant="borderless" style={cardBaseStyle} />
      </div>
    )
  }

  const totalDebt = apartments.reduce((s, a) => s + Math.max(0, a.currentBalance), 0)
  const totalCredit = apartments.reduce((s, a) => s + Math.abs(Math.min(0, a.currentBalance)), 0)
  const aidatDebt = apartments.reduce((s, a) => s + (a.aidatDebt ?? 0), 0)
  const heatingDebt = apartments.reduce((s, a) => s + (a.heatingDebt ?? 0), 0)
  const debtorCount = apartments.filter((a) => a.currentBalance > 0).length
  const creditorCount = apartments.filter((a) => a.currentBalance < 0).length
  const settledCount = apartments.filter((a) => a.currentBalance === 0).length
  const lossPercent = data.lastLossPercent ?? 0
  const hasInvoice = data.lastInvoiceId !== null

  const totalMoneyIn = bankData?.totalMoneyIn ?? 0
  const totalMoneyOut = bankData?.totalMoneyOut ?? 0

  const topDebtors = [...apartments]
    .filter((a) => a.currentBalance > 0)
    .sort((a, b) => b.currentBalance - a.currentBalance)
    .slice(0, 5)

  const mix = [
    { type: 'Aidat Borcu', value: aidatDebt },
    { type: 'Isınma Borcu', value: heatingDebt },
  ].filter((d) => d.value > 0)

  const period = data.lastInvoicePeriodFrom
    ? `${dayjs(data.lastInvoicePeriodFrom).format('DD.MM.YYYY')} – ${dayjs(data.lastInvoicePeriodTo).format('DD.MM.YYYY')}`
    : '—'

  return (
    <div
      style={{
        background: '#f0f2f5',
        padding: 24,
        margin: -16,
        minHeight: 'calc(100vh - 64px)',
      }}
    >
      {/* Üst Başlık Alanı */}
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: 12,
          marginBottom: 20,
        }}
      >
        <div>
          <Typography.Title level={3} style={{ margin: 0, fontWeight: 700, color: '#1f1f1f' }}>
            {data.buildingName || 'Westalife Residence'}
          </Typography.Title>
          <Typography.Text type="secondary" style={{ fontSize: 13 }}>
            {dayjs().format('DD MMMM YYYY')} · {data.apartmentCount} Bağımsız Bölüm · {data.activeMeterCount} Aktif Sayaç
          </Typography.Text>
        </div>
        <Space size={10}>
          <Button
            size="middle"
            icon={<HomeOutlined />}
            onClick={() => navigate('/apartments')}
            style={{ borderRadius: 8, fontWeight: 500 }}
          >
            Daire Listesi
          </Button>
        </Space>
      </div>

      {/* 1. Üst KPI Kartları (Yeşil, Kırmızı, Mor, Turuncu) */}
      <Row gutter={[20, 20]}>
        <Col xs={24} sm={12} lg={6}>
          <KpiCard
            label="Toplam Tahsilat"
            value={tl(totalMoneyIn)}
            hint={
              creditorCount > 0
                ? `Banka girişi · ${creditorCount} alacaklı (${tl(totalCredit)})`
                : 'Dönem içi toplam banka girişi'
            }
            color="#389e0d"
            bgTint="#f6ffed"
            icon={<RiseOutlined />}
          />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <KpiCard
            label="Toplam Gider"
            value={tl(totalMoneyOut)}
            hint="Dönem içi toplam banka çıkışı"
            color="#cf1322"
            bgTint="#fff1f0"
            icon={<FallOutlined />}
          />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <KpiCard
            label="Aidat Borcu"
            value={tl(aidatDebt)}
            hint="Kalan anapara + gecikme faizi"
            color="#722ed1"
            bgTint="#f9f0ff"
            icon={<AccountBookOutlined />}
          />
        </Col>
        <Col xs={24} sm={12} lg={6}>
          <KpiCard
            label="Isınma Borcu"
            value={tl(heatingDebt)}
            hint="Kalan anapara + gecikme faizi"
            color="#d46b08"
            bgTint="#fff7e6"
            icon={<FireOutlined />}
          />
        </Col>
      </Row>

      {/* 2. Banka Hesapları (Sol) ve Aylık Nakit Akışı Alan Grafiği (Sağ) */}
      <BankPanel />

      {/* 3. En Borçlu 5 Daire (List) ve Borç Dağılımı (Donut + Merkezde Toplam Borç) */}
      <Row gutter={[20, 20]} style={{ marginTop: 20 }}>
        <Col xs={24} lg={12}>
          <Card
            variant="borderless"
            style={cardBaseStyle}
            styles={{ body: { padding: '12px 24px 20px' } }}
            title={
              <Space size={8}>
                <WarningOutlined style={{ color: '#cf1322', fontSize: 18 }} />
                <span style={{ fontWeight: 600, fontSize: 16 }}>En Borçlu 5 Daire</span>
              </Space>
            }
            extra={
              <Button
                type="link"
                size="small"
                onClick={() => navigate('/payments')}
                style={{ fontWeight: 500 }}
              >
                Cari Hesaplar
              </Button>
            }
          >
            {topDebtors.length > 0 ? (
              <List
                itemLayout="horizontal"
                dataSource={topDebtors}
                renderItem={(a, index) => (
                  <List.Item
                    style={{
                      padding: '14px 4px',
                      borderBottom:
                        index === topDebtors.length - 1 ? 'none' : '1px solid #f0f0f0',
                    }}
                    actions={[
                      <Tag
                        key="debt"
                        color="error"
                        style={{
                          fontSize: 15,
                          fontWeight: 700,
                          padding: '5px 12px',
                          borderRadius: 8,
                          margin: 0,
                        }}
                      >
                        {tl(a.currentBalance)}
                      </Tag>,
                    ]}
                  >
                    <List.Item.Meta
                      avatar={
                        <Avatar
                          style={{
                            backgroundColor: index === 0 ? '#cf1322' : index < 3 ? '#fa541c' : '#ff7875',
                            fontWeight: 700,
                            fontSize: 14,
                          }}
                          size={38}
                        >
                          {index + 1}
                        </Avatar>
                      }
                      title={
                        <Space size={8} wrap>
                          <Typography.Text strong style={{ fontSize: 15 }}>
                            Daire {a.doorNumber}
                          </Typography.Text>
                          {a.ownerName && (
                            <Typography.Text type="secondary" style={{ fontSize: 13 }}>
                              · {a.ownerName}
                            </Typography.Text>
                          )}
                        </Space>
                      }
                      description={
                        <Space size={12} style={{ fontSize: 12 }}>
                          <span style={{ color: '#722ed1' }}>
                            Aidat: {tl(a.aidatDebt ?? 0)}
                          </span>
                          <span style={{ color: '#d46b08' }}>
                            Isınma: {tl(a.heatingDebt ?? 0)}
                          </span>
                        </Space>
                      }
                    />
                  </List.Item>
                )}
              />
            ) : (
              <Empty description="Borçlu daire bulunmuyor." />
            )}
          </Card>
        </Col>

        <Col xs={24} lg={12}>
          <Card
            variant="borderless"
            style={cardBaseStyle}
            styles={{ body: { padding: '16px 24px 20px' } }}
            title={
              <Space size={8}>
                <PieChartOutlined style={{ color: '#722ed1', fontSize: 18 }} />
                <span style={{ fontWeight: 600, fontSize: 16 }}>Borç Dağılımı</span>
              </Space>
            }
          >
            {mix.length > 0 ? (
              <div style={{ position: 'relative', height: 270 }}>
                <Pie
                  data={mix}
                  angleField="value"
                  colorField="type"
                  height={270}
                  radius={0.92}
                  innerRadius={0.68}
                  legend={{ color: { position: 'bottom' } }}
                  scale={{
                    color: {
                      domain: ['Aidat Borcu', 'Isınma Borcu'],
                      range: ['#722ed1', '#d46b08'],
                    },
                  }}
                  tooltip={{
                    items: [{ channel: 'y', valueFormatter: (v: number) => tl(Number(v)) }],
                  }}
                />
                {/* Donut Halkasının Tam Ortasında Toplam Borç Gösterimi */}
                <div
                  style={{
                    position: 'absolute',
                    top: '44%',
                    left: '50%',
                    transform: 'translate(-50%, -50%)',
                    textAlign: 'center',
                    pointerEvents: 'none',
                    width: 170,
                  }}
                >
                  <Typography.Text
                    type="secondary"
                    style={{
                      fontSize: 11,
                      fontWeight: 600,
                      textTransform: 'uppercase',
                      letterSpacing: 0.6,
                      display: 'block',
                    }}
                  >
                    TOPLAM BORÇ
                  </Typography.Text>
                  <div
                    style={{
                      fontSize: 19,
                      fontWeight: 800,
                      color: '#cf1322',
                      lineHeight: 1.25,
                      marginTop: 2,
                    }}
                  >
                    {tl(totalDebt)}
                  </div>
                </div>
              </div>
            ) : (
              <Empty description="Açık borç bulunmuyor." />
            )}

            <Row gutter={[12, 12]} style={{ marginTop: 12 }}>
              <Col span={8}>
                <div
                  style={{
                    background: '#fff1f0',
                    borderRadius: 10,
                    padding: '10px 12px',
                    textAlign: 'center',
                  }}
                >
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    Borçlu Daire
                  </Typography.Text>
                  <div style={{ fontSize: 20, fontWeight: 700, color: '#cf1322' }}>
                    {debtorCount}
                  </div>
                </div>
              </Col>
              <Col span={8}>
                <div
                  style={{
                    background: '#f6ffed',
                    borderRadius: 10,
                    padding: '10px 12px',
                    textAlign: 'center',
                  }}
                >
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    Alacaklı Daire
                  </Typography.Text>
                  <div style={{ fontSize: 20, fontWeight: 700, color: '#389e0d' }}>
                    {creditorCount}
                  </div>
                </div>
              </Col>
              <Col span={8}>
                <div
                  style={{
                    background: '#f5f5f5',
                    borderRadius: 10,
                    padding: '10px 12px',
                    textAlign: 'center',
                  }}
                >
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    Borcu Yok
                  </Typography.Text>
                  <div style={{ fontSize: 20, fontWeight: 700, color: '#595959' }}>
                    {settledCount}
                  </div>
                </div>
              </Col>
            </Row>
          </Card>
        </Col>
      </Row>

      {/* 4. En Alt Bölüm: Kayıp-Kaçak, Son Fatura ve Sağda Belirgin Sayaç Okuma Kartı */}
      <Row gutter={[20, 20]} style={{ marginTop: 20 }}>
        <Col xs={24} md={8}>
          <Card
            variant="borderless"
            style={cardBaseStyle}
            styles={{ body: { padding: 28 } }}
            title={
              <Space size={8}>
                <DashboardOutlined style={{ color: lossColor(lossPercent), fontSize: 18 }} />
                <span style={{ fontWeight: 600, fontSize: 16 }}>Kayıp-Kaçak Oranı</span>
              </Space>
            }
          >
            <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 12 }}>
              <Progress
                type="dashboard"
                size={165}
                strokeWidth={10}
                percent={Math.min(lossPercent, 100)}
                strokeColor={lossColor(lossPercent)}
                format={() => (
                  <span style={{ fontSize: 22, fontWeight: 700, color: lossColor(lossPercent) }}>
                    %{lossPercent.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                  </span>
                )}
              />
              <Tag color={lossPercent < 10 ? 'success' : lossPercent < 25 ? 'warning' : 'error'}>
                {lossPercent < 10
                  ? 'Normal Seviye (< %10)'
                  : lossPercent < 25
                    ? 'Dikkat Edilmeli (%10 - %25)'
                    : 'Yüksek Kayıp (≥ %25)'}
              </Tag>
              <Typography.Text type="secondary" style={{ textAlign: 'center', fontSize: 13 }}>
                Ana sayaç kWh − daire sayaçları toplamı fark oranı
              </Typography.Text>
            </div>
          </Card>
        </Col>

        <Col xs={24} md={9}>
          <Card
            variant="borderless"
            style={cardBaseStyle}
            styles={{ body: { padding: 28 } }}
            title={
              <Space size={8}>
                <FileTextOutlined style={{ color: '#1677ff', fontSize: 18 }} />
                <span style={{ fontWeight: 600, fontSize: 16 }}>Son Doğalgaz Faturası</span>
              </Space>
            }
            extra={
              hasInvoice && data.lastInvoiceId ? (
                <Space size={8}>
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
                    PDF
                  </Button>
                  <Button
                    size="small"
                    icon={<TableOutlined />}
                    onClick={() => navigate(`/reports?invoice=${data.lastInvoiceId}`)}
                  >
                    Detay
                  </Button>
                </Space>
              ) : null
            }
          >
            {hasInvoice ? (
              <Space direction="vertical" size={16} style={{ width: '100%' }}>
                <div
                  style={{
                    background: '#fafafa',
                    padding: '14px 16px',
                    borderRadius: 10,
                    border: '1px solid #f0f0f0',
                  }}
                >
                  <Typography.Text type="secondary" style={{ fontSize: 12, display: 'block' }}>
                    FATURA NUMARASI & DÖNEM
                  </Typography.Text>
                  <Typography.Text strong style={{ fontSize: 16, display: 'block', marginTop: 2 }}>
                    {data.lastInvoiceNumber}
                  </Typography.Text>
                  <Typography.Text type="secondary" style={{ fontSize: 13 }}>
                    {period}
                  </Typography.Text>
                </div>

                <Statistic
                  title="Fatura Toplam Tutarı"
                  value={data.lastInvoiceTotalAmount ?? 0}
                  precision={2}
                  suffix="TL"
                  valueStyle={{ fontSize: 28, fontWeight: 700, color: '#1f1f1f' }}
                />

                <Space wrap size={[8, 8]}>
                  {data.lastInvoiceReconciled === true && (
                    <Tag icon={<CheckCircleOutlined />} color="success" style={{ padding: '4px 10px' }}>
                      Kuruşu Kuruşuna Mutabık
                    </Tag>
                  )}
                  {data.lastInvoiceReconciled === false && (
                    <Tag icon={<AlertOutlined />} color="error" style={{ padding: '4px 10px' }}>
                      Mutabakatsız
                    </Tag>
                  )}
                  {data.lastInvoiceReconciled === null && (
                    <Tag style={{ padding: '4px 10px' }}>Hesaplanmadı</Tag>
                  )}
                  {data.lastCalculationFailedMeterCount > 0 ? (
                    <Tag color="warning" style={{ padding: '4px 10px' }}>
                      {data.lastCalculationFailedMeterCount} daire okunamadı
                    </Tag>
                  ) : (
                    <Tag color="processing" style={{ padding: '4px 10px' }}>
                      Tüm daireler okundu
                    </Tag>
                  )}
                </Space>
              </Space>
            ) : (
              <Empty description="Henüz fatura girilmedi." />
            )}
          </Card>
        </Col>

        <Col xs={24} md={7}>
          <Card
            variant="borderless"
            style={{
              ...cardBaseStyle,
              background: 'linear-gradient(145deg, #ffffff 0%, #f0f7ff 100%)',
              borderTop: '4px solid #1677ff',
            }}
            styles={{
              body: {
                padding: 28,
                display: 'flex',
                flexDirection: 'column',
                justifyContent: 'space-between',
                height: 'calc(100% - 56px)',
              },
            }}
            title={
              <Space size={8}>
                <ThunderboltOutlined style={{ color: '#1677ff', fontSize: 18 }} />
                <span style={{ fontWeight: 600, fontSize: 16 }}>Uzaktan Sayaç Okuma</span>
              </Space>
            }
          >
            <div>
              <div
                style={{
                  width: 56,
                  height: 56,
                  borderRadius: 16,
                  background: '#e6f4ff',
                  color: '#1677ff',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  fontSize: 28,
                  marginBottom: 16,
                }}
              >
                <ThunderboltOutlined />
              </div>
              <Typography.Title level={5} style={{ marginTop: 0, marginBottom: 8 }}>
                Izar Center M-Bus Sorgusu
              </Typography.Title>
              <Typography.Paragraph type="secondary" style={{ fontSize: 13, marginBottom: 24 }}>
                Binadaki aktif <strong>{data.activeMeterCount}</strong> kalorimetre sayacını Izar Center
                üzerinden anlık olarak sorgular ve endeksleri kaydeder.
              </Typography.Paragraph>
            </div>

            <Button
              type="primary"
              block
              size="large"
              icon={<ThunderboltOutlined />}
              loading={readingMutation.isPending}
              onClick={() => readingMutation.mutate()}
              style={{
                height: 54,
                fontSize: 16,
                fontWeight: 600,
                borderRadius: 10,
                boxShadow: '0 6px 16px rgba(22, 119, 255, 0.32)',
              }}
            >
              Sayaçları Şimdi Oku
            </Button>
          </Card>
        </Col>
      </Row>
    </div>
  )
}
