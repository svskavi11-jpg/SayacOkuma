import type { CSSProperties } from 'react'
import { useQuery } from '@tanstack/react-query'
import { Button, Card, Col, Empty, Row, Space, Statistic, Tag, Typography, theme } from 'antd'
import {
  ArrowDownOutlined,
  ArrowUpOutlined,
  BankOutlined,
  LineChartOutlined,
  WalletOutlined,
} from '@ant-design/icons'
import { Area } from '@ant-design/plots'
import { useNavigate } from 'react-router-dom'
import { apiClient } from '../../api/client'
import type { BankDashboard, BankDashboardAccount } from '../../types/api'

const tl = (value: number) =>
  `${value.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} TL`

const MONTHS = [
  'Ocak',
  'Şubat',
  'Mart',
  'Nisan',
  'Mayıs',
  'Haziran',
  'Temmuz',
  'Ağustos',
  'Eylül',
  'Ekim',
  'Kasım',
  'Aralık',
]

const panelCardStyle: CSSProperties = {
  borderRadius: 12,
  boxShadow: '0 4px 18px rgba(15, 23, 42, 0.06)',
  border: 'none',
  height: '100%',
  display: 'flex',
  flexDirection: 'column',
}

function getBankTheme(bankName: string, index: number, primary: string): {
  accent: string
  bgTint: string
  badgeBg: string
} {
  const normalized = bankName.toLocaleLowerCase('tr-TR')
  if (normalized.includes('garanti')) {
    return { accent: '#389e0d', bgTint: '#f6ffed', badgeBg: '#d9f7be' }
  }
  if (normalized.includes('teb')) {
    return { accent: '#0958d9', bgTint: '#f0f5ff', badgeBg: '#bae0ff' }
  }
  if (normalized.includes('iş') || normalized.includes('isbank')) {
    return { accent: '#1d39c4', bgTint: '#f0f5ff', badgeBg: '#d6e4ff' }
  }
  if (normalized.includes('ziraat')) {
    return { accent: '#cf1322', bgTint: '#fff1f0', badgeBg: '#ffccc7' }
  }
  if (normalized.includes('akbank')) {
    return { accent: '#d4380d', bgTint: '#fff2e8', badgeBg: '#ffd8bf' }
  }
  if (normalized.includes('yapı') || normalized.includes('yapi')) {
    return { accent: '#0050b3', bgTint: '#e6f4ff', badgeBg: '#bae0ff' }
  }
  const palette = [
    { accent: primary, bgTint: '#f0f5ff', badgeBg: '#bae0ff' },
    { accent: '#13c2c2', bgTint: '#e6fffb', badgeBg: '#b5f5ec' },
    { accent: '#722ed1', bgTint: '#f9f0ff', badgeBg: '#efdbff' },
    { accent: '#eb2f96', bgTint: '#fff0f6', badgeBg: '#ffd6e7' },
  ]
  return palette[index % palette.length]
}

function accountLabel(account: BankDashboardAccount, accounts: BankDashboardAccount[]) {
  const sharedName = accounts.filter((item) => item.bankName === account.bankName).length > 1
  return sharedName ? `${account.bankName} · ${account.accountName}` : account.bankName
}

