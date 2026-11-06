import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Button, DatePicker, Form, Input, InputNumber, Modal, Popconfirm, Select, Space, Table, message } from 'antd'
import { DeleteOutlined, EditOutlined, PlusOutlined } from '@ant-design/icons'
import { BankAccountSelect } from '../bankAccounts/BankAccountSelect'
import type { ColumnsType } from 'antd/es/table'
import dayjs, { type Dayjs } from 'dayjs'
import { apiClient } from '../../api/client'
import { formatDate } from '../../utils/dateUtils'
import type { AidatInvoiceListItem, BudgetRealizationReport, CreateExpenseResult, ExpenseListItem } from '../../types/api'

const tl = (v: number) => v.toLocaleString('tr-TR', { style: 'currency', currency: 'TRY' })

interface ExpenseForm {
  expenseDate: Dayjs
  amount: number
  description?: string
  expenseType?: string
  budgetItemId?: string
  documentNumber?: string
  fundAidatInvoiceId?: string
  bankAccountId?: string
}

/** Gider girisi. Belge no, hizli kalem ve fon baglantisi bu formdadir. */
export function ExpensesTab({
  year,
  budgetEnabled,
  locked,
}: {
  year: number
  budgetEnabled: boolean
  locked: boolean
}) {
  const queryClient = useQueryClient()
  const [form] = Form.useForm<ExpenseForm>()
  const [itemForm] = Form.useForm<{ category: string; estimatedAmount: number }>()
  const [itemOpen, setItemOpen] = useState(false)
  const [editForm] = Form.useForm<ExpenseForm>()
  const [editing, setEditing] = useState<ExpenseListItem | null>(null)
  const futureYear = year > dayjs().year()
  const blocked = futureYear || locked

  const expensesQuery = useQuery({
    queryKey: ['expenses', year],
    queryFn: async () => (await apiClient.get<ExpenseListItem[]>('/budget/expenses', { params: { year } })).data,
  })

  const itemsQuery = useQuery({
    queryKey: ['budget-items', year],
    enabled: budgetEnabled,
    queryFn: async () => (await apiClient.get<BudgetRealizationReport>('/budget/items', { params: { year } })).data,
  })

  const fundsQuery = useQuery({
    queryKey: ['aidat-invoices'],
    queryFn: async () => (await apiClient.get<AidatInvoiceListItem[]>('/aidat/invoices')).data,
  })

  const funds = (fundsQuery.data ?? []).filter((row) => row.chargeType === 'OzelToplama')

  const saveMutation = useMutation({
    mutationFn: async (values: ExpenseForm) =>
      (
        await apiClient.post<CreateExpenseResult>('/budget/expenses', {
          expenseDate: values.expenseDate.format('YYYY-MM-DD'),
          amount: values.amount,
          description: values.description?.trim() || null,
          expenseType: budgetEnabled ? null : values.expenseType?.trim() || null,
          budgetItemId: budgetEnabled ? values.budgetItemId : null,
          documentNumber: values.documentNumber?.trim() || null,
          fundAidatInvoiceId: values.fundAidatInvoiceId || null,
          bankAccountId: values.bankAccountId || null,
        })
      ).data,
    onSuccess: (result) => {
      void queryClient.invalidateQueries({ queryKey: ['expenses'] })
      void queryClient.invalidateQueries({ queryKey: ['budget-items'] })
      void queryClient.invalidateQueries({ queryKey: ['budget-realization'] })
      void queryClient.invalidateQueries({ queryKey: ['aidat-invoices'] })
      form.resetFields()
      if (result.budgetWarning && result.warningMessage) {
        message.warning(result.warningMessage)
      } else {
        message.success('Gider kaydedildi.')
      }
    },
  })

  const updateMutation = useMutation({
    mutationFn: async (values: ExpenseForm) => {
      if (!editing) return
      await apiClient.put(`/budget/expenses/${editing.id}`, {
        expenseDate: values.expenseDate.format('YYYY-MM-DD'),
        amount: values.amount,
        description: values.description?.trim() || null,
        expenseType: budgetEnabled ? null : values.expenseType?.trim() || null,
        budgetItemId: budgetEnabled ? values.budgetItemId : null,
        documentNumber: values.documentNumber?.trim() || null,
        fundAidatInvoiceId: values.fundAidatInvoiceId || null,
        bankAccountId: values.bankAccountId || null,
      })
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['expenses'] })
      void queryClient.invalidateQueries({ queryKey: ['budget-items'] })
      void queryClient.invalidateQueries({ queryKey: ['budget-realization'] })
      setEditing(null)
      editForm.resetFields()
      message.success('Gider güncellendi.')
    },
  })

  const deleteMutation = useMutation({
    mutationFn: async (id: string) => apiClient.delete(`/budget/expenses/${id}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['expenses'] })
      void queryClient.invalidateQueries({ queryKey: ['budget-items'] })
      void queryClient.invalidateQueries({ queryKey: ['budget-realization'] })
      void queryClient.invalidateQueries({ queryKey: ['aidat-invoices'] })
      message.success('Gider silindi.')
    },
  })

  const createItemMutation = useMutation({
    mutationFn: async (values: { category: string; estimatedAmount: number }) =>
      (await apiClient.post<string>('/budget/items', { year, category: values.category.trim(), estimatedAmount: values.estimatedAmount })).data,
    onSuccess: (id) => {
      void queryClient.invalidateQueries({ queryKey: ['budget-items'] })
      void queryClient.invalidateQueries({ queryKey: ['budget-realization'] })
      form.setFieldValue('budgetItemId', id)
      setItemOpen(false)
      itemForm.resetFields()
      message.success('Bütçe kalemi eklendi.')
    },
  })

  const columns: ColumnsType<ExpenseListItem> = [
    { title: 'Tarih', dataIndex: 'expenseDate', width: 120, render: (v: string) => formatDate(v) },
    { title: 'Belge No', dataIndex: 'documentNumber', width: 140, render: (v: string | null) => v || '—' },
    { title: budgetEnabled ? 'Bütçe Kalemi' : 'Gider Türü', dataIndex: 'displayType' },
    { title: 'Fon', dataIndex: 'fundTitle', width: 160, render: (v: string | null) => v || '—' },
    { title: 'Açıklama', dataIndex: 'description', render: (v: string | null) => v || '—' },
    { title: 'Banka', dataIndex: 'bankAccountLabel', width: 180, render: (v: string | null) => v || '—' },
    { title: 'Tutar', dataIndex: 'amount', align: 'right', width: 140, render: (v: number) => tl(v) },
    {
      title: '',
      width: 70,
      render: (_: unknown, row) => (
        <Space>
        <Button
          size="small"
          icon={<EditOutlined />}
          disabled={locked}
          onClick={() => {
            setEditing(row)
            editForm.setFieldsValue({
              expenseDate: dayjs(row.expenseDate),
              amount: row.amount,
              description: row.description ?? undefined,
              expenseType: budgetEnabled ? undefined : row.displayType,
              budgetItemId: row.budgetItemId ?? undefined,
              documentNumber: row.documentNumber ?? undefined,
              fundAidatInvoiceId: row.fundAidatInvoiceId ?? undefined,
              bankAccountId: row.bankAccountId ?? undefined,
            })
          }}
        />
        <Popconfirm
          title="Bu gider silinsin mi?"
          okText="Sil"
          cancelText="Vazgeç"
          onConfirm={() => deleteMutation.mutate(row.id)}
        >
          <Button size="small" danger icon={<DeleteOutlined />} disabled={locked} />
        </Popconfirm>
        </Space>
      ),
    },
  ]

  return (
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      {futureYear && (
        <Alert type="warning" showIcon message="Gelecek bir yıla gider girilemez. Tarih bugünden ileri olamaz." />
      )}
      {locked && <Alert type="warning" showIcon message="Bu bütçe yılı kilitli. Yeni gider eklenemez." />}
      {budgetEnabled && (itemsQuery.data?.rows.length ?? 0) === 0 && !itemsQuery.isLoading && (
        <Alert type="info" showIcon message="Önce bir bütçe kalemi tanımlayın. Gider formundaki yeni kalem düğmesini kullanabilirsiniz." />
      )}
      <Form form={form} layout="inline" disabled={blocked} onFinish={(values) => saveMutation.mutate(values)}>
        <Form.Item name="expenseDate" rules={[{ required: true, message: 'Tarih zorunludur.' }]}>
          <DatePicker
            format="DD.MM.YYYY"
            placeholder="Tarih"
            disabledDate={(d) => d.year() !== year || d.isAfter(dayjs(), 'day')}
          />
        </Form.Item>
        <Form.Item name="amount" rules={[{ required: true, message: 'Tutar zorunludur.' }]}>
          <InputNumber min={0.01} precision={2} decimalSeparator="," addonAfter="TL" placeholder="Tutar" style={{ width: 160 }} />
        </Form.Item>
        {budgetEnabled ? (
          <Form.Item>
            <Space.Compact>
              <Form.Item name="budgetItemId" noStyle rules={[{ required: true, message: 'Bütçe kalemi zorunludur.' }]}>
                <Select
                  style={{ minWidth: 220 }}
                  placeholder="Bütçe kalemi"
                  loading={itemsQuery.isLoading}
                  options={(itemsQuery.data?.rows ?? []).map((r) => ({ value: r.budgetItemId, label: r.category }))}
                />
              </Form.Item>
              <Button icon={<PlusOutlined />} onClick={() => setItemOpen(true)} disabled={blocked}>
                Yeni Bütçe Kalemi
              </Button>
            </Space.Compact>
          </Form.Item>
        ) : (
          <Form.Item name="expenseType" rules={[{ required: true, message: 'Gider türü zorunludur.' }]}>
            <Input maxLength={100} placeholder="Gider türü" style={{ width: 200 }} />
          </Form.Item>
        )}
        <Form.Item name="documentNumber">
          <Input maxLength={50} placeholder="Fatura / makbuz no" style={{ width: 180 }} />
        </Form.Item>
        <Form.Item name="fundAidatInvoiceId">
          <Select
            allowClear
            style={{ minWidth: 220 }}
            placeholder="Fondan düş (isteğe bağlı)"
            loading={fundsQuery.isLoading}
            options={funds.map((fund) => ({
              value: fund.id,
              label: fund.title || 'Özel toplama',
            }))}
          />
        </Form.Item>
        <Form.Item name="bankAccountId">
          <BankAccountSelect />
        </Form.Item>
        <Form.Item name="description">
          <Input maxLength={500} placeholder="Açıklama" style={{ width: 220 }} />
        </Form.Item>
        <Form.Item>
          <Button type="primary" htmlType="submit" loading={saveMutation.isPending}>
            Kaydet
          </Button>
        </Form.Item>
      </Form>
      <Table
        rowKey="id"
        size="small"
        loading={expensesQuery.isLoading}
        dataSource={expensesQuery.data ?? []}
        columns={columns}
        pagination={{ pageSize: 12 }}
        locale={{ emptyText: `${year} yılı için gider yok.` }}
      />
      <Modal
        title={`${year} — Yeni Bütçe Kalemi`}
        open={itemOpen}
        confirmLoading={createItemMutation.isPending}
        okText="Ekle"
        cancelText="Vazgeç"
        onCancel={() => setItemOpen(false)}
        onOk={() => itemForm.validateFields().then((values) => createItemMutation.mutate(values))}
      >
        <Form form={itemForm} layout="vertical">
          <Form.Item name="category" label="Kategori" rules={[{ required: true, message: 'Kategori zorunludur.' }]}>
            <Input maxLength={100} />
          </Form.Item>
          <Form.Item name="estimatedAmount" label="Tahmini tutar" rules={[{ required: true, message: 'Tutar zorunludur.' }]}>
            <InputNumber min={0.01} precision={2} decimalSeparator="," addonAfter="TL" style={{ width: '100%' }} />
          </Form.Item>
        </Form>
      </Modal>
      <Modal
        title="Gideri düzenle"
        open={editing !== null}
        confirmLoading={updateMutation.isPending}
        okText="Kaydet"
        cancelText="Vazgeç"
        onCancel={() => { setEditing(null); editForm.resetFields() }}
        onOk={() => editForm.validateFields().then((values) => updateMutation.mutate(values))}
      >
        <Form form={editForm} layout="vertical">
          <Form.Item name="expenseDate" label="Tarih" rules={[{ required: true, message: 'Tarih zorunludur.' }]}>
            <DatePicker format="DD.MM.YYYY" style={{ width: '100%' }} disabledDate={(d) => d.year() !== year || d.isAfter(dayjs(), 'day')} />
          </Form.Item>
          <Form.Item name="amount" label="Tutar" rules={[{ required: true, message: 'Tutar zorunludur.' }]}>
            <InputNumber min={0.01} precision={2} decimalSeparator="," addonAfter="TL" style={{ width: '100%' }} />
          </Form.Item>
          {budgetEnabled ? (
            <Form.Item name="budgetItemId" label="Bütçe kalemi" rules={[{ required: true, message: 'Bütçe kalemi zorunludur.' }]}>
              <Select
                options={(itemsQuery.data?.rows ?? []).map((r) => ({ value: r.budgetItemId, label: r.category }))}
              />
            </Form.Item>
          ) : (
            <Form.Item name="expenseType" label="Gider türü" rules={[{ required: true, message: 'Gider türü zorunludur.' }]}>
              <Input maxLength={100} />
            </Form.Item>
          )}
          <Form.Item name="documentNumber" label="Belge no">
            <Input maxLength={50} />
          </Form.Item>
          <Form.Item name="fundAidatInvoiceId" label="Fon">
            <Select
              allowClear
              options={funds.map((fund) => ({ value: fund.id, label: fund.title || 'Özel toplama' }))}
            />
          </Form.Item>
          <Form.Item name="bankAccountId" label="Banka hesabı">
            <BankAccountSelect includeId={editing?.bankAccountId} style={{ width: '100%' }} />
          </Form.Item>
          <Form.Item name="description" label="Açıklama">
            <Input maxLength={500} />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
  )
}
