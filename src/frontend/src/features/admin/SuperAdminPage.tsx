import { AppModal } from '../../components/AppModal'
import { useState } from 'react'
import { Navigate } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Button, Form, Input, Select, Space, Table, Tabs, Tag, message } from 'antd'
import { PlusOutlined, UserAddOutlined } from '@ant-design/icons'
import type { ColumnsType } from 'antd/es/table'
import { apiClient } from '../../api/client'
import { useAuth } from '../auth/AuthContext'
import { MigrationWizard } from './MigrationWizard'

interface TenantRow {
  id: string
  name: string
  address: string
  isActive: boolean
  userCount: number
  createdOn: string
}

interface TenantForm {
  name: string
  address?: string
  adminFullName: string
  adminEmail: string
  adminPassword: string
}

interface EditForm {
  name: string
  address?: string
}

interface UserForm {
  fullName: string
  email: string
  password: string
  roleId: string
}

interface AssignableRole {
  id: string
  name: string
}

const passwordRules = [
  { required: true, message: 'Şifre zorunludur.' },
  { min: 8, message: 'Şifre en az 8 karakter olmalıdır.' },
  { pattern: /[A-Z]/, message: 'Şifre en az bir büyük harf içermelidir.' },
  { pattern: /[a-z]/, message: 'Şifre en az bir küçük harf içermelidir.' },
  { pattern: /[0-9]/, message: 'Şifre en az bir rakam içermelidir.' },
  { pattern: /[^a-zA-Z0-9]/, message: 'Şifre en az bir özel karakter içermelidir.' },
]

export function SuperAdminPage() {
  const { session, enterTenant } = useAuth()
  const queryClient = useQueryClient()
  const [createForm] = Form.useForm<TenantForm>()
  const [editForm] = Form.useForm<EditForm>()
  const [userForm] = Form.useForm<UserForm>()
  const [creating, setCreating] = useState(false)
  const [editing, setEditing] = useState<TenantRow | null>(null)
  const [userTarget, setUserTarget] = useState<TenantRow | null>(null)

  const assignableRoles = useQuery({
    queryKey: ['assignable-roles', userTarget?.id],
    enabled: userTarget !== null,
    queryFn: async () => (await apiClient.get<AssignableRole[]>(`/admin/tenants/${userTarget?.id}/roles`)).data,
  })

  const tenants = useQuery({
    queryKey: ['admin-tenants'],
    queryFn: async () => (await apiClient.get<TenantRow[]>('/admin/tenants')).data,
    enabled: session?.role === 'SuperAdmin',
  })

  const refresh = () => queryClient.invalidateQueries({ queryKey: ['admin-tenants'] })

  const createMutation = useMutation({
    mutationFn: (values: TenantForm) => apiClient.post('/admin/tenants', {
      name: values.name.trim(),
      address: values.address?.trim() || null,
      adminFullName: values.adminFullName.trim(),
      adminEmail: values.adminEmail.trim(),
      adminPassword: values.adminPassword,
    }),
    onSuccess: () => {
      message.success('Müşteri sitesi oluşturuldu.')
      setCreating(false)
      createForm.resetFields()
      refresh()
    },
  })

  const updateMutation = useMutation({
    mutationFn: (values: EditForm) => apiClient.put(`/admin/tenants/${editing?.id}`, {
      name: values.name.trim(),
      address: values.address?.trim() || null,
    }),
    onSuccess: () => {
      message.success('Site güncellendi.')
      setEditing(null)
      refresh()
    },
  })

  const statusMutation = useMutation({
    mutationFn: (row: TenantRow) => apiClient.post(`/admin/tenants/${row.id}/status`, { isActive: !row.isActive }),
    onSuccess: () => {
      message.success('Site durumu güncellendi.')
      refresh()
    },
  })

  const userMutation = useMutation({
    mutationFn: (values: UserForm) => apiClient.post(`/admin/tenants/${userTarget?.id}/users`, {
      fullName: values.fullName.trim(),
      email: values.email.trim(),
      password: values.password,
      roleId: values.roleId,
    }),
    onSuccess: () => {
      message.success('Kullanıcı eklendi.')
      setUserTarget(null)
      userForm.resetFields()
      refresh()
    },
  })

  if (session?.role !== 'SuperAdmin') {
    return <Navigate to="/" replace />
  }

  const columns: ColumnsType<TenantRow> = [
    { title: 'Site', dataIndex: 'name' },
    { title: 'Adres', dataIndex: 'address', render: (value: string) => value || '—' },
    { title: 'Kullanıcı', dataIndex: 'userCount', width: 110 },
    {
      title: 'Durum',
      dataIndex: 'isActive',
      width: 110,
      render: (active: boolean) => active ? <Tag color="green">Aktif</Tag> : <Tag>Pasif</Tag>,
    },
    {
      title: '',
      fixed: 'right',
      key: 'actions',
      width: 360,
      render: (_, row) => (
        <Space wrap>
          <Button
            type="primary"
            size="small"
            disabled={!row.isActive}
            onClick={() => {
              if (!enterTenant(row.id, row.name)) {
                message.error('Site kimliği okunamadı.')
                return
              }
              window.location.reload()
            }}
          >
            Siteye Gir
          </Button>
          <Button size="small" onClick={() => { setEditing(row); editForm.setFieldsValue({ name: row.name, address: row.address }) }}>
            Düzenle
          </Button>
          <Button size="small" icon={<UserAddOutlined />} onClick={() => { setUserTarget(row); userForm.resetFields() }}>
            Kullanıcı
          </Button>
          <Button size="small" danger={row.isActive} onClick={() => statusMutation.mutate(row)}>
            {row.isActive ? 'Pasife al' : 'Aktifleştir'}
          </Button>
        </Space>
      ),
    },
  ]

  return (
    <>
      <Tabs
        items={[
          {
            key: 'tenants',
            label: 'Müşteriler',
            children: (
              <>
                <Space style={{ marginBottom: 16, width: '100%', justifyContent: 'space-between' }}>
                  <span />
                  <Button type="primary" icon={<PlusOutlined />} onClick={() => setCreating(true)}>Yeni müşteri</Button>
                </Space>
                <Table
                  scroll={{ x: 'max-content' }}
                  rowKey="id"
                  loading={tenants.isLoading}
                  dataSource={tenants.data ?? []}
                  columns={columns}
                  pagination={false}
                />
              </>
            ),
          },
          {
            key: 'migration',
            label: 'Sunucu Taşıma',
            children: <MigrationWizard />,
          },
        ]}
      />

      <AppModal
        title="Yeni müşteri"
        open={creating}
        onCancel={() => setCreating(false)}
        onOk={() => createForm.submit()}
        confirmLoading={createMutation.isPending}
        okText="Oluştur"
        cancelText="Vazgeç"
        destroyOnClose
      >
        <Form form={createForm} layout="vertical" onFinish={(values) => createMutation.mutate(values)}>
          <Form.Item name="name" label="Site adı" rules={[{ required: true, message: 'Site adı zorunludur.' }, { max: 200 }]}>
            <Input />
          </Form.Item>
          <Form.Item name="address" label="Adres" rules={[{ max: 300 }]}>
            <Input.TextArea rows={2} />
          </Form.Item>
          <Form.Item name="adminFullName" label="Yönetici adı" rules={[{ required: true, message: 'Yönetici adı zorunludur.' }]}>
            <Input />
          </Form.Item>
          <Form.Item name="adminEmail" label="Yönetici e-posta" rules={[{ required: true, message: 'E-posta zorunludur.' }, { type: 'email', message: 'Geçerli bir e-posta girin.' }]}>
            <Input />
          </Form.Item>
          <Form.Item name="adminPassword" label="Yönetici şifresi" rules={passwordRules}>
            <Input.Password />
          </Form.Item>
        </Form>
      </AppModal>

      <AppModal
        title="Siteyi düzenle"
        open={editing !== null}
        onCancel={() => setEditing(null)}
        onOk={() => editForm.submit()}
        confirmLoading={updateMutation.isPending}
        okText="Kaydet"
        cancelText="Vazgeç"
        destroyOnClose
      >
        <Form form={editForm} layout="vertical" onFinish={(values) => updateMutation.mutate(values)}>
          <Form.Item name="name" label="Site adı" rules={[{ required: true, message: 'Site adı zorunludur.' }, { max: 200 }]}>
            <Input />
          </Form.Item>
          <Form.Item name="address" label="Adres" rules={[{ max: 300 }]}>
            <Input.TextArea rows={2} />
          </Form.Item>
        </Form>
      </AppModal>

      <AppModal
        title={userTarget ? `${userTarget.name} — kullanıcı ekle` : 'Kullanıcı ekle'}
        open={userTarget !== null}
        onCancel={() => setUserTarget(null)}
        onOk={() => userForm.submit()}
        confirmLoading={userMutation.isPending}
        okText="Ekle"
        cancelText="Vazgeç"
        destroyOnClose
      >
        <Form form={userForm} layout="vertical" onFinish={(values) => userMutation.mutate(values)}>
          <Form.Item name="fullName" label="Ad soyad" rules={[{ required: true, message: 'Ad soyad zorunludur.' }]}>
            <Input />
          </Form.Item>
          <Form.Item name="email" label="E-posta" rules={[{ required: true, message: 'E-posta zorunludur.' }, { type: 'email', message: 'Geçerli bir e-posta girin.' }]}>
            <Input />
          </Form.Item>
          <Form.Item name="password" label="Şifre" rules={passwordRules}>
            <Input.Password />
          </Form.Item>
          <Form.Item name="roleId" label="Rol" rules={[{ required: true, message: 'Rol seçin.' }]}>
            <Select
              loading={assignableRoles.isLoading}
              options={(assignableRoles.data ?? []).map((role) => ({ value: role.id, label: role.name }))}
            />
          </Form.Item>
        </Form>
      </AppModal>
    </>
  )
}
