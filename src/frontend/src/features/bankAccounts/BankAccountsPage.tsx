import { useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { Button, Card, DatePicker, Form, Input, InputNumber, Modal, Popconfirm, Space, Table, Tabs, Tag, message } from "antd"
import { EditOutlined, PlusOutlined } from "@ant-design/icons"
import type { ColumnsType } from "antd/es/table"
import dayjs, { type Dayjs } from "dayjs"
import { apiClient } from "../../api/client"
import { formatDate } from "../../utils/dateUtils"
import type { BankAccountListItem } from "../../types/api"
import { BankTransactionsTab } from "./BankTransactionsTab"

const tl = (value: number) => value.toLocaleString("tr-TR", { style: "currency", currency: "TRY" })

interface BankAccountForm {
  bankName: string
  accountName: string
  accountNumber: string
  iban?: string
  openingBalance: number
  openingDate: Dayjs
}

export function BankAccountsPage() {
  const queryClient = useQueryClient()
  const [form] = Form.useForm<BankAccountForm>()
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<BankAccountListItem | null>(null)

  const accountsQuery = useQuery({
    queryKey: ["bank-accounts"],
    queryFn: async () => (await apiClient.get<BankAccountListItem[]>("/bank-accounts")).data,
  })

  const saveMutation = useMutation({
    mutationFn: async (values: BankAccountForm) => {
      const body = {
        bankName: values.bankName.trim(),
        accountName: values.accountName.trim(),
        accountNumber: values.accountNumber.trim(),
        iban: values.iban?.trim() || null,
        openingBalance: values.openingBalance,
        openingDate: values.openingDate.format("YYYY-MM-DD"),
      }
      if (editing) {
        await apiClient.put(`/bank-accounts/${editing.id}`, body)
        return
      }
      await apiClient.post("/bank-accounts", body)
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["bank-accounts"] })
      setOpen(false)
      setEditing(null)
      form.resetFields()
      message.success(editing ? "Hesap güncellendi." : "Hesap eklendi.")
    },
  })

  const activeMutation = useMutation({
    mutationFn: async (row: BankAccountListItem) =>
      apiClient.put(`/bank-accounts/${row.id}/active`, { isActive: !row.isActive }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["bank-accounts"] })
      message.success("Hesap durumu güncellendi.")
    },
  })

  const deleteMutation = useMutation({
    mutationFn: async (id: string) => apiClient.delete(`/bank-accounts/${id}`),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ["bank-accounts"] })
      message.success("Hesap silindi.")
    },
  })

  const openCreate = () => {
    setEditing(null)
    form.resetFields()
    form.setFieldsValue({ openingBalance: 0, openingDate: dayjs() })
    setOpen(true)
  }

  const openEdit = (row: BankAccountListItem) => {
    setEditing(row)
    form.setFieldsValue({
      bankName: row.bankName,
      accountName: row.accountName,
      accountNumber: row.accountNumber,
      iban: row.iban ?? undefined,
      openingBalance: row.openingBalance,
      openingDate: dayjs(row.openingDate),
    })
    setOpen(true)
  }

  const columns: ColumnsType<BankAccountListItem> = [
    { title: "Banka", dataIndex: "bankName", ellipsis: true },
    { title: "Hesap adı", dataIndex: "accountName", ellipsis: true },
    { title: "Hesap no", dataIndex: "accountNumber", ellipsis: true },
    { title: "Devir", dataIndex: "openingBalance", align: "right", width: 120, render: (value: number) => tl(value) },
    { title: "Giren", dataIndex: "moneyIn", align: "right", width: 120, render: (value: number) => tl(value) },
    { title: "Çıkan", dataIndex: "moneyOut", align: "right", width: 120, render: (value: number) => tl(value) },
    { title: "Güncel bakiye", dataIndex: "currentBalance", align: "right", width: 140, render: (value: number) => tl(value) },
    {
      title: "IBAN",
      dataIndex: "iban",
      ellipsis: true,
      render: (value: string | null) => value || "—",
    },
    {
      title: "Devir tarihi",
      dataIndex: "openingDate",
      width: 120,
      render: (value: string) => formatDate(value),
    },
    {
      title: "Durum",
      dataIndex: "isActive",
      width: 90,
      render: (value: boolean) => (value ? <Tag color="green">Aktif</Tag> : <Tag>Pasif</Tag>),
    },
    {
      title: "",
      width: 280,
      render: (_: unknown, row) => (
        <Space>
          <Button size="small" icon={<EditOutlined />} onClick={() => openEdit(row)}>Düzenle</Button>
          <Button size="small" onClick={() => activeMutation.mutate(row)}>
            {row.isActive ? "Pasife al" : "Aktifleştir"}
          </Button>
          <Popconfirm title="Bu hesap silinsin mi?" okText="Sil" cancelText="Vazgeç" onConfirm={() => deleteMutation.mutate(row.id)}>
            <Button size="small" danger>Sil</Button>
          </Popconfirm>
        </Space>
      ),
    },
  ]

  return (
    <Card title="Banka Hesapları">
    <Tabs
      items={[
        {
          key: "accounts",
          label: "Hesaplar",
          children: (
    <Space direction="vertical" size={16} style={{ width: "100%" }}>
      <Button type="primary" icon={<PlusOutlined />} onClick={openCreate}>Hesap Ekle</Button>
      <Table
        rowKey="id"
        size="small"
        loading={accountsQuery.isLoading}
        dataSource={accountsQuery.data ?? []}
        columns={columns}
        pagination={{ pageSize: 12 }}
        locale={{ emptyText: "Banka hesabı yok." }}
      />
      <Modal
        title={editing ? "Hesabı düzenle" : "Hesap ekle"}
        open={open}
        confirmLoading={saveMutation.isPending}
        okText="Kaydet"
        cancelText="Vazgeç"
        onCancel={() => { setOpen(false); setEditing(null); form.resetFields() }}
        onOk={() => form.validateFields().then((values) => saveMutation.mutate(values))}
      >
        <Form form={form} layout="vertical">
          <Form.Item name="bankName" label="Banka adı" rules={[{ required: true, message: "Banka adı zorunludur." }]}>
            <Input maxLength={100} />
          </Form.Item>
          <Form.Item name="accountName" label="Hesap adı" rules={[{ required: true, message: "Hesap adı zorunludur." }]}>
            <Input maxLength={100} placeholder="Vadesiz, POS" />
          </Form.Item>
          <Form.Item name="accountNumber" label="Hesap numarası" rules={[{ required: true, message: "Hesap numarası zorunludur." }]}>
            <Input maxLength={34} />
          </Form.Item>
          <Form.Item name="iban" label="IBAN">
            <Input maxLength={32} placeholder="TR ile başlar, boş kalabilir" />
          </Form.Item>
          <Form.Item name="openingDate" label="Devir tarihi" rules={[{ required: true, message: "Devir tarihi zorunludur." }]}>
            <DatePicker format="DD.MM.YYYY" style={{ width: "100%" }} disabledDate={(date) => date.isAfter(dayjs(), "day")} />
          </Form.Item>
          <Form.Item name="openingBalance" label="Devir bakiyesi" rules={[{ required: true, message: "Devir bakiyesi zorunludur." }]}>
            <InputNumber min={0} precision={2} decimalSeparator="," addonAfter="TL" style={{ width: "100%" }} />
          </Form.Item>
        </Form>
      </Modal>
    </Space>
          ),
        },
        {
          key: "movements",
          label: "Hareketler ve Virman",
          children: <BankTransactionsTab />,
        },
      ]}
    />
    </Card>
  )
}