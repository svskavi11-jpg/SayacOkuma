import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Button, Card, DatePicker, Popconfirm, Space, Tabs, message } from 'antd'
import { LockOutlined, UnlockOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import { apiClient } from '../../api/client'
import { useAuth } from '../auth/AuthContext'
import type { BudgetPeriodStatus, BudgetSettingsDTO } from '../../types/api'
import { BudgetItemsTab } from './BudgetItemsTab'
import { ExpensesTab } from './ExpensesTab'
import { IncomeItemsTab } from './IncomeItemsTab'
import { RealizationTab } from './RealizationTab'
import { TransfersTab } from './TransfersTab'

/**
 * Faz 6.10: Menü her zaman açıktır.
 * Bütçe takibi kapalıyken yalnız Gider Girişi sekmesi görünür.
 */
export function BudgetPage() {
  const queryClient = useQueryClient()
  const { hasPermission } = useAuth()
  const canEdit = hasPermission('Budget.Edit')
  const [year, setYear] = useState(dayjs().year())
  const [activeKey, setActiveKey] = useState('expenses')

  const settingsQuery = useQuery({
    queryKey: ['budget-settings'],
    queryFn: async () => (await apiClient.get<BudgetSettingsDTO>('/settings/budget')).data,
  })

  const periodQuery = useQuery({
    queryKey: ['budget-period', year],
    queryFn: async () => (await apiClient.get<BudgetPeriodStatus>('/budget/period', { params: { year } })).data,
  })

  const lockMutation = useMutation({
    mutationFn: async (locked: boolean) =>
      (await apiClient.put<BudgetPeriodStatus>('/budget/period/lock', { year, locked })).data,
    onSuccess: (status) => {
      void queryClient.invalidateQueries({ queryKey: ['budget-period'] })
      void queryClient.invalidateQueries({ queryKey: ['budget-realization'] })
      message.success(status.isLocked ? 'Bütçe yılı kilitlendi.' : 'Bütçe yılı kilidi açıldı.')
    },
  })

  const enabled = settingsQuery.data?.isBudgetTrackingEnabled ?? false
  const locked = periodQuery.data?.isLocked ?? false

  useEffect(() => {
    if (!enabled && activeKey !== 'expenses') setActiveKey('expenses')
  }, [enabled, activeKey])

  const items = [
    ...(enabled
      ? [{ key: 'items', label: 'Bütçe Kalemleri', children: <BudgetItemsTab year={year} locked={locked} /> }]
      : []),
    ...(enabled
      ? [{ key: 'income', label: 'Gelir Kalemleri', children: <IncomeItemsTab year={year} locked={locked} /> }]
      : []),
    { key: 'expenses', label: 'Gider Girişi', children: <ExpensesTab year={year} budgetEnabled={enabled} locked={locked} /> },
    ...(enabled
      ? [{ key: 'transfers', label: 'Virman', children: <TransfersTab year={year} locked={locked} /> }]
      : []),
    ...(enabled
      ? [{ key: 'realization', label: 'Gerçekleşme', children: <RealizationTab year={year} /> }]
      : []),
  ]

  return (
    <Card
      title="Bütçe ve Giderler"
      loading={settingsQuery.isLoading}
      extra={
        <Space>
          {canEdit && <Popconfirm
            title={locked ? 'Bu yılın kilidi açılsın mı?' : 'Bu bütçe yılı kilitlensin mi?'}
            description={locked ? 'Kilit açılınca yeni kayıt yazılabilir.' : 'Kilitliyken bu yıla gider, gelir ve virman eklenemez.'}
            okText={locked ? 'Kilidi Aç' : 'Kilitle'}
            cancelText="Vazgeç"
            onConfirm={() => lockMutation.mutate(!locked)}
          >
            <Button
              icon={locked ? <UnlockOutlined /> : <LockOutlined />}
              loading={lockMutation.isPending}
              danger={!locked}
            >
              {locked ? 'Kilidi Aç' : 'Yılı Kilitle'}
            </Button>
          </Popconfirm>}
          <DatePicker
            picker="year"
            value={dayjs().year(year)}
            allowClear={false}
            onChange={(value) => {
              if (value) setYear(value.year())
            }}
          />
        </Space>
      }
    >
      {!enabled && (
        <Alert
          type="info"
          showIcon
          style={{ marginBottom: 16 }}
          message="Bütçe takibi kapalı. Giderler serbest tür ile kaydedilir."
          description="Bütçe kalemleri ve gerçekleşme raporu için Ayarlar’dan bütçe takibini açın."
        />
      )}
      <Tabs activeKey={activeKey} onChange={setActiveKey} items={items} />
    </Card>
  )
}
