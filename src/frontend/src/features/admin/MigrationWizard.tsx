import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Button, Popconfirm, Space, Steps, Timeline, Typography } from 'antd'
import dayjs from 'dayjs'
import { apiClient } from '../../api/client'

interface MaintenanceStatus {
  maintenance: boolean
  since: string | null
}

const commands = [
  {
    title: 'Yeni sunucuda kurulum',
    command: 'sudo bash scripts/setup-target-server.sh',
    note: 'Depoyu yeni Ubuntu sunucusuna koyup bu komutu orada çalıştırın. Yalnız boş PostgreSQL açılır. API ve web kapalı kalır. .env varsa script durur.',
  },
  {
    title: 'Bu sunucuda paket',
    command: 'bash scripts/package-source-server.sh',
    note: 'Bakım açıkken, deponun kökünden çalıştırın. Paket migration-out klasörüne yazılır. İçinde veritabanı, evraklar, imajlar ve imza anahtarı vardır.',
  },
  {
    title: 'Yeni sunucuda yükleme',
    command: 'sudo bash scripts/import-to-target.sh /yol/paket',
    note: 'Paketi yeni sunucuya siz kopyalayın. Sayılar uyuşmazsa API açılmaz. DNS değişmez. Yükleme bitince yığını siz başlatırsınız.',
  },
]

export function MigrationWizard() {
  const queryClient = useQueryClient()
  const status = useQuery({
    queryKey: ['migration-status'],
    queryFn: async () => (await apiClient.get<MaintenanceStatus>('/admin/migration/status')).data,
    refetchInterval: 3000,
  })

  const toggle = useMutation({
    mutationFn: (enabled: boolean) => apiClient.post<MaintenanceStatus>('/admin/migration/maintenance', { enabled }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['migration-status'] }),
  })

  const maintenance = status.data?.maintenance === true
  const since = status.data?.since ? dayjs(status.data.since).format('DD.MM.YYYY HH:mm') : null

  return (
    <Steps
      direction="vertical"
      current={maintenance ? 2 : 0}
      items={[
        {
          title: 'Uyarı',
          description: (
            <Space direction="vertical" size={12}>
              <Alert
                type="warning"
                showIcon
                message="Bakım modu yazmaları durdurur"
                description="Tahsilat, evrak ve diğer kayıtlar 503 alır. Okuma, giriş ve bu ekran açık kalır. Bakım, siz kapatana kadar açık kalır."
              />
              {maintenance ? (
                <Alert type="success" showIcon message={since ? `Bakım açık. ${since} tarihinden beri.` : 'Bakım açık.'} />
              ) : (
                <Button type="primary" loading={toggle.isPending} onClick={() => toggle.mutate(true)}>
                  Bakım modunu aç
                </Button>
              )}
            </Space>
          ),
        },
        {
          title: 'Yapılacaklar',
          description: (
            <Timeline
              items={commands.map((item) => ({
                children: (
                  <Space direction="vertical" size={4}>
                    <Typography.Text strong>{item.title}</Typography.Text>
                    <Typography.Paragraph code copyable style={{ marginBottom: 0 }}>
                      {item.command}
                    </Typography.Paragraph>
                    <Typography.Text type="secondary">{item.note}</Typography.Text>
                  </Space>
                ),
              }))}
            />
          ),
        },
        {
          title: 'Durum',
          description: (
            <Space direction="vertical" size={12}>
              <Alert
                type={maintenance ? 'info' : 'warning'}
                showIcon
                message={maintenance ? 'Kaynak sunucu bakımda' : 'Bakım kapalı'}
                description="DNS'i yeni sunucuya çevirin. Kaynak sunucudaki bakım, geçiş bitene kadar açık kalır. Taşıma bitmiş olsa bile bakım kendiliğinden kapanmaz."
              />
              <Popconfirm
                title="Bakım kapatılsın mı?"
                description="DNS hâlâ bu sunucuyu gösteriyorsa yeni tahsilatlar pakete girmez."
                okText="Bakımı kapat"
                cancelText="Vazgeç"
                disabled={!maintenance}
                onConfirm={() => toggle.mutate(false)}
              >
                <Button danger disabled={!maintenance} loading={toggle.isPending}>
                  DNS'i yeni sunucuya çevirdikten sonra Bakımı Kapat
                </Button>
              </Popconfirm>
            </Space>
          ),
        },
      ]}
    />
  )
}
