import { useQuery } from '@tanstack/react-query'
import { Button, Card, Col, Empty, Row, Space, Tag, Typography } from 'antd'
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

const tl = (value: number) => value.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })

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

const cardStyle: React.CSSProperties = {
  borderRadius: 12,
  boxShadow: '0 4px 16px rgba(15, 23, 42, 0.06)',
  border: 'none',
  height: '100%',
}

function getBankAccentColor(bankName: string, index: number): string {
  const normalized = bankName.toLocaleLowerCase('tr-TR')
  if (normalized.includes('garanti')) return '#237804'
  if (normalized.includes('teb')) return '#0958d9'
  if (normalized.includes('iş') || normalized.includes('isbank')) return '#1d39c4'
  if (normalized.includes('ziraat')) return '#cf1322'
  if (normalized.includes('akbank')) return '#d4380d'
  if (normalized.includes('yapı') || normalized.includes('yapi')) return '#0050b3'
  const palette = ['#1677ff', '#13c2c2', '#722ed1', '#eb2f96']
  return palette[index % palette.length]
}

function accountLabel(account: BankDashboardAccount, accounts: BankDashboardAccount[]) {
  const sharedName = accounts.filter((item) => item.bankName === account.bankName).length > 1
  return sharedName ? `${account.bankName} · ${account.accountName}` : account.bankName
}

export function BankPanel() {
  const navigate = useNavigate()
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
      {/* Sol Taraf: Banka Hesapları Kartları */}
      <Col xs={24} lg={9}>
        <Card
          variant="borderless"
          style={cardStyle}
          styles={{ body: { padding: 20 } }}
          title={
            <Space size={8}>
              <BankOutlined style={{ color: '#1677ff', fontSize: 18 }} />
              <span style={{ fontWeight: 600, fontSize: 16 }}>Banka Hesapları</span>
            </Space>
          }
          extra={
            <Button
              type="link"
              size="small"
              icon={<BankOutlined />}
              onClick={() => navigate('/bank-accounts')}
              style={{ fontWeight: 500 }}
            >
              Tüm Hesaplar
            </Button>
          }
          loading={banksQuery.isLoading}
        >
          {accounts.length === 0 ? (
            <Empty description="Tanımlı banka hesabı bulunmuyor." />
          ) : (
            <Space direction="vertical" size={14} style={{ width: '100%' }}>
              {/* Toplam Banka Mevcudu Özeti */}
              <div
                style={{
                  background: 'linear-gradient(135deg, #f0f5ff 0%, #e6f4ff 100%)',
                  borderRadius: 10,
                  padding: '14px 16px',
                  border: '1px solid #bae0ff',
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center',
                }}
              >
                <div>
                  <Typography.Text type="secondary" style={{ fontSize: 12, fontWeight: 500 }}>
                    TOPLAM BANKA MEVCUDU
                  </Typography.Text>
                  <div
                    style={{
                      fontSize: 24,
                      fontWeight: 700,
                      color: (data?.totalCurrentBalance ?? 0) < 0 ? '#cf1322' : '#0958d9',
                      lineHeight: 1.25,
                      marginTop: 2,
                    }}
                  >
                    {tl(data?.totalCurrentBalance ?? 0)}
                  </div>
                  <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                    Dönem Başı Devir: {tl(data?.totalOpeningBalance ?? 0)}
                  </Typography.Text>
                </div>
                <div
                  style={{
                    width: 44,
                    height: 44,
                    borderRadius: 12,
                    background: '#1677ff',
                    color: '#fff',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    fontSize: 20,
                    boxShadow: '0 4px 10px rgba(22, 119, 255, 0.25)',
                  }}
                >
                  <WalletOutlined />
                </div>
              </div>

              {/* Tekil Banka Hesap Kartları (Garanti, TEB vb.) */}
              {accounts.map((account, idx) => {
                const accent = getBankAccentColor(account.bankName, idx)
                return (
                  <div
                    key={account.id}
                    style={{
                      background: '#fafafa',
                      borderRadius: 10,
                      padding: '14px 16px',
                      border: '1px solid #f0f0f0',
                      borderLeft: `4px solid ${accent}`,
                      transition: 'all 0.2s ease',
                    }}
                  >
                    <div
                      style={{
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'flex-start',
                        gap: 12,
                        marginBottom: 10,
                      }}
                    >
                      <div style={{ minWidth: 0 }}>
                        <Typography.Text strong style={{ fontSize: 15, display: 'block' }} ellipsis>
                          {accountLabel(account, accounts)}
                        </Typography.Text>
                        <Typography.Text type="secondary" style={{ fontSize: 12 }}>
                          {account.accountName} · Devir: {tl(account.openingBalance)}
                        </Typography.Text>
                      </div>
                      <Tag
                        color={account.currentBalance < 0 ? 'error' : 'success'}
                        style={{
                          margin: 0,
                          fontSize: 14,
                          fontWeight: 700,
                          padding: '3px 10px',
                          borderRadius: 6,
                        }}
                      >
                        {tl(account.currentBalance)}
                      </Tag>
                    </div>

                    <div
                      style={{
                        display: 'flex',
                        justifyContent: 'space-between',
                        gap: 8,
                        paddingTop: 8,
                        borderTop: '1px dashed #e8e8e8',
                        fontSize: 12,
                      }}
                    >
                      <span style={{ color: '#389e0d', fontWeight: 500 }}>
                        <ArrowUpOutlined style={{ marginRight: 4 }} />
                        Giren: {tl(account.moneyIn)}
                      </span>
                      <span style={{ color: '#cf1322', fontWeight: 500 }}>
                        <ArrowDownOutlined style={{ marginRight: 4 }} />
                        Çıkan: {tl(account.moneyOut)}
                      </span>
                    </div>
                  </div>
                )
              })}
            </Space>
          )}
        </Card>
      </Col>

      {/* Sağ Taraf: Aylık Gelir-Gider Alan Grafiği (Area Chart) */}
      <Col xs={24} lg={15}>
        <Card
          variant="borderless"
          style={cardStyle}
          styles={{ body: { padding: 20 } }}
          title={
            <Space size={8}>
              <LineChartOutlined style={{ color: '#389e0d', fontSize: 18 }} />
              <span style={{ fontWeight: 600, fontSize: 16 }}>
                {data?.year ?? new Date().getFullYear()} Yılı Aylık Nakit Akışı Trendi
              </span>
            </Space>
          }
          extra={
            <Typography.Text type="secondary" style={{ fontSize: 12 }}>
              Bankası seçilen hareketler (iptaller hariç)
            </Typography.Text>
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
              height={310}
              legend={{ color: { position: 'top' } }}
              scale={{
                color: {
                  domain: ['Tahsilat (Giren)', 'Gider (Çıkan)'],
                  range: ['#389e0d', '#cf1322'],
                },
              }}
              style={{
                fillOpacity: 0.16,
              }}
              line={{
                shapeField: 'smooth',
                style: {
                  lineWidth: 2.5,
                },
              }}
              axis={{
                y: {
                  labelFormatter: (value: number) =>
                    `${Number(value).toLocaleString('tr-TR', { maximumFractionDigits: 0 })} ₺`,
                },
              }}
              tooltip={{
                items: [{ channel: 'y', valueFormatter: (value: number) => tl(Number(value)) }],
              }}
            />
          )}
        </Card>
      </Col>
    </Row>
  )
}
