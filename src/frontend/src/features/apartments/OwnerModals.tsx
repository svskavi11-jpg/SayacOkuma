import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Form, Input, Modal, Table, Tag, Typography, message } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import dayjs from 'dayjs'
import { apiClient } from '../../api/client'
import type {
  ApartmentListItem,
  ApartmentOwnerHistoryItem,
  UpdateApartmentOwnerResult,
} from '../../types/api'

/** Etap 6.7: Kat maliki güncelleme — SCD Type 2 (eski kayıt kapanır, geçmiş korunur). */
export function OwnerEditModal({
  apartment,
  onClose,
}: {
  apartment: ApartmentListItem | null
  onClose: () => void
}) {
  const queryClient = useQueryClient()
  const [form] = Form.useForm<{ ownerName: string }>()

  const ownerMutation = useMutation({
    mutationFn: async (input: { apartmentId: string; ownerName: string }) =>
      (
        await apiClient.put<UpdateApartmentOwnerResult>(
          `/apartments/${input.apartmentId}/owner`,
          { ownerName: input.ownerName },
        )
      ).data,
    onSuccess: (res) => {
      void queryClient.invalidateQueries({ queryKey: ['apartments'] })
      if (res.changed) {
        message.success(
          `Kat maliki güncellendi (SCD Type 2): ${res.ownerName} — ${dayjs(res.validFrom).format('DD.MM.YYYY HH:mm')} itibarıyla geçerli.`,
        )
      } else {
        message.info('Kat maliki zaten aynı — değişiklik yapılmadı.')
      }
      onClose()
    },
    onError: () => {
      message.error('Kat maliki güncellenemedi.')
    },
  })

  return (
    <Modal
      title={`Kat Maliki Güncelle — Daire ${apartment?.doorNumber ?? ''}`}
      open={apartment !== null}
      onCancel={onClose}
      confirmLoading={ownerMutation.isPending}
      okText="Kaydet (SCD)"
      cancelText="İptal"
      onOk={() =>
        form.validateFields().then((values) => {
          if (apartment === null) return
          ownerMutation.mutate({ apartmentId: apartment.id, ownerName: values.ownerName.trim() })
        })
      }
    >
      <Typography.Paragraph type="secondary">
        Yeni malik adı, bugünden itibaren geçerli yeni bir SCD Type 2 kaydı açar; eski malik kaydı
        kapatılır ve geçmişte sorgulanabilir kalır. Ad değişmediyse kayıt açılmaz.
      </Typography.Paragraph>
      <Form form={form} layout="vertical">
        <Form.Item
          name="ownerName"
          label="Kat Maliki Adı Soyadı"
          initialValue={apartment?.ownerName ?? undefined}
          rules={[{ required: true, message: 'Kat maliki adı soyadı zorunludur.' }]}
        >
          <Input maxLength={100} placeholder="Örn: Ahmet Yılmaz" />
        </Form.Item>
      </Form>
    </Modal>
  )
}

const historyColumns: ColumnsType<ApartmentOwnerHistoryItem> = [
  { title: 'Kat Maliki', dataIndex: 'ownerName' },
  {
    title: 'Başlangıç',
    dataIndex: 'validFrom',
    width: 160,
    render: (v: string) => dayjs(v).format('DD.MM.YYYY HH:mm'),
  },
  {
    title: 'Bitiş',
    dataIndex: 'validTo',
    width: 160,
    render: (v: string | null) =>
      v ? dayjs(v).format('DD.MM.YYYY HH:mm') : <Tag color="green">Aktif</Tag>,
  },
]

/** Etap 6.7: Kat maliki geçmişi (SCD Type 2 — kapalı kayıtlar dahil). */
export function OwnerHistoryModal({
  apartment,
  onClose,
}: {
  apartment: ApartmentListItem | null
  onClose: () => void
}) {
  const historyQuery = useQuery({
    queryKey: ['apartment-owner-history', apartment?.id],
    queryFn: async () =>
      (
        await apiClient.get<ApartmentOwnerHistoryItem[]>(
          `/apartments/${apartment!.id}/owner/history`,
        )
      ).data,
    enabled: apartment !== null,
  })

  return (
    <Modal
      title={`Kat Maliki Geçmişi — Daire ${apartment?.doorNumber ?? ''}`}
      open={apartment !== null}
      onCancel={onClose}
      footer={null}
      width={620}
    >
      <Table
        rowKey="id"
        columns={historyColumns}
        dataSource={historyQuery.data}
        loading={historyQuery.isLoading}
        size="small"
        pagination={false}
        locale={{ emptyText: 'Bu daire için henüz kat maliki kaydı girilmemiş.' }}
      />
    </Modal>
  )
}
