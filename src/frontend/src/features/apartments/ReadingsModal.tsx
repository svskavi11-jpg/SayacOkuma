import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert,
  Button,
  Input,
  InputNumber,
  List,
  Modal,
  Popconfirm,
  Space,
  Table,
  Tag,
  Typography,
  message,
} from 'antd'
import type { ColumnsType } from 'antd/es/table'
import dayjs from 'dayjs'
import { apiClient } from '../../api/client'
import type {
  ApartmentListItem,
  MeterReadingAuditLogItem,
  MeterReadingCorrectionItem,
  MeterReadingListItem,
  UpdateMeterReadingResult,
} from '../../types/api'

type EditDraft = { newIndexValue: number; reason: string }

/**
 * Etap 6.7: Sayaç okumalarını görüntüleme ve düzeltme (tek veya toplu).
 * Her düzeltmede zorunlu "Değişiklik Nedeni" alınır; kayıt MeterReadingAuditLog'a yazılır.
 * Kilitli döneme düşen okumalar düzenlenemez.
 */
export function ReadingsModal({
  apartment,
  onClose,
}: {
  apartment: ApartmentListItem | null
  onClose: () => void
}) {
  const queryClient = useQueryClient()
  const meterId = apartment?.activeMeterId ?? null

  const [selectedKeys, setSelectedKeys] = useState<React.Key[]>([])
  const [editDrafts, setEditDrafts] = useState<Record<string, EditDraft>>({})
  const [auditFor, setAuditFor] = useState<MeterReadingListItem | null>(null)

  const readingsQuery = useQuery({
    queryKey: ['meter-readings', meterId],
    queryFn: async () =>
      (await apiClient.get<MeterReadingListItem[]>('/meter-readings', { params: { meterId } })).data,
    enabled: meterId !== null,
  })

  const auditQuery = useQuery({
    queryKey: ['meter-reading-audit', auditFor?.id],
    queryFn: async () =>
      (await apiClient.get<MeterReadingAuditLogItem[]>(`/meter-readings/${auditFor!.id}/audit-log`)).data,
    enabled: auditFor !== null,
  })

  const correctionMutation = useMutation({
    mutationFn: async (items: MeterReadingCorrectionItem[]) =>
      (await apiClient.put<UpdateMeterReadingResult>('/meter-readings/corrections', { items })).data,
    onSuccess: (res) => {
      void queryClient.invalidateQueries({ queryKey: ['meter-readings'] })
      void queryClient.invalidateQueries({ queryKey: ['apartments'] })
      setEditDrafts({})
      setSelectedKeys([])
      if (res.rowErrors.length === 0) {
        message.success(`${res.updatedCount} okuma düzeltildi (denetim kayıtları yazıldı).`)
      } else {
        Modal.warning({
          title: `Kısmi başarı: ${res.updatedCount} düzeltildi, ${res.rowErrors.length} satır hatalı`,
          content: (
            <List
              size="small"
              dataSource={res.rowErrors}
              renderItem={(e) => (
                <List.Item>
                  <Typography.Text type="danger">{e}</Typography.Text>
                </List.Item>
              )}
            />
          ),
          okText: 'Tamam',
          width: 560,
        })
      }
    },
    onError: () => {
      message.error('Düzeltme kaydedilemedi.')
    },
  })

  const readings = readingsQuery.data ?? []

  const items = useMemo<MeterReadingCorrectionItem[]>(
    () =>
      Object.entries(editDrafts)
        .filter(([, d]) => Number.isFinite(d.newIndexValue) && d.reason.trim().length > 0)
        .map(([readingId, d]) => ({
          readingId,
          newIndexValue: d.newIndexValue,
          reason: d.reason.trim(),
          changedBy: null, // Faz 7'ye kadar boş → backend "Yönetici" kaydeder
        })),
    [editDrafts],
  )

  const startEdit = (r: MeterReadingListItem) => {
    setEditDrafts((prev) =>
      prev[r.id] ? prev : { ...prev, [r.id]: { newIndexValue: r.indexValue, reason: '' } },
    )
  }

  const startBatchEdit = () => {
    const next = { ...editDrafts }
    for (const key of selectedKeys) {
      const r = readings.find((x) => x.id === key)
      if (r && !r.isLockedPeriod && !next[r.id]) {
        next[r.id] = { newIndexValue: r.indexValue, reason: '' }
      }
    }
    setEditDrafts(next)
  }

  const columns: ColumnsType<MeterReadingListItem> = [
    {
      title: 'Okuma Tarihi',
      dataIndex: 'readingDateUtc',
      width: 140,
      render: (v: string) => dayjs(v).format('DD.MM.YYYY HH:mm'),
    },
    {
      title: 'Endeks (kWh)',
      dataIndex: 'indexValue',
      width: 160,
      render: (v: number, r: MeterReadingListItem) =>
        editDrafts[r.id] ? (
          <InputNumber
            style={{ width: '100%' }}
            min={0}
            step={0.001}
            decimalSeparator=","
            value={editDrafts[r.id].newIndexValue}
            onChange={(val) =>
              setEditDrafts((prev) => ({
                ...prev,
                [r.id]: { ...prev[r.id], newIndexValue: val ?? 0 },
              }))
            }
          />
        ) : (
          v.toFixed(3)
        ),
    },
    {
      title: 'Tüketim (kWh)',
      dataIndex: 'consumptionKWh',
      width: 110,
      align: 'right',
      render: (v: number) => v.toFixed(3),
    },
    { title: 'Kaynak', dataIndex: 'source', width: 100 },
    {
      title: 'Durum',
      width: 150,
      render: (_: unknown, r: MeterReadingListItem) => (
        <Space size={4} wrap>
          {r.isInitialReading && <Tag color="blue">Başlangıç</Tag>}
          {r.isLockedPeriod && <Tag color="red">Kilitli</Tag>}
        </Space>
      ),
    },
    {
      title: 'Düzeltme Nedeni',
      width: 220,
      render: (_: unknown, r: MeterReadingListItem) =>
        editDrafts[r.id] ? (
          <Input
            placeholder="Zorunlu — neden düzeltildi?"
            value={editDrafts[r.id].reason}
            onChange={(e) =>
              setEditDrafts((prev) => ({
                ...prev,
                [r.id]: { ...prev[r.id], reason: e.target.value },
              }))
            }
          />
        ) : (
          <span style={{ color: '#bbb' }}>—</span>
        ),
    },
    {
      title: 'Denetim',
      width: 190,
      render: (_: unknown, r: MeterReadingListItem) => (
        <Space size={4}>
          <Button
            size="small"
            type="link"
            disabled={r.isLockedPeriod}
            onClick={() => startEdit(r)}
          >
            Düzenle
          </Button>
          <Button size="small" type="link" onClick={() => setAuditFor(r)}>
            Log{r.auditLogCount > 0 ? ` (${r.auditLogCount})` : ''}
          </Button>
        </Space>
      ),
    },
  ]

  return (
    <Modal
      title={`Sayaç Okumaları — Daire ${apartment?.doorNumber ?? ''}`}
      open={apartment !== null}
      onCancel={onClose}
      footer={null}
      width={1060}
    >
      <Space direction="vertical" size={12} style={{ width: '100%' }}>
        {apartment && !apartment.activeMeterId && (
          <Alert type="warning" showIcon message="Bu daireye bağlı aktif kalorimetre bulunamadı." />
        )}

        <Space wrap>
          <Typography.Text type="secondary">
            Düzenlenecek okumaları seçip "Toplu Düzenle" ile açın veya satır bazlı "Düzenle" kullanın.
            Kilitli dönem okumaları düzenlenemez.
          </Typography.Text>
          <Button
            size="small"
            disabled={selectedKeys.length === 0}
            onClick={startBatchEdit}
          >
            Toplu Düzenle ({selectedKeys.length})
          </Button>
          <Popconfirm
            title="Düzeltmeler kaydedilsin mi?"
            description="Her düzeltme için denetim kaydı (eski/yeni değer + neden) yazılır."
            okText="Kaydet"
            cancelText="Vazgeç"
            disabled={items.length === 0}
            onConfirm={() => correctionMutation.mutate(items)}
          >
            <Button
              type="primary"
              size="small"
              disabled={items.length === 0}
              loading={correctionMutation.isPending}
            >
              Düzeltmeleri Kaydet ({items.length})
            </Button>
          </Popconfirm>
        </Space>

        <Table
          rowKey="id"
          columns={columns}
          dataSource={readings}
          loading={readingsQuery.isLoading}
          size="small"
          pagination={{ pageSize: 10 }}
          rowSelection={{
            selectedRowKeys: selectedKeys,
            onChange: (keys) => setSelectedKeys(keys),
            getCheckboxProps: (r: MeterReadingListItem) => ({ disabled: r.isLockedPeriod }),
          }}
        />

        <Modal
          title={`Düzeltme Geçmişi — ${auditFor ? dayjs(auditFor.readingDateUtc).format('DD.MM.YYYY HH:mm') : ''}`}
          open={auditFor !== null}
          onCancel={() => setAuditFor(null)}
          footer={null}
          width={720}
        >
          {auditQuery.data && auditQuery.data.length > 0 ? (
            <List
              size="small"
              dataSource={auditQuery.data}
              renderItem={(l: MeterReadingAuditLogItem) => (
                <List.Item>
                  <Space direction="vertical" size={2} style={{ width: '100%' }}>
                    <Typography.Text>
                      <strong>
                        {l.oldIndexValue.toFixed(3)} → {l.newIndexValue.toFixed(3)} kWh
                      </strong>{' '}
                      (tüketim {l.oldConsumptionKWh.toFixed(3)} → {l.newConsumptionKWh.toFixed(3)})
                    </Typography.Text>
                    <Typography.Text type="secondary">
                      Neden: {l.reason} — {l.changedBy},{' '}
                      {dayjs(l.changedOnUtc).format('DD.MM.YYYY HH:mm')}
                    </Typography.Text>
                  </Space>
                </List.Item>
              )}
            />
          ) : (
            <Alert type="info" message="Bu okuma için düzeltme kaydı yok." />
          )}
        </Modal>
      </Space>
    </Modal>
  )
}