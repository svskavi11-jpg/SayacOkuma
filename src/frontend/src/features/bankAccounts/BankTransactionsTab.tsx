import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { Button, Card, Col, DatePicker, Form, Input, InputNumber, Popconfirm, Row, Select, Space, Table, message } from "antd"
import type { ColumnsType } from "antd/es/table"
import dayjs, { type Dayjs } from "dayjs"
import { apiClient } from "../../api/client"
import { useAuth } from "../auth/AuthContext"
import { formatDate } from "../../utils/dateUtils"
import type { BankTransactionListItem, BankTransactionType } from "../../types/api"
import { BankAccountSelect } from "./BankAccountSelect"

const tl = (value: number) => value.toLocaleString("tr-TR", { style: "currency", currency: "TRY" })

const typeLabel: Record<BankTransactionType, string> = {
  BankInterest: "Faiz",
  BankFee: "Masraf",
  TransferOut: "Virman çıkış",
  TransferIn: "Virman giriş",
}

const incomingTypes = new Set<BankTransactionType>(["BankInterest", "TransferIn"])

interface TransferForm {
  sourceBankAccountId: string
  targetBankAccountId: string
  amount: number
  transactionDate: Dayjs
  description?: string
}

interface EntryForm {
  bankAccountId: string
  type: "BankInterest" | "BankFee"
  amount: number
  transactionDate: Dayjs
  description?: string
}