export function BankPanel() {
  const navigate = useNavigate()
  const { token } = theme.useToken()
  const banksQuery = useQuery({
    queryKey: ['dashboard-banks'],
    queryFn: async () => (await apiClient.get<BankDashboard>('/dashboard/banks')).data,
  })

  const data = banksQuery.data
  const accounts = data?.accounts ?? []

  const byMonth = (data?.months ?? []).flatMap((month) => [
    {
      name: MONTHS[month.month - 1] ?? String(month.month),
      kind: 'Tahsilat (Giren)',
      amount: month.moneyIn,
    },
    {
      name: MONTHS[month.month - 1] ?? String(month.month),
      kind: 'Gider (Çıkan)',
      amount: month.moneyOut,
    },
  ])

  return (
    <Row gutter={[20, 20]} style={{ marginTop: 20 }}>
      {/* Sol Taraf: Banka Hesapları Küçük Şık Özet Kartları */}
      <Col xs={24} lg={9} style={{ display: 'flex' }}>
        <Card
          variant="borderless"
          style={{ ...panelCardStyle, width: '100%' }}
          styles={{
            header: { padding: '16px 24px', borderBottom: '1px solid #f0f2f5' },
            body: { padding: 24, flex: 1, display: 'flex', flexDirection: 'column', gap: 16 },
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
                <BankOutlined />
              </div>
              <span style={{ fontWeight: 600, fontSize: 16, color: '#1f1f1f' }}>
                Banka Hesapları
              </span>
            </Space>
          }
          extra={
            <Button
              type="link"
              size="small"
              onClick={() => navigate('/bank-accounts')}
              style={{ fontWeight: 500, padding: 0 }}
            >
              Tüm Hesaplar →
            </Button>
          }
          loading={banksQuery.isLoading}
        >
          {accounts.length === 0 ? (
            <Empty description="Tanımlı banka hesabı bulunmuyor." />
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 14, flex: 1 }}>
              {/* Toplam Banka Mevcudu Üst Özet Kartı */}
              <div
                style={{
                  background: `linear-gradient(135deg, ${token.colorPrimaryActive} 0%, ${token.colorPrimary} 100%)`,
                  borderRadius: 12,
                  padding: '18px 20px',
                  color: '#fff',
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                  boxShadow: '0 6px 16px rgba(22, 119, 255, 0.22)',
                }}
              >
                <div>
                  <div
                    style={{
                      fontSize: 11,
                      fontWeight: 600,
                      textTransform: 'uppercase',
                      letterSpacing: 0.7,
                      opacity: 0.85,
                      marginBottom: 4,
                    }}
                  >
                    Toplam Banka Mevcudu
                  </div>
                  <div style={{ fontSize: 26, fontWeight: 800, lineHeight: 1.2, letterSpacing: -0.3 }}>
                    {tl(data?.totalCurrentBalance ?? 0)}
                  </div>
                  <div style={{ fontSize: 12, opacity: 0.85, marginTop: 6 }}>
                    Dönem Başı Devir: <strong>{tl(data?.totalOpeningBalance ?? 0)}</strong>
                  </div>
                </div>
                <div
                  style={{
                    width: 48,
                    height: 48,
                    borderRadius: 12,
                    background: 'rgba(255, 255, 255, 0.18)',
                    color: '#fff',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    fontSize: 22,
                    flexShrink: 0,
                  }}
                >
                  <WalletOutlined />
                </div>
              </div>

              {/* Banka Hesapları (Garanti, TEB vb.) Küçük Şık Özet Kartları */}
              <div style={{ display: 'flex', flexDirection: 'column', gap: 12 }}>
                {accounts.map((account, idx) => {
                  const bankLook = getBankTheme(account.bankName, idx, token.colorPrimary)
                  return (
                    <Card
                      key={account.id}
                      size="small"
                      variant="borderless"
                      styles={{ body: { padding: '16px 18px' } }}
                      style={{
                        borderRadius: 12,
                        background: '#ffffff',
                        border: '1px solid #f0f2f5',
                        borderLeft: `4px solid ${bankLook.accent}`,
                        boxShadow: '0 2px 10px rgba(15, 23, 42, 0.04)',
                      }}
                    >
                      <div
                        style={{
                          display: 'flex',
                          justifyContent: 'space-between',
                          alignItems: 'center',
                          gap: 12,
                          marginBottom: 12,
                        }}
                      >
                        <div style={{ display: 'flex', alignItems: 'center', gap: 12, minWidth: 0 }}>
                          <div
                            style={{
                              width: 40,
                              height: 40,
                              borderRadius: 10,
                              background: bankLook.bgTint,
                              color: bankLook.accent,
                              display: 'flex',
                              alignItems: 'center',
                              justifyContent: 'center',
                              fontSize: 18,
                              fontWeight: 700,
                              flexShrink: 0,
                            }}
                          >
                            <BankOutlined />
                          </div>
                          <div style={{ minWidth: 0 }}>
                            <Typography.Text
                              strong
                              style={{ fontSize: 15, color: '#1f1f1f', display: 'block' }}
                              ellipsis
                            >
                              {accountLabel(account, accounts)}
                            </Typography.Text>
                            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                              {account.accountName} · Devir: {tl(account.openingBalance)}
                            </Typography.Text>
                          </div>
                        </div>

                        <Statistic
                          value={account.currentBalance}
                          precision={2}
                          groupSeparator="."
                          decimalSeparator=","
                          suffix="TL"
                          valueStyle={{
                            fontSize: 18,
                            fontWeight: 700,
                            color: account.currentBalance < 0 ? '#cf1322' : bankLook.accent,
                            whiteSpace: 'nowrap',
                          }}
                        />
                      </div>

                      <div
                        style={{
                          display: 'flex',
                          justifyContent: 'space-between',
                          alignItems: 'center',
                          gap: 8,
                          paddingTop: 10,
                          borderTop: '1px dashed #f0f0f0',
                        }}
                      >
                        <Tag
                          bordered={false}
                          color="success"
                          style={{ margin: 0, fontSize: 12, fontWeight: 500, borderRadius: 6 }}
                        >
                          <ArrowUpOutlined style={{ marginRight: 4 }} />
                          Giren: {tl(account.moneyIn)}
                        </Tag>
                        <Tag
                          bordered={false}
                          color="error"
                          style={{ margin: 0, fontSize: 12, fontWeight: 500, borderRadius: 6 }}
                        >
                          <ArrowDownOutlined style={{ marginRight: 4 }} />
                          Çıkan: {tl(account.moneyOut)}
                        </Tag>
                      </div>
                    </Card>
                  )
                })}
              </div>
            </div>
          )}
        </Card>
      </Col>

      {/* Sağ Taraf: Aylık Gelir-Gider Yumuşatılmış Alan Grafiği (Area Chart) */}
      <Col xs={24} lg={15} style={{ display: 'flex' }}>
        <Card
          variant="borderless"
          style={{ ...panelCardStyle, width: '100%' }}
          styles={{
            header: { padding: '16px 24px', borderBottom: '1px solid #f0f2f5' },
            body: { padding: 24, flex: 1, display: 'flex', flexDirection: 'column', justifyContent: 'center' },
          }}
          title={
            <Space size={10}>
              <div
                style={{
                  width: 34,
                  height: 34,
                  borderRadius: 10,
                  background: '#f6ffed',
                  color: '#389e0d',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  fontSize: 17,
                }}
              >
                <LineChartOutlined />
              </div>
              <div>
                <span style={{ fontWeight: 600, fontSize: 16, color: '#1f1f1f', display: 'block' }}>
                  {data?.year ?? new Date().getFullYear()} Yılı Aylık Nakit Akışı Trendi
                </span>
              </div>
            </Space>
          }
          extra={
            <Space size={8} wrap>
              <Tag bordered={false} color="success" style={{ borderRadius: 6, fontWeight: 500 }}>
                Toplam Giriş: {tl(data?.totalMoneyIn ?? 0)}
              </Tag>
              <Tag bordered={false} color="error" style={{ borderRadius: 6, fontWeight: 500 }}>
                Toplam Çıkış: {tl(data?.totalMoneyOut ?? 0)}
              </Tag>
            </Space>
          }
          loading={banksQuery.isLoading}
        >
          {byMonth.length === 0 ? (
            <Empty description="Aylık nakit akışı verisi bulunamadı." />
          ) : (
            <Area
              data={byMonth}
              xField="name"
              yField="amount"
              colorField="kind"
              shapeField="smooth"
              height={340}
              legend={{
                color: {
                  position: 'top',
                  layout: { justifyContent: 'flex-end' },
                },
              }}
              scale={{
                color: {
                  domain: ['Tahsilat (Giren)', 'Gider (Çıkan)'],
                  range: ['#389e0d', '#cf1322'],
                },
              }}
              style={{
                fillOpacity: 0.18,
              }}
              line={{
                shapeField: 'smooth',
                style: {
                  lineWidth: 3,
                },
              }}
              axis={{
                x: {
                  labelAutoRotate: false,
                },
                y: {
                  labelFormatter: (value: number) =>
                    `${Number(value).toLocaleString('tr-TR', { maximumFractionDigits: 0 })} TL`,
                },
              }}
              tooltip={{
                items: [
                  {
                    channel: 'y',
                    valueFormatter: (value: number) => tl(Number(value)),
                  },
                ],
              }}
            />
          )}
        </Card>
      </Col>
    </Row>
  )
}
