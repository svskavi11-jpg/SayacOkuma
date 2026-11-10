import { useQuery } from '@tanstack/react-query'
import { DatePicker, Input, Select, Space, Table, Tag } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { useState } from 'react'
import dayjs, { type Dayjs } from 'dayjs'
import { apiClient } from '../../api/client'
import { formatDate } from '../../utils/dateUtils'
import type { ResidentHistoryItem, ResidentType } from '../../types/api'

const { RangePicker } = DatePicker

type RangeValue = [Dayjs, Dayjs] | null

function roleTag(residentType: ResidentType) {
  return residentType === 'Kiraci'
    ? <Tag color="geekblue">Kiracı</Tag>
    : <Tag color="purple">Ev Sahibi</Tag>
}

export function ResidentHistoryTab() {
  const [search, setSearch] = useState('')
  const [role, setRole] = useState<ResidentType | undefined>()
  const [range, setRange] = useState<RangeValue>(null)
  const term = search.trim()
  const from = range?.[0].format('YYYY-MM-DD')
  const to = range?.[1].format('YYYY-MM-DD')

  const historyQuery = useQuery({
    queryKey: ['resident-history', term, role ?? '', from ?? '', to ?? ''],
    queryFn: async () => {
      const params = new URLSearchParams()
      if (term) {
        params.set('doorNumber', term)
        params.set('name', term)
      }
      if (role) params.set('residentType', role)
      if (from) params.set('from', from)
      if (to) params.set('to', to)
      const query = params.toString()
      return (await apiClient.get<ResidentHistoryItem[]>(`/residents/history${query ? `?${query}` : ''}`)).data
    },
  })

  const columns: ColumnsType<ResidentHistoryItem> = [
    { title: 'Daire No', dataIndex: 'doorNumber', width: 90 },
    { title: 'Kişi Adı', dataIndex: 'ownerName' },
    {
      title: 'Rol',
      dataIndex: 'residentType',
      width: 120,
      render: (value: ResidentType) => roleTag(value),
    },
    {
      title: 'Giriş Tarihi',
      dataIndex: 'validFrom',
      width: 130,
      render: (value: string) => formatDate(value),
    },
    {
      title: 'Çıkış Tarihi',
      dataIndex: 'validTo',
      width: 150,
      render: (value: string | null) => (value ? formatDate(value) : 'Devam Ediyor'),
    },
    {
      title: 'Sözleşme Başlangıç',
      dataIndex: 'leaseStartDate',
      width: 150,
      render: (value: string | null) => (value ? dayjs(value).format('DD.MM.YYYY') : '-'),
    },
    {
      title: 'Sözleşme Bitiş',
      dataIndex: 'leaseEndDate',
      width: 150,
      render: (value: string | null) => (value ? dayjs(value).format('DD.MM.YYYY') : '-'),
    },
    {
      title: 'Telefon',
      dataIndex: 'phoneNumber',
      width: 150,
      render: (value: string | null) => value || '—',
    },
  ]

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Space wrap>
        <Input.Search
          allowClear
          placeholder="Kapı no veya kişi adı"
          style={{ width: 260 }}
          value={search}
          onChange={(event) => setSearch(event.target.value)}
        />
        <Select
          allowClear
          placeholder="Rol"
          style={{ width: 160 }}
          value={role}
          onChange={(value) => setRole(value)}
          options={[
            { value: 'EvSahibi', label: 'Ev Sahibi' },
            { value: 'Kiraci', label: 'Kiracı' },
          ]}
        />
        <RangePicker
          allowClear
          format="DD.MM.YYYY"
          value={range}
          placeholder={['Başlangıç', 'Bitiş']}
          onChange={(value) => {
            if (value?.[0] && value[1]) setRange([value[0], value[1]])
            else setRange(null)
          }}
        />
      </Space>
      <Table
        scroll={{ x: 'max-content' }}
        rowKey="id"
        size="small"
        columns={columns}
        dataSource={historyQuery.data}
        loading={historyQuery.isLoading}
        pagination={{ pageSize: 20, showSizeChanger: false }}
        locale={{ emptyText: 'Bu filtreye uyan sakin kaydı yok.' }}
      />
    </Space>
  )
}
