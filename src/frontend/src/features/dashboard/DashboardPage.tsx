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
  Tooltip,
  Typography,
  theme,
} from 'antd'
import {
  AccountBookOutlined,
  AlertOutlined,
  BankOutlined,
  CheckCircleOutlined,
  CreditCardOutlined,
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
import { useAuth } from '../auth/AuthContext'
import { downloadFile } from '../../api/download'
import type {
  ApartmentListItem,
  BankDashboard,
  DashboardSummary,
  MeterReadingCycleResult,
} from '../../types/api'
import { BankPanel } from './BankPanel'

const tl = (v: number) =>
  `${v.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} TL`

const cardBaseStyle: CSSProperties = {
  borderRadius: 16,
  boxShadow: '0 4px 18px rgba(15, 23, 42, 0.06)',
  border: 'none',
  height: '100%',
  display: 'flex',
  flexDirection: 'column',
}

function lossColor(percent: number): string {
  if (percent < 10) return '#389e0d'
  if (percent < 25) return '#d48806'
  return '#cf1322'
}

function KpiCard({
  label,
  amount,
  hint,
  color,
  bgTint,
  icon,
  trend,
}: {
  label: string
  amount: number
  hint?: string
  color: string
  bgTint: string
  icon: ReactNode
  trend?: { value: number; label?: string }
}) {
  return (
    <Card
      variant="borderless"
      styles={{
        body: {
          padding: '22px 20px',
          flex: 1,
          display: 'flex',
          flexDirection: 'column',
          justifyContent: 'center',
        },
      }}
      style={cardBaseStyle}
    >
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          gap: 16,
        }}
      >
        <div style={{ minWidth: 0, flex: 1 }}>
          <Statistic
            title={
              <span
                style={{
                  fontSize: 12,
                  fontWeight: 600,
                  textTransform: 'uppercase',
                  letterSpacing: 0.5,
                  color: '#8c8c8c',
                }}
              >
                {label}
              </span>
            }
            value={amount}
            precision={2}
            groupSeparator="."
            decimalSeparator=","
            suffix="TL"
            valueStyle={{
              fontSize: 22,
              fontWeight: 700,
              color,
              lineHeight: 1.25,
            }}
          />
          {hint && (
            <Typography.Text
              type="secondary"
              style={{ fontSize: 12, display: 'block', marginTop: 6 }}
              ellipsis
            >
              {hint}
            </Typography.Text>
          )}
          {trend !== undefined && (
            <div style={{ marginTop: 8, display: 'flex', alignItems: 'center', gap: 4 }}>
              <span
                style={{
                  fontSize: 12,
                  fontWeight: 600,
                  color: trend.value >= 0 ? '#389e0d' : '#cf1322',
                  background: trend.value >= 0 ? '#f6ffed' : '#fff1f0',
                  borderRadius: 6,
                  padding: '2px 8px',
                }}
              >
                {trend.value >= 0 ? '↑' : '↓'} %{Math.abs(trend.value).toFixed(1)}
              </span>
              <Typography.Text type="secondary" style={{ fontSize: 11 }}>
                {trend.label ?? 'geçen aya göre'}
              </Typography.Text>
            </div>
          )}
        </div>

        <div
          style={{
            width: 48,
            height: 48,
            borderRadius: 16,
            background: bgTint,
            color,
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            fontSize: 26,
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
  const { hasPermission } = useAuth()
  const canSummary = [
    'Reports.GeneralAssembly',
    'Reports.IncomeExpense',
    'Reports.ApartmentTracking',
    'Reports.FuelBreakdown',
    'Reports.BankControl',
    'Reports.GasNotifications',
  ].some((code) => hasPermission(code))
  const canBanks = hasPermission('Bank.View')
  const canApartments = hasPermission('Apartments.View')
  const canTrigger = hasPermission('MeterReadings.TriggerRead')
  const queryClient = useQueryClient()
  const { token } = theme.useToken()

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
    enabled: canSummary,
  })

  const apartmentsQuery = useQuery({
    queryKey: ['apartments'],
    queryFn: async () => (await apiClient.get<ApartmentListItem[]>('/apartments')).data,
    enabled: canApartments,
  })

  const banksQuery = useQuery({
    queryKey: ['dashboard-banks'],
    queryFn: async () => (await apiClient.get<BankDashboard>('/dashboard/banks')).data,
    enabled: canBanks,
  })

  const data = summaryQuery.data
  const apartments = apartmentsQuery.data ?? []
  const bankData = banksQuery.data

  if (!canSummary) {
    return (
      <Card>
        <Empty description="Özet panel için rapor görme yetkisi gerekir." />
        {canTrigger && (
          <Button
            type="primary"
            icon={<ThunderboltOutlined />}
            loading={readingMutation.isPending}
            onClick={() => readingMutation.mutate()}
            style={{ marginTop: 16 }}
          >
            Sayaçları Şimdi Oku
          </Button>
        )}
      </Card>
    )
  }

  if (summaryQuery.isLoading || !data) {
    return (
      <div
        style={{
          background: token.colorBgLayout ?? '#f0f2f5',
          padding: 24,
          minHeight: 'calc(100vh - 64px)',
          margin: -16,
        }}
      >
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
        background: token.colorBgLayout ?? '#f0f2f5',
        padding: 24,
        margin: -16,
        minHeight: 'calc(100vh - 64px)',
      }}
    >
      {/* ── Gradient Hero Banner ── */}
      <div
        style={{
          background: `linear-gradient(135deg, ${token.colorPrimary} 0%, ${token.colorPrimaryActive} 100%)`,
          borderRadius: 20,
          padding: '24px 28px',
          marginBottom: 24,
          boxShadow: `0 8px 32px ${token.colorPrimary}38`,
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: 16,
        }}
      >
        {/* Sol: Başlık */}
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12, marginBottom: 6 }}>
            <div
              style={{
                width: 42,
                height: 42,
                borderRadius: 12,
                background: 'rgba(255,255,255,0.18)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                fontSize: 22,
                color: '#fff',
              }}
            >
              <HomeOutlined />
            </div>
            <Typography.Title
              level={3}
              style={{ margin: 0, fontWeight: 800, color: '#fff', letterSpacing: -0.3 }}
            >
              {data.buildingName || 'Westalife Residence'}
            </Typography.Title>
          </div>
          <div style={{ display: 'flex', gap: 16, flexWrap: 'wrap' }}>
            <Tag
              style={{
                background: 'rgba(255,255,255,0.18)',
                border: 'none',
                color: '#fff',
                borderRadius: 6,
                fontWeight: 500,
              }}
            >
              📅 {dayjs().format('DD MMMM YYYY')}
            </Tag>
            <Tag
              style={{
                background: 'rgba(255,255,255,0.18)',
                border: 'none',
                color: '#fff',
                borderRadius: 6,
                fontWeight: 500,
              }}
            >
              🏢 {data.apartmentCount} Bağımsız Bölüm
            </Tag>
            <Tag
              style={{
                background: 'rgba(255,255,255,0.18)',
                border: 'none',
                color: '#fff',
                borderRadius: 6,
                fontWeight: 500,
              }}
            >
              ⚡ {data.activeMeterCount} Aktif Sayaç
            </Tag>
          </div>
        </div>

        {/* Sağ: Quick Actions */}
        <Space size={8} wrap>
          <Tooltip title="Daire Listesi">
            <Button
              icon={<HomeOutlined />}
              onClick={() => navigate('/apartments')}
              style={{
                borderRadius: 10,
                fontWeight: 600,
                background: 'rgba(255,255,255,0.15)',
                border: '1px solid rgba(255,255,255,0.3)',
                color: '#fff',
                backdropFilter: 'blur(4px)',
              }}
            >
              Daireler
            </Button>
          </Tooltip>
          <Tooltip title="Banka Hesapları">
            <Button
              icon={<BankOutlined />}
              onClick={() => navigate('/bank-accounts')}
              style={{
                borderRadius: 10,
                fontWeight: 600,
                background: 'rgba(255,255,255,0.15)',
                border: '1px solid rgba(255,255,255,0.3)',
                color: '#fff',
                backdropFilter: 'blur(4px)',
              }}
            >
              Bankalar
            </Button>
          </Tooltip>
          <Tooltip title="Ödemeler">
            <Button
              icon={<CreditCardOutlined />}
              onClick={() => navigate('/payments')}
              style={{
                borderRadius: 10,
                fontWeight: 600,
                background: 'rgba(255,255,255,0.15)',
                border: '1px solid rgba(255,255,255,0.3)',
                color: '#fff',
                backdropFilter: 'blur(4px)',
              }}
            >
              Ödemeler
            </Button>
          </Tooltip>
          <Tooltip title="Raporlar">
            <Button
              icon={<PieChartOutlined />}
              onClick={() => navigate('/reports')}
              style={{
                borderRadius: 10,
                fontWeight: 600,
                background: 'rgba(255,255,255,0.15)',
                border: '1px solid rgba(255,255,255,0.3)',
                color: '#fff',
                backdropFilter: 'blur(4px)',
              }}
            >
              Raporlar
            </Button>
          </Tooltip>
          {canTrigger && (
            <Button
              type="primary"
              icon={<ThunderboltOutlined />}
              loading={readingMutation.isPending}
              onClick={() => readingMutation.mutate()}
              style={{
                borderRadius: 10,
                fontWeight: 700,
                background: '#fff',
                color: token.colorPrimary,
                border: 'none',
                boxShadow: '0 4px 14px rgba(0,0,0,0.15)',
              }}
            >
              Sayaç Oku
            </Button>
          )}
        </Space>
      </div>

      {/* 1. Üst KPI Kartları */}
      <Row gutter={[20, 20]}>
        <Col xs={24} sm={12} md={6} style={{ display: 'flex' }}>
          <div style={{ width: '100%' }}>
            <KpiCard
              label="Toplam Tahsilat"
              amount={totalMoneyIn}
              hint={
                creditorCount > 0
                  ? `Banka girişi · ${creditorCount} alacaklı (${tl(totalCredit)})`
                  : 'Dönem içi toplam banka girişi'
              }
              color="#389e0d"
              bgTint="#f6ffed"
              icon={<RiseOutlined />}
            />
          </div>
        </Col>
        <Col xs={24} sm={12} md={6} style={{ display: 'flex' }}>
          <div style={{ width: '100%' }}>
            <KpiCard
              label="Toplam Gider"
              amount={totalMoneyOut}
              hint="Dönem içi toplam banka çıkışı"
              color="#cf1322"
              bgTint="#fff1f0"
              icon={<FallOutlined />}
            />
          </div>
        </Col>
        <Col xs={24} sm={12} md={6} style={{ display: 'flex' }}>
          <div style={{ width: '100%' }}>
            <KpiCard
              label="Aidat Borcu"
              amount={aidatDebt}
              hint={`${debtorCount} borçlu daire`}
              color="#722ed1"
              bgTint="#f9f0ff"
              icon={<AccountBookOutlined />}
              trend={debtorCount > 0 ? { value: debtorCount, label: 'borçlu daire sayısı' } : undefined}
            />
          </div>
        </Col>
        <Col xs={24} sm={12} md={6} style={{ display: 'flex' }}>
          <div style={{ width: '100%' }}>
            <KpiCard
              label="Isınma Borcu"
              amount={heatingDebt}
              hint="Kalan anapara + gecikme faizi"
              color="#d46b08"
              bgTint="#fff7e6"
              icon={<FireOutlined />}
            />
          </div>
        </Col>
      </Row>

      {/* 2. Banka Hesapları (Sol) ve Aylık Nakit Akışı Alan Grafiği (Sağ) */}
      <BankPanel />

      {/* 3. En Borçlu 5 Daire (List) ve Borç Dağılımı (Büyük Donut + Merkezde Toplam Borç) */}
      <Row gutter={[20, 20]} style={{ marginTop: 20 }}>
        <Col xs={24} lg={12} style={{ display: 'flex' }}>
          <Card
            variant="borderless"
            style={{ ...cardBaseStyle, width: '100%' }}
            styles={{
              header: { padding: '16px 24px', borderBottom: '1px solid #f0f2f5' },
              body: { padding: '16px 24px 24px', flex: 1 },
            }}
            title={
              <Space size={10}>
                <div
                  style={{
                    width: 34,
                    height: 34,
                    borderRadius: 10,
                    background: '#fff1f0',
                    color: '#cf1322',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    fontSize: 17,
                  }}
                >
                  <WarningOutlined />
                </div>
                <span style={{ fontWeight: 600, fontSize: 16, color: '#1f1f1f' }}>
                  En Borçlu 5 Daire
                </span>
              </Space>
            }
            extra={
              <Button
                type="link"
                size="small"
                onClick={() => navigate('/payments')}
                style={{ fontWeight: 500, padding: 0 }}
              >
                Cari Hesaplar →
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
                      padding: '16px 8px',
                      borderBottom:
                        index === topDebtors.length - 1 ? 'none' : '1px solid #f0f2f5',
                    }}
                    actions={[
                      <Tag
                        key="debt"
                        color="error"
                        style={{
                          fontSize: 15,
                          fontWeight: 700,
                          padding: '6px 14px',
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
                            backgroundColor:
                              index === 0 ? '#cf1322' : index < 3 ? '#fa541c' : '#ff7875',
                            fontWeight: 700,
                            fontSize: 15,
                          }}
                          size={42}
                        >
                          {index + 1}
                        </Avatar>
                      }
                      title={
                        <Space size={8} wrap>
                          <Typography.Text strong style={{ fontSize: 15, color: '#1f1f1f' }}>
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
                        <div>
                          <Space size={12} style={{ fontSize: 12, marginTop: 2 }} wrap>
                            <span style={{ color: '#722ed1', fontWeight: 500 }}>
                              Aidat: {tl(a.aidatDebt ?? 0)}
                            </span>
                            <span style={{ color: '#d46b08', fontWeight: 500 }}>
                              Isınma: {tl(a.heatingDebt ?? 0)}
                            </span>
                          </Space>
                          <Progress
                            percent={Math.round(
                              (a.currentBalance / topDebtors[0].currentBalance) * 100,
                            )}
                            showInfo={false}
                            strokeColor={
                              index === 0
                                ? '#cf1322'
                                : index < 3
                                  ? '#fa541c'
                                  : '#ff7875'
                            }
                            trailColor="#f5f5f5"
                            size={{ width: undefined, height: 4 }}
                            style={{ marginTop: 6, marginBottom: 0 }}
                          />
                        </div>
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

        <Col xs={24} lg={12} style={{ display: 'flex' }}>
          <Card
            variant="borderless"
            style={{ ...cardBaseStyle, width: '100%' }}
            styles={{
              header: { padding: '16px 24px', borderBottom: '1px solid #f0f2f5' },
              body: {
                padding: '20px 24px 24px',
                flex: 1,
                display: 'flex',
                flexDirection: 'column',
                justifyContent: 'space-between',
              },
            }}
            title={
              <Space size={10}>
                <div
                  style={{
                    width: 34,
                    height: 34,
                    borderRadius: 10,
                    background: '#f9f0ff',
                    color: '#722ed1',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    fontSize: 17,
                  }}
                >
                  <PieChartOutlined />
                </div>
                <span style={{ fontWeight: 600, fontSize: 16, color: '#1f1f1f' }}>
                  Borç Dağılımı
                </span>
              </Space>
            }
          >
            {mix.length > 0 ? (
              <div>
                <div style={{ position: 'relative', height: 290 }}>
                  <Pie
                    data={mix}
                    angleField="value"
                    colorField="type"
                    height={290}
                    radius={0.94}
                    innerRadius={0.7}
                    legend={false}
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
                      top: '50%',
                      left: '50%',
                      transform: 'translate(-50%, -50%)',
                      textAlign: 'center',
                      pointerEvents: 'none',
                      width: 185,
                    }}
                  >
                    <Typography.Text
                      type="secondary"
                      style={{
                        fontSize: 12,
                        fontWeight: 600,
                        textTransform: 'uppercase',
                        letterSpacing: 0.7,
                        display: 'block',
                      }}
                    >
                      Toplam Borç
                    </Typography.Text>
                    <div
                      style={{
                        fontSize: 21,
                        fontWeight: 800,
                        color: '#cf1322',
                        lineHeight: 1.25,
                        marginTop: 4,
                      }}
                    >
                      {tl(totalDebt)}
                    </div>
                  </div>
                </div>

                {/* Özel Renk Açıklaması (Legend) */}
                <div
                  style={{
                    display: 'flex',
                    justifyContent: 'center',
                    gap: 28,
                    marginTop: 8,
                    marginBottom: 16,
                    flexWrap: 'wrap',
                  }}
                >
                  <Space size={8}>
                    <span
                      style={{
                        display: 'inline-block',
                        width: 10,
                        height: 10,
                        borderRadius: '50%',
                        background: '#722ed1',
                      }}
                    />
                    <Typography.Text type="secondary" style={{ fontSize: 13 }}>
                      Aidat Borcu:
                    </Typography.Text>
                    <Typography.Text strong style={{ fontSize: 13, color: '#722ed1' }}>
                      {tl(aidatDebt)}
                    </Typography.Text>
                  </Space>
                  <Space size={8}>
                    <span
                      style={{
                        display: 'inline-block',
                        width: 10,
                        height: 10,
                        borderRadius: '50%',
                        background: '#d46b08',
                      }}
                    />
                    <Typography.Text type="secondary" style={{ fontSize: 13 }}>
                      Isınma Borcu:
                    </Typography.Text>
                    <Typography.Text strong style={{ fontSize: 13, color: '#d46b08' }}>
                      {tl(heatingDebt)}
                    </Typography.Text>
                  </Space>
                </div>
              </div>
            ) : (
              <Empty description="Açık borç bulunmuyor." />
            )}

            <Row gutter={[12, 12]}>
              <Col xs={24} sm={8}>
                <div
                  style={{
                    background: '#fff1f0',
                    borderRadius: 10,
                    padding: '12px 10px',
                    textAlign: 'center',
                  }}
                >
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    Borçlu Daire
                  </Typography.Text>
                  <div style={{ fontSize: 20, fontWeight: 700, color: '#cf1322', marginTop: 2 }}>
                    {debtorCount}
                  </div>
                </div>
              </Col>
              <Col xs={24} sm={8}>
                <div
                  style={{
                    background: '#f6ffed',
                    borderRadius: 10,
                    padding: '12px 10px',
                    textAlign: 'center',
                  }}
                >
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    Alacaklı Daire
                  </Typography.Text>
                  <div style={{ fontSize: 20, fontWeight: 700, color: '#389e0d', marginTop: 2 }}>
                    {creditorCount}
                  </div>
                </div>
              </Col>
              <Col xs={24} sm={8}>
                <div
                  style={{
                    background: '#f5f5f5',
                    borderRadius: 10,
                    padding: '12px 10px',
                    textAlign: 'center',
                  }}
                >
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    Borcu Yok
                  </Typography.Text>
                  <div style={{ fontSize: 20, fontWeight: 700, color: '#595959', marginTop: 2 }}>
                    {settledCount}
                  </div>
                </div>
              </Col>
            </Row>
          </Card>
        </Col>
      </Row>

      {/* 4. En Alt Bölüm: Kayıp-Kaçak, Son Fatura ve Sağda Belirgin Primary Sayaç Okuma Kartı */}
      <Row gutter={[20, 20]} style={{ marginTop: 20 }}>
        <Col xs={24} md={8} style={{ display: 'flex' }}>
          <Card
            variant="borderless"
            style={{ ...cardBaseStyle, width: '100%' }}
            styles={{
              header: { padding: '16px 28px', borderBottom: '1px solid #f0f2f5' },
              body: {
                padding: '28px 30px',
                flex: 1,
                display: 'flex',
                flexDirection: 'column',
                justifyContent: 'center',
                alignItems: 'center',
              },
            }}
            title={
              <Space size={10}>
                <div
                  style={{
                    width: 34,
                    height: 34,
                    borderRadius: 10,
                    background: '#f6ffed',
                    color: lossColor(lossPercent),
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    fontSize: 17,
                  }}
                >
                  <DashboardOutlined />
                </div>
                <span style={{ fontWeight: 600, fontSize: 16, color: '#1f1f1f' }}>
                  Kayıp-Kaçak Oranı
                </span>
              </Space>
            }
          >
            <div
              style={{
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                gap: 14,
              }}
            >
              <Progress
                type="dashboard"
                size={175}
                strokeWidth={10}
                percent={Math.min(lossPercent, 100)}
                strokeColor={lossColor(lossPercent)}
                format={() => (
                  <span style={{ fontSize: 24, fontWeight: 700, color: lossColor(lossPercent) }}>
                    %
                    {lossPercent.toLocaleString('tr-TR', {
                      minimumFractionDigits: 2,
                      maximumFractionDigits: 2,
                    })}
                  </span>
                )}
              />
              <Tag
                color={lossPercent < 10 ? 'success' : lossPercent < 25 ? 'warning' : 'error'}
                style={{ padding: '4px 12px', borderRadius: 6, fontWeight: 500, margin: 0 }}
              >
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

        <Col xs={24} md={9} style={{ display: 'flex' }}>
          <Card
            variant="borderless"
            style={{ ...cardBaseStyle, width: '100%' }}
            styles={{
              header: { padding: '16px 28px', borderBottom: '1px solid #f0f2f5' },
              body: {
                padding: '28px 30px',
                flex: 1,
                display: 'flex',
                flexDirection: 'column',
                justifyContent: 'space-between',
              },
            }}
            title={
              <Space size={10}>
                <div
                  style={{
                    width: 34,
                    height: 34,
                    borderRadius: 10,
                    background: token.colorPrimaryBg,
                    color: token.colorPrimary,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    fontSize: 17,
                  }}
                >
                  <FileTextOutlined />
                </div>
                <span style={{ fontWeight: 600, fontSize: 16, color: '#1f1f1f' }}>
                  Son Doğalgaz Faturası
                </span>
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
              <Space direction="vertical" size={18} style={{ width: '100%' }}>
                <div
                  style={{
                    background: '#fafafa',
                    padding: '16px 18px',
                    borderRadius: 10,
                    border: '1px solid #f0f0f0',
                  }}
                >
                  <Typography.Text
                    type="secondary"
                    style={{
                      fontSize: 11,
                      fontWeight: 600,
                      letterSpacing: 0.5,
                      display: 'block',
                    }}
                  >
                    FATURA NUMARASI & DÖNEM
                  </Typography.Text>
                  <Typography.Text
                    strong
                    style={{ fontSize: 16, display: 'block', marginTop: 4, color: '#1f1f1f' }}
                  >
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
                  groupSeparator="."
                  decimalSeparator=","
                  suffix="TL"
                  valueStyle={{ fontSize: 28, fontWeight: 700, color: '#1f1f1f' }}
                />

                <Space wrap size={[8, 8]}>
                  {data.lastInvoiceReconciled === true && (
                    <Tag
                      icon={<CheckCircleOutlined />}
                      color="success"
                      style={{ padding: '4px 10px', borderRadius: 6 }}
                    >
                      Kuruşu Kuruşuna Mutabık
                    </Tag>
                  )}
                  {data.lastInvoiceReconciled === false && (
                    <Tag
                      icon={<AlertOutlined />}
                      color="error"
                      style={{ padding: '4px 10px', borderRadius: 6 }}
                    >
                      Mutabakatsız
                    </Tag>
                  )}
                  {data.lastInvoiceReconciled === null && (
                    <Tag style={{ padding: '4px 10px', borderRadius: 6 }}>Hesaplanmadı</Tag>
                  )}
                  {data.lastCalculationFailedMeterCount > 0 ? (
                    <Tag color="warning" style={{ padding: '4px 10px', borderRadius: 6 }}>
                      {data.lastCalculationFailedMeterCount} daire okunamadı
                    </Tag>
                  ) : (
                    <Tag color="processing" style={{ padding: '4px 10px', borderRadius: 6 }}>
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

        <Col xs={24} md={7} style={{ display: 'flex' }}>
          <Card
            variant="borderless"
            style={{
              ...cardBaseStyle,
              width: '100%',
              background: `linear-gradient(155deg, ${token.colorBgContainer} 0%, ${token.colorPrimaryBg} 100%)`,
            }}
            styles={{
              header: { padding: '16px 28px', borderBottom: `1px solid ${token.colorPrimaryBg}` },
              body: {
                padding: '28px 30px',
                flex: 1,
                display: 'flex',
                flexDirection: 'column',
                justifyContent: 'space-between',
                gap: 20,
              },
            }}
            title={
              <Space size={10}>
                <div
                  style={{
                    width: 34,
                    height: 34,
                    borderRadius: 10,
                    background: token.colorPrimaryBg,
                    color: token.colorPrimary,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    fontSize: 17,
                  }}
                >
                  <ThunderboltOutlined />
                </div>
                <span style={{ fontWeight: 600, fontSize: 16, color: '#1f1f1f' }}>
                  Uzaktan Sayaç Okuma
                </span>
              </Space>
            }
          >
            <div>
              <div
                style={{
                  width: 60,
                  height: 60,
                  borderRadius: 16,
                  background: token.colorPrimaryBg,
                  color: token.colorPrimary,
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
              <Typography.Paragraph
                type="secondary"
                style={{ fontSize: 13, marginBottom: 0, lineHeight: 1.6 }}
              >
                Binadaki aktif <strong>{data.activeMeterCount}</strong> kalorimetre sayacını Izar
                Center üzerinden anlık olarak sorgular ve endeksleri kaydeder.
              </Typography.Paragraph>
            </div>

            {canTrigger && <Button
              type="primary"
              block
              size="large"
              icon={<ThunderboltOutlined />}
              loading={readingMutation.isPending}
              onClick={() => readingMutation.mutate()}
              style={{
                height: 56,
                fontSize: 17,
                fontWeight: 600,
                borderRadius: 10,
                boxShadow: `0 6px 18px ${token.colorPrimary}40`,
              }}
            >
              Sayaçları Şimdi Oku
            </Button>}
          </Card>
        </Col>
      </Row>
    </div>
  )
}