export function BankTransactionsTab() {
  const { hasPermission } = useAuth()
  const canManage = hasPermission("Bank.Manage")
  const queryClient = useQueryClient()
  const [transferForm] = Form.useForm<TransferForm>()
  const [entryForm] = Form.useForm<EntryForm>()
  const sourceId = Form.useWatch("sourceBankAccountId", transferForm)
  const targetId = Form.useWatch("targetBankAccountId", transferForm)

  const transactionsQuery = useQuery({
    queryKey: ["bank-transactions"],
    queryFn: async () => (await apiClient.get<BankTransactionListItem[]>("/bank-accounts/transactions")).data,
  })

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ["bank-transactions"] })
    void queryClient.invalidateQueries({ queryKey: ["bank-accounts"] })
    void queryClient.invalidateQueries({ queryKey: ["dashboard-banks"] })
  }

  const transferMutation = useMutation({
    mutationFn: async (values: TransferForm) =>
      apiClient.post("/bank-accounts/transfers", {
        sourceBankAccountId: values.sourceBankAccountId,
        targetBankAccountId: values.targetBankAccountId,
        amount: values.amount,
        transactionDate: values.transactionDate.format("YYYY-MM-DD"),
        description: values.description?.trim() || null,
      }),
    onSuccess: () => {
      transferForm.resetFields()
      transferForm.setFieldValue("transactionDate", dayjs())
      refresh()
      message.success("Virman kaydedildi.")
    },
  })

  const entryMutation = useMutation({
    mutationFn: async (values: EntryForm) =>
      apiClient.post("/bank-accounts/transactions", {
        bankAccountId: values.bankAccountId,
        type: values.type,
        amount: values.amount,
        transactionDate: values.transactionDate.format("YYYY-MM-DD"),
        description: values.description?.trim() || null,
      }),
    onSuccess: () => {
      entryForm.resetFields()
      entryForm.setFieldValue("transactionDate", dayjs())
      refresh()
      message.success("Hareket kaydedildi.")
    },
  })

  const deleteMutation = useMutation({
    mutationFn: async (id: string) => apiClient.delete(`/bank-accounts/transactions/${id}`),
    onSuccess: () => {
      refresh()
      message.success("Hareket silindi.")
    },
  })

  const columns: ColumnsType<BankTransactionListItem> = [
    { title: "Tarih", dataIndex: "transactionDate", width: 120, render: (value: string) => formatDate(value) },
    { title: "Hesap", dataIndex: "bankLabel", ellipsis: true },
    { title: "Tür", dataIndex: "type", width: 130, render: (value: BankTransactionType) => typeLabel[value] },
    {
      title: "Tutar",
      dataIndex: "amount",
      align: "right",
      width: 140,
      render: (value: number, row) => (
        <span style={{ color: incomingTypes.has(row.type) ? "#389e0d" : "#cf1322" }}>
          {incomingTypes.has(row.type) ? "+" : "−"}
          {tl(value)}
        </span>
      ),
    },
    { title: "Karşı hesap", dataIndex: "targetLabel", ellipsis: true, render: (value: string | null) => value || "—" },
    { title: "Açıklama", dataIndex: "description", ellipsis: true, render: (value: string | null) => value || "—" },
    {
      title: "",
      fixed: 'right',
      width: 90,
      render: (_: unknown, row) => (
        canManage ? <Popconfirm
          title={row.transferGroupId ? "Bu virmanın iki bacağı da silinsin mi?" : "Bu hareket silinsin mi?"}
          okText="Sil"
          cancelText="Vazgeç"
          onConfirm={() => deleteMutation.mutate(row.id)}
        >
          <Button size="small" danger>Sil</Button>
        </Popconfirm> : null
      ),
    },
  ]

  return (
    <Space direction="vertical" size={16} style={{ width: "100%" }}>
      {canManage && <Row gutter={[16, 16]}>
        <Col xs={24} lg={12}>
          <Card size="small" title="Virman">
            <Form
              form={transferForm}
              layout="vertical"
              initialValues={{ transactionDate: dayjs() }}
              onFinish={(values) => transferMutation.mutate(values)}
            >
              <Form.Item name="sourceBankAccountId" label="Kaynak hesap" rules={[{ required: true, message: "Kaynak hesap zorunludur." }]}>
                <BankAccountSelect allowClear={false} excludeId={targetId} placeholder="Paranın çıkacağı hesap" style={{ width: "100%" }} />
              </Form.Item>
              <Form.Item name="targetBankAccountId" label="Hedef hesap" rules={[{ required: true, message: "Hedef hesap zorunludur." }]}>
                <BankAccountSelect allowClear={false} excludeId={sourceId} placeholder="Paranın gireceği hesap" style={{ width: "100%" }} />
              </Form.Item>
              <Form.Item name="amount" label="Tutar" rules={[{ required: true, message: "Tutar zorunludur." }]}>
                <InputNumber min={0.01} precision={2} decimalSeparator="," addonAfter="TL" style={{ width: "100%" }} />
              </Form.Item>
              <Form.Item name="transactionDate" label="Tarih" rules={[{ required: true, message: "Tarih zorunludur." }]}>
                <DatePicker format="DD.MM.YYYY" style={{ width: "100%" }} disabledDate={(date) => date.isAfter(dayjs(), "day")} />
              </Form.Item>
              <Form.Item name="description" label="Açıklama">
                <Input maxLength={200} />
              </Form.Item>
              <Button type="primary" htmlType="submit" loading={transferMutation.isPending}>Virmanı kaydet</Button>
            </Form>
          </Card>
        </Col>
        <Col xs={24} lg={12}>
          <Card size="small" title="Faiz / Masraf">
            <Form
              form={entryForm}
              layout="vertical"
              initialValues={{ transactionDate: dayjs(), type: "BankInterest" }}
              onFinish={(values) => entryMutation.mutate(values)}
            >
              <Form.Item name="bankAccountId" label="Hesap" rules={[{ required: true, message: "Hesap zorunludur." }]}>
                <BankAccountSelect allowClear={false} placeholder="Hesap seçin" style={{ width: "100%" }} />
              </Form.Item>
              <Form.Item name="type" label="Tür" rules={[{ required: true, message: "Tür zorunludur." }]}>
                <Select
                  options={[
                    { value: "BankInterest", label: "Faiz" },
                    { value: "BankFee", label: "Masraf" },
                  ]}
                />
              </Form.Item>
              <Form.Item name="amount" label="Tutar" rules={[{ required: true, message: "Tutar zorunludur." }]}>
                <InputNumber min={0.01} precision={2} decimalSeparator="," addonAfter="TL" style={{ width: "100%" }} />
              </Form.Item>
              <Form.Item name="transactionDate" label="Tarih" rules={[{ required: true, message: "Tarih zorunludur." }]}>
                <DatePicker format="DD.MM.YYYY" style={{ width: "100%" }} disabledDate={(date) => date.isAfter(dayjs(), "day")} />
              </Form.Item>
              <Form.Item name="description" label="Açıklama">
                <Input maxLength={200} />
              </Form.Item>
              <Button type="primary" htmlType="submit" loading={entryMutation.isPending}>Hareketi kaydet</Button>
            </Form>
          </Card>
        </Col>
      </Row>}
      <Table
        scroll={{ x: 'max-content' }}
        rowKey="id"
        size="small"
        loading={transactionsQuery.isLoading}
        dataSource={transactionsQuery.data ?? []}
        columns={columns}
        pagination={{ pageSize: 12 }}
        locale={{ emptyText: "Banka hareketi yok." }}
      />
    </Space>
  )
}
