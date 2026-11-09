import { useEffect, useMemo, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Alert,
  Button,
  Card,
  Checkbox,
  Form,
  Input,
  Modal,
  Radio,
  Segmented,
  Select,
  Space,
  Table,
  Tabs,
  Tag,
  Typography,
  message,
} from 'antd'
import type { ColumnsType } from 'antd/es/table'
import dayjs from 'dayjs'
import { apiClient } from '../../api/client'
import type {
  ApartmentListItem,
  NotificationChannel,
  NotificationDeliveryStatus,
  NotificationLogItem,
  NotificationPlaceholderItem,
  NotificationPreview,
  NotificationPurpose,
  NotificationTemplateItem,
  SendManualNotificationResult,
} from '../../types/api'

const channelLabel: Record<NotificationChannel, string> = {
  Email: 'E-posta',
  Sms: 'SMS',
  WhatsApp: 'WhatsApp',
}

const statusTag: Record<NotificationDeliveryStatus, { color: string; text: string }> = {
  Sent: { color: 'success', text: 'Başarılı' },
  Failed: { color: 'error', text: 'Başarısız' },
  Simulated: { color: 'warning', text: 'Simüle Edildi' },
}

const purposeLabel: Record<NotificationPurpose, string> = {
  General: 'Genel Bildirim',
  Aidat: 'Aidat Bildirimi',
  Heating: 'Isınma Bildirimi',
  AllDebtors: 'Tüm Borçlulara Bildirim',
}

const defaultBodies: Record<NotificationPurpose, string> = {
  General:
    'Sayın [AdSoyad], [DaireNo] numaralı dairenizin [Donem] dönemi bakiyesi [Tutar] tutarındadır. Westalife Residence yönetimi.',
  Aidat:
    'Sayın [AdSoyad], [DaireNo] numaralı dairenizin kalan aidat borcu [AidatBorc] tutarındadır. Bu dönem aidatı: [BuDonemAidat] ([MevcutAy]). Westalife Residence yönetimi.',
  Heating:
    'Sayın [AdSoyad], [DaireNo] numaralı dairenizin [MevcutAy] ısınma bildirimi hazır. Kalan ısınma borcu: [IsinmaBorc]. Faturanız: [PdfLink] Westalife Residence yönetimi.',
  AllDebtors:
    'Sayın [AdSoyad], [DaireNo] numaralı dairenizin toplam borcu [Tutar] tutarındadır (Aidat: [AidatBorc], Isınma: [IsinmaBorc]). Westalife Residence yönetimi.',
}

function matchesPurposeTemplate(purpose: NotificationPurpose, template: NotificationTemplateItem) {
  if (purpose === 'General') return true
  const haystack = `${template.name} ${template.body} ${template.subject ?? ''}`.toLocaleLowerCase('tr-TR')
  if (purpose === 'Aidat') {
    return (
      haystack.includes('[aidatborc]') ||
      haystack.includes('[budonemaidat]') ||
      haystack.includes('aidat')
    )
  }
  if (purpose === 'AllDebtors') {
    return (
      haystack.includes('[tutar]') ||
      haystack.includes('[aidatborc]') ||
      haystack.includes('[isinmaborc]') ||
      haystack.includes('borç') ||
      haystack.includes('borc')
    )
  }
  return (
    haystack.includes('[isinmaborc]') ||
    haystack.includes('[budonemisinma]') ||
    haystack.includes('[pdflink]') ||
    haystack.includes('ısınma') ||
    haystack.includes('isinma') ||
    haystack.includes('doğalgaz') ||
    haystack.includes('dogalgaz')
  )
}

function debtorIds(apartments: ApartmentListItem[], purpose: NotificationPurpose) {
  if (purpose === 'Aidat') return apartments.filter((a) => (a.aidatDebt ?? 0) > 0).map((a) => a.id)
  if (purpose === 'Heating') return apartments.filter((a) => (a.heatingDebt ?? 0) > 0).map((a) => a.id)
  if (purpose === 'AllDebtors') return apartments.filter((a) => (a.currentBalance ?? 0) > 0).map((a) => a.id)
  return apartments.map((a) => a.id)
}

interface SendForm {
  apartmentIds: string[]
  channel: NotificationChannel
  templateId?: string
  subject?: string
  body?: string
  gasInvoiceId?: string
  includePdf?: boolean
}

interface GasInvoiceOption {
  id: string
  invoiceNumber: string
  periodFrom: string
  periodTo: string
}

interface TemplateForm {
  id?: string
  name: string
  channel: NotificationChannel
  subject?: string
  body: string
}

export function NotificationsPage() {
  return (
    <Card title="Bildirim Merkezi">
      <Tabs
        items={[
          { key: 'send', label: 'Bildirim Gönder', children: <SendTab /> },
          { key: 'history', label: 'Gönderim Geçmişi', children: <HistoryTab /> },
          { key: 'templates', label: 'Şablonlar', children: <TemplatesTab /> },
        ]}
      />
    </Card>
  )
}

function SendTab() {
  const queryClient = useQueryClient()
  const [form] = Form.useForm<SendForm>()
  const bodyRef = useRef<HTMLTextAreaElement>(null)
  const [purpose, setPurpose] = useState<NotificationPurpose>('General')
  const apartmentIds = Form.useWatch('apartmentIds', form) as string[] | undefined
  const channel = (Form.useWatch('channel', form) as NotificationChannel | undefined) ?? 'Sms'
  const templateId = Form.useWatch('templateId', form) as string | undefined
  const subject = Form.useWatch('subject', form) as string | undefined
  const body = Form.useWatch('body', form) as string | undefined
  const gasInvoiceId = Form.useWatch('gasInvoiceId', form) as string | undefined
  const includePdf = Form.useWatch('includePdf', form) as boolean | undefined
  const firstApartmentId = apartmentIds?.[0]

  const apartmentsQuery = useQuery({
    queryKey: ['apartments'],
    queryFn: async () => (await apiClient.get<ApartmentListItem[]>('/apartments')).data,
  })

  const templatesQuery = useQuery({
    queryKey: ['notification-templates'],
    queryFn: async () => (await apiClient.get<NotificationTemplateItem[]>('/notifications/templates')).data,
  })

  const gasInvoicesQuery = useQuery({
    queryKey: ['gas-invoices'],
    queryFn: async () => (await apiClient.get<GasInvoiceOption[]>('/gas-invoices')).data,
    enabled: purpose === 'Heating' || Boolean(includePdf),
  })

  const apartments = apartmentsQuery.data ?? []
  const debtors = useMemo(() => debtorIds(apartments, purpose), [apartments, purpose])

  const channelTemplates = useMemo(() => {
    const byChannel = (templatesQuery.data ?? []).filter((t) => t.channel === channel)
    const matched = byChannel.filter((t) => matchesPurposeTemplate(purpose, t))
    return matched.length > 0 ? matched : byChannel
  }, [templatesQuery.data, channel, purpose])

  useEffect(() => {
    const byChannel = (templatesQuery.data ?? []).filter((t) => t.channel === channel)
    const matched = byChannel.filter((t) => matchesPurposeTemplate(purpose, t))
    const templates = matched.length > 0 ? matched : byChannel
    const preferred = templates[0]
    const nextDebtors = debtorIds(apartmentsQuery.data ?? [], purpose)

    form.setFieldsValue({
      templateId: preferred?.id,
      body: preferred ? undefined : defaultBodies[purpose],
      subject: purpose === 'General' ? form.getFieldValue('subject') : purposeLabel[purpose],
      apartmentIds: purpose === 'General' ? [] : nextDebtors,
      includePdf: purpose === 'Heating',
    })
    // Yalnızca tür veya kanal değişince formu hazırla; manuel seçimi bozma.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [purpose, channel])

  const previewQuery = useQuery({
    queryKey: [
      'notification-preview',
      firstApartmentId,
      channel,
      purpose,
      templateId ?? '',
      subject ?? '',
      body ?? '',
    ],
    enabled: Boolean(firstApartmentId),
    queryFn: async () =>
      (
        await apiClient.post<NotificationPreview>('/notifications/preview', {
          apartmentId: firstApartmentId,
          channel,
          purpose,
          templateId: templateId || null,
          subject: templateId ? null : subject ?? null,
          body: templateId ? null : body ?? null,
        })
      ).data,
  })

  const sendMutation = useMutation({
    mutationFn: async (values: SendForm) =>
      (
        await apiClient.post<SendManualNotificationResult>('/notifications', {
          apartmentIds: values.apartmentIds,
          channel: values.channel,
          purpose,
          templateId: values.templateId || null,
          subject: values.templateId ? null : values.subject ?? null,
          body: values.templateId ? null : values.body ?? null,
          gasInvoiceId: values.gasInvoiceId || null,
          includePdf: Boolean(values.includePdf && values.gasInvoiceId),
        })
      ).data,
    onSuccess: (result) => {
      void queryClient.invalidateQueries({ queryKey: ['notification-logs'] })
      message.success(
        `${purposeLabel[result.purpose]} bitti. Başarılı: ${result.sent}, Başarısız: ${result.failed}, Simüle: ${result.simulated}.`,
      )
    },
  })

  const selectDebtors = () => {
    form.setFieldValue('apartmentIds', debtors)
    if (debtors.length === 0) {
      message.info(
        purpose === 'Aidat'
          ? 'Aidat borcu olan daire bulunamadı.'
          : purpose === 'Heating'
            ? 'Isınma borcu olan daire bulunamadı.'
            : purpose === 'AllDebtors'
              ? 'Cari bakiyesi pozitif daire bulunamadı.'
              : 'Seçilecek daire yok.',
      )
    }
  }

  const selectAll = () => {
    form.setFieldValue(
      'apartmentIds',
      apartments.map((a) => a.id),
    )
  }

  return (
    <Form
      form={form}
      layout="vertical"
      initialValues={{ channel: 'Sms', apartmentIds: [], body: defaultBodies.General, includePdf: false }}
      onFinish={(values) => sendMutation.mutate(values)}
      style={{ maxWidth: 820 }}
    >
      <div style={{ marginBottom: 16 }}>
        <Typography.Text strong style={{ display: 'block', marginBottom: 8 }}>
          Gönderim türü
        </Typography.Text>
        <Segmented
          block
          value={purpose}
          onChange={(value) => setPurpose(value as NotificationPurpose)}
          options={[
            { label: 'Genel', value: 'General' },
            { label: 'Aidat', value: 'Aidat' },
            { label: 'Isınma', value: 'Heating' },
            { label: 'Tüm Borçlular', value: 'AllDebtors' },
          ]}
        />
        {purpose !== 'General' && (
          <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
            {purpose === 'Aidat'
              ? 'Aidat borcu olan daireler varsayılan seçilir. Şablon listesi aidat değişkenlerine göre süzülür.'
              : purpose === 'Heating'
                ? 'Isınma borcu olan daireler varsayılan seçilir. SMS’te [PdfLink], e-posta/WhatsApp’ta PDF ek kullanılır.'
                : 'Cari bakiyesi 0’dan büyük tüm daireler seçilir. [Tutar], [AidatBorc], [IsinmaBorc] kullanılabilir.'}
          </Typography.Paragraph>
        )}
      </div>

      <Form.Item
        name="apartmentIds"
        label="Daireler"
        rules={[{ required: true, message: 'En az bir daire seçin.' }]}
        extra={
          <Space wrap style={{ marginTop: 8 }}>
            <Button size="small" onClick={selectDebtors}>
              Borçluları Seç
              {purpose !== 'General' ? ` (${debtors.length})` : ''}
            </Button>
            <Button size="small" onClick={selectAll}>
              Tümünü Seç ({apartments.length})
            </Button>
          </Space>
        }
      >
        <Select
          mode="multiple"
          allowClear
          showSearch
          optionFilterProp="label"
          placeholder="Daire seçin"
          loading={apartmentsQuery.isLoading}
          options={apartments.map((a) => {
            const debtHint =
              purpose === 'Aidat'
                ? a.aidatDebt > 0
                  ? ` · Aidat ${a.aidatDebt.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} TL`
                  : ''
                : purpose === 'Heating'
                  ? a.heatingDebt > 0
                    ? ` · Isınma ${a.heatingDebt.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} TL`
                    : ''
                  : purpose === 'AllDebtors'
                    ? a.currentBalance > 0
                      ? ` · Borç ${a.currentBalance.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} TL`
                      : ''
                    : a.currentBalance !== 0
                      ? ` · ${a.currentBalance.toLocaleString('tr-TR', { minimumFractionDigits: 2 })} TL`
                      : ''
            return {
              value: a.id,
              label: `${a.doorNumber} — ${a.kiraciName || a.ownerName || 'Sakin yok'}${debtHint}`,
            }
          })}
        />
      </Form.Item>

      <Form.Item name="channel" label="Kanal">
        <Radio.Group
          onChange={() => form.setFieldValue('templateId', undefined)}
          options={[
            { value: 'Email', label: 'E-posta' },
            { value: 'Sms', label: 'SMS' },
            { value: 'WhatsApp', label: 'WhatsApp' },
          ]}
        />
      </Form.Item>

      {(purpose === 'Heating' || includePdf) && (
        <>
          <Form.Item
            name="gasInvoiceId"
            label="Gaz faturası (PDF / PdfLink)"
            rules={
              includePdf || purpose === 'Heating'
                ? [{ required: true, message: 'PDF için gaz faturası seçin.' }]
                : undefined
            }
            extra="Bireysel gider bildirimi PDF’i bu faturadan üretilir. SMS’te [PdfLink] doldurulur."
          >
            <Select
              allowClear
              showSearch
              optionFilterProp="label"
              placeholder="Hesaplanmış gaz faturası seçin"
              loading={gasInvoicesQuery.isLoading}
              options={(gasInvoicesQuery.data ?? []).map((g) => ({
                value: g.id,
                label: `${g.invoiceNumber} (${dayjs(g.periodFrom).format('DD.MM.YYYY')} – ${dayjs(g.periodTo).format('DD.MM.YYYY')})`,
              }))}
            />
          </Form.Item>
          <Form.Item name="includePdf" valuePropName="checked">
            <Checkbox disabled={!gasInvoiceId && purpose !== 'Heating'}>
              PDF ekle / güvenli link üret (E-posta ve WhatsApp’ta ek, SMS’te [PdfLink])
            </Checkbox>
          </Form.Item>
        </>
      )}

      <Form.Item name="templateId" label="Şablon">
        <Select
          allowClear
          placeholder="Şablon seçin veya aşağıya metin yazın"
          options={channelTemplates.map((t) => ({ value: t.id, label: t.name }))}
          onClear={() => form.setFieldValue('body', defaultBodies[purpose])}
        />
      </Form.Item>

      {channel === 'Email' && !templateId && (
        <Form.Item name="subject" label="Konu">
          <Input />
        </Form.Item>
      )}

      {!templateId && (
        <Form.Item
          name="body"
          label="Mesaj"
          extra={<PlaceholderGuide onInsert={(token) => insertToken(form, 'body', token, bodyRef.current)} />}
        >
          <Input.TextArea
            rows={4}
            ref={(node) => {
              bodyRef.current = node?.resizableTextArea?.textArea ?? null
            }}
          />
        </Form.Item>
      )}
      {templateId && <PlaceholderGuide />}

      {previewQuery.data && (
        <Alert
          type={previewQuery.data.hasRecipient ? 'info' : 'warning'}
          showIcon
          style={{ marginBottom: 16 }}
          message={`Önizleme — ${previewQuery.data.recipientName}${
            previewQuery.data.recipientAddress ? ` (${previewQuery.data.recipientAddress})` : ''
          }`}
          description={
            <Space direction="vertical" size={4}>
              {previewQuery.data.subject && <Typography.Text strong>{previewQuery.data.subject}</Typography.Text>}
              <Typography.Paragraph style={{ marginBottom: 0, whiteSpace: 'pre-wrap' }}>
                {previewQuery.data.body}
              </Typography.Paragraph>
              {!previewQuery.data.hasRecipient && (
                <Typography.Text type="warning">Bu dairenin seçilen kanal için iletişim bilgisi yok.</Typography.Text>
              )}
            </Space>
          }
        />
      )}
      {(apartmentIds?.length ?? 0) > 1 && (
        <Typography.Paragraph type="secondary">
          Önizleme ilk seçilen daire içindir. Gönderimde her dairenin kendi bakiyesi kullanılır.
        </Typography.Paragraph>
      )}
      <Button type="primary" htmlType="submit" loading={sendMutation.isPending}>
        {purpose === 'Aidat'
          ? 'Aidat Bildirimi Gönder'
          : purpose === 'Heating'
            ? 'Isınma Bildirimi Gönder'
            : purpose === 'AllDebtors'
              ? 'Tüm Borçlulara Gönder'
              : 'Gönder'}
      </Button>
    </Form>
  )
}

function HistoryTab() {
  const logsQuery = useQuery({
    queryKey: ['notification-logs'],
    queryFn: async () => (await apiClient.get<NotificationLogItem[]>('/notifications/logs')).data,
  })

  const columns: ColumnsType<NotificationLogItem> = [
    { title: 'Daire', dataIndex: 'doorNumber', width: 90 },
    {
      title: 'Kanal',
      dataIndex: 'channel',
      width: 110,
      render: (value: NotificationChannel) => channelLabel[value] ?? value,
    },
    { title: 'Alıcı', dataIndex: 'recipientAddress', width: 180 },
    {
      title: 'Mesaj',
      dataIndex: 'message',
      ellipsis: true,
    },
    {
      title: 'Tarih',
      dataIndex: 'sentAt',
      width: 160,
      render: (value: string) => dayjs(value).format('DD.MM.YYYY HH:mm'),
    },
    {
      title: 'Durum',
      dataIndex: 'status',
      width: 140,
      render: (value: NotificationDeliveryStatus) => {
        const tag = statusTag[value]
        return <Tag color={tag?.color}>{tag?.text ?? value}</Tag>
      },
    },
    { title: 'Hata', dataIndex: 'errorMessage', ellipsis: true },
  ]

  return (
    <Table
      rowKey="id"
      loading={logsQuery.isLoading}
      dataSource={logsQuery.data ?? []}
      columns={columns}
      pagination={{ pageSize: 20 }}
      scroll={{ x: 960 }}
    />
  )
}

function TemplatesTab() {
  const queryClient = useQueryClient()
  const [form] = Form.useForm<TemplateForm>()
  const bodyRef = useRef<HTMLTextAreaElement>(null)
  const [editingId, setEditingId] = useState<string | undefined>()
  const channel = (Form.useWatch('channel', form) as NotificationChannel | undefined) ?? 'Sms'

  const templatesQuery = useQuery({
    queryKey: ['notification-templates'],
    queryFn: async () => (await apiClient.get<NotificationTemplateItem[]>('/notifications/templates')).data,
  })

  const saveMutation = useMutation({
    mutationFn: async (values: TemplateForm) =>
      (
        await apiClient.put<NotificationTemplateItem>('/notifications/templates', {
          id: editingId ?? null,
          name: values.name,
          channel: values.channel,
          subject: values.channel === 'Email' ? values.subject ?? null : null,
          body: values.body,
        })
      ).data,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['notification-templates'] })
      message.success('Şablon kaydedildi.')
      setEditingId(undefined)
      form.resetFields()
    },
  })

  const columns: ColumnsType<NotificationTemplateItem> = [
    { title: 'Ad', dataIndex: 'name' },
    {
      title: 'Kanal',
      dataIndex: 'channel',
      width: 120,
      render: (value: NotificationChannel) => channelLabel[value],
    },
    { title: 'Konu', dataIndex: 'subject', ellipsis: true },
    { title: 'Mesaj', dataIndex: 'body', ellipsis: true },
  ]

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <PlaceholderGuide onInsert={(token) => insertToken(form, 'body', token, bodyRef.current)} />
      <Table
        rowKey="id"
        size="small"
        loading={templatesQuery.isLoading}
        dataSource={templatesQuery.data ?? []}
        columns={columns}
        pagination={false}
        onRow={(record) => ({
          onClick: () => {
            setEditingId(record.id)
            form.setFieldsValue({
              id: record.id,
              name: record.name,
              channel: record.channel,
              subject: record.subject ?? undefined,
              body: record.body,
            })
          },
        })}
      />
      <Form
        form={form}
        layout="vertical"
        style={{ maxWidth: 760 }}
        initialValues={{ channel: 'Sms' }}
        onFinish={(values) => saveMutation.mutate(values)}
      >
        <Form.Item name="name" label="Ad" rules={[{ required: true, message: 'Ad zorunludur.' }]}>
          <Input />
        </Form.Item>
        <Form.Item name="channel" label="Kanal" rules={[{ required: true }]}>
          <Radio.Group
            options={[
              { value: 'Email', label: 'E-posta' },
              { value: 'Sms', label: 'SMS' },
              { value: 'WhatsApp', label: 'WhatsApp' },
            ]}
          />
        </Form.Item>
        {channel === 'Email' && (
          <Form.Item name="subject" label="Konu">
            <Input />
          </Form.Item>
        )}
        <Form.Item name="body" label="Mesaj" rules={[{ required: true, message: 'Mesaj zorunludur.' }]}>
          <Input.TextArea
            rows={4}
            ref={(node) => {
              bodyRef.current = node?.resizableTextArea?.textArea ?? null
            }}
          />
        </Form.Item>
        <Space>
          <Button type="primary" htmlType="submit" loading={saveMutation.isPending}>
            {editingId ? 'Güncelle' : 'Ekle'}
          </Button>
          {editingId && (
            <Button
              onClick={() => {
                setEditingId(undefined)
                form.resetFields()
              }}
            >
              Vazgeç
            </Button>
          )}
        </Space>
      </Form>
    </Space>
  )
}

function insertToken(
  form: { getFieldValue: (name: 'body') => string | undefined; setFieldValue: (name: 'body', value: string) => void },
  field: 'body',
  token: string,
  textarea: HTMLTextAreaElement | null,
) {
  const current = form.getFieldValue(field) ?? ''
  if (!textarea) {
    form.setFieldValue(field, `${current}${token}`)
    return
  }

  const start = textarea.selectionStart ?? current.length
  const end = textarea.selectionEnd ?? start
  form.setFieldValue(field, current.slice(0, start) + token + current.slice(end))
  const pos = start + token.length
  requestAnimationFrame(() => {
    textarea.focus()
    textarea.setSelectionRange(pos, pos)
  })
}

function PlaceholderGuide({ onInsert }: { onInsert?: (token: string) => void }) {
  const [open, setOpen] = useState(false)
  const query = useQuery({
    queryKey: ['notification-placeholders'],
    queryFn: async () =>
      (await apiClient.get<NotificationPlaceholderItem[]>('/notifications/placeholders')).data,
  })

  const copy = async (token: string) => {
    try {
      await navigator.clipboard.writeText(token)
      message.success(`${token} kopyalandı.`)
    } catch {
      message.warning(`${token} panoya alınamadı. Satıra tıklayarak mesaja ekleyebilirsiniz.`)
    }
  }

  const columns: ColumnsType<NotificationPlaceholderItem> = [
    {
      title: 'Değişken',
      dataIndex: 'token',
      width: 170,
      render: (token: string) => <Typography.Text code>{token}</Typography.Text>,
    },
    { title: 'Açıklama', dataIndex: 'description' },
    { title: 'Örnek', dataIndex: 'example', width: 150 },
    {
      title: '',
      width: 100,
      render: (_value, row) => (
        <Button
          size="small"
          onClick={(event) => {
            event.stopPropagation()
            void copy(row.token)
          }}
        >
          Kopyala
        </Button>
      ),
    },
  ]

  return (
    <>
      <Button type="link" style={{ paddingInline: 0 }} onClick={() => setOpen(true)}>
        Kullanılabilir Değişkenler
      </Button>
      <Modal
        title="Kullanılabilir Değişkenler"
        open={open}
        onCancel={() => setOpen(false)}
        footer={null}
        width={760}
      >
        <Typography.Paragraph type="secondary">
          Satıra tıklayınca değişken, imlecin olduğu mesaj kutusuna eklenir. Şablon seçiliyken Kopyala düğmesini kullanın.
        </Typography.Paragraph>
        <Table
          rowKey="token"
          size="small"
          loading={query.isLoading}
          dataSource={query.data ?? []}
          columns={columns}
          pagination={false}
          onRow={(row) => ({
            onClick: () => {
              if (onInsert) {
                onInsert(row.token)
                setOpen(false)
                return
              }
              void copy(row.token)
            },
            style: { cursor: 'pointer' },
          })}
        />
      </Modal>
    </>
  )
}
