import { useMemo, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  Button,
  Card,
  Checkbox,
  Col,
  Collapse,
  Empty,
  Form,
  Input,
  List,
  Popconfirm,
  Row,
  Space,
  Tag,
  Typography,
  message,
} from 'antd'
import { PlusOutlined, UserAddOutlined } from '@ant-design/icons'
import { apiClient } from '../../api/client'
import { AppModal } from '../../components/AppModal'
import { IfPermission } from '../auth/IfPermission'
import { useAuth } from '../auth/AuthContext'

interface RoleRow {
  id: string
  name: string
  isSystem: boolean
  userCount: number
  permissions: string[]
}

interface CatalogItem {
  code: string
  group: string
  label: string
}

interface RoleForm {
  name: string
}

interface StaffForm {
  fullName: string
  email: string
  password: string
}

const passwordRules = [
  { required: true, message: 'Şifre zorunludur.' },
  { min: 8, message: 'Şifre en az 8 karakter olmalıdır.' },
  { pattern: /[A-Z]/, message: 'Şifre en az bir büyük harf içermelidir.' },
  { pattern: /[a-z]/, message: 'Şifre en az bir küçük harf içermelidir.' },
  { pattern: /[0-9]/, message: 'Şifre en az bir rakam içermelidir.' },
  { pattern: /[^a-zA-Z0-9]/, message: 'Şifre en az bir özel karakter içermelidir.' },
]

const groupOrder = ['Daireler', 'Finans', 'Sayaç', 'Raporlar', 'Bildirimler', 'Ayarlar', 'Roller']

const panelLabels: Record<string, string> = {
  'Roles.View': 'Rolleri Görme',
  'Roles.Manage': 'Rol ve Yetki Yönetimi',
}

function panelOf(item: CatalogItem) {
  if (item.code.startsWith('Reports.')) return 'Raporlar'
  if (item.code.startsWith('Roles.')) return 'Roller'
  return item.group
}

export function RolesPage() {
  const { hasPermission } = useAuth()
  const canManage = hasPermission('Roles.Manage')
  const queryClient = useQueryClient()
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [draftName, setDraftName] = useState('')
  const [draftPermissions, setDraftPermissions] = useState<string[]>([])
  const [creating, setCreating] = useState(false)
  const [addingStaff, setAddingStaff] = useState(false)
  const [createForm] = Form.useForm<RoleForm>()
  const [staffForm] = Form.useForm<StaffForm>()

  const roles = useQuery({
    queryKey: ['roles'],
    queryFn: async () => (await apiClient.get<RoleRow[]>('/roles')).data,
  })

  const catalog = useQuery({
    queryKey: ['permission-catalog'],
    queryFn: async () => (await apiClient.get<CatalogItem[]>('/roles/permissions')).data,
  })

  const selected = roles.data?.find((role) => role.id === selectedId) ?? roles.data?.[0] ?? null
  const activeId = selected?.id ?? null

  const groups = useMemo(() => {
    const items = (catalog.data ?? []).map((item) => ({ ...item, group: panelOf(item) }))
    const names = [...groupOrder]
    for (const item of items) {
      if (!names.includes(item.group)) {
        names.push(item.group)
      }
    }
    return names
      .map((group) => ({ group, items: items.filter((item) => item.group === group) }))
      .filter((group) => group.items.length > 0)
  }, [catalog.data])

  function selectRole(role: RoleRow) {
    setSelectedId(role.id)
    setDraftName(role.name)
    setDraftPermissions(role.permissions)
  }

  const shown = selected
    ? {
        ...selected,
        name: selectedId === selected.id ? draftName || selected.name : selected.name,
        permissions: selectedId === selected.id ? draftPermissions : selected.permissions,
      }
    : null

  const saveMutation = useMutation({
    mutationFn: async () => {
      if (!selected) {
        return
      }
      await apiClient.put(`/roles/${selected.id}`, {
        name: (selectedId === selected.id ? draftName : selected.name).trim(),
        permissions: selectedId === selected.id ? draftPermissions : selected.permissions,
      })
    },
    onSuccess: () => {
      message.success('Rol kaydedildi.')
      void queryClient.invalidateQueries({ queryKey: ['roles'] })
    },
  })

  const createMutation = useMutation({
    mutationFn: (values: RoleForm) => apiClient.post('/roles', { name: values.name.trim(), permissions: ['Apartments.View'] }),
    onSuccess: async (response, values) => {
      message.success('Rol oluşturuldu.')
      setCreating(false)
      createForm.resetFields()
      await queryClient.invalidateQueries({ queryKey: ['roles'] })
      if (typeof response.data === 'string') {
        setSelectedId(response.data)
        setDraftName(values.name.trim())
        setDraftPermissions(['Apartments.View'])
      }
    },
  })

  const deleteMutation = useMutation({
    mutationFn: (id: string) => apiClient.delete(`/roles/${id}`),
    onSuccess: () => {
      message.success('Rol silindi.')
      setSelectedId(null)
      void queryClient.invalidateQueries({ queryKey: ['roles'] })
    },
  })

  const staffMutation = useMutation({
    mutationFn: (values: StaffForm) => apiClient.post('/roles/staff', {
      fullName: values.fullName.trim(),
      email: values.email.trim(),
      password: values.password,
      roleId: activeId,
    }),
    onSuccess: () => {
      message.success('Personel hesabı açıldı.')
      setAddingStaff(false)
      staffForm.resetFields()
      void queryClient.invalidateQueries({ queryKey: ['roles'] })
    },
  })

  const currentPermissions = shown?.permissions ?? []
  const locked = !shown || shown.isSystem || !canManage

  return (
    <Row gutter={[16, 16]}>
      <Col xs={24} lg={8}>
        <Card
          title="Roller"
          extra={
            <IfPermission code="Roles.Manage">
              <Button size="small" type="primary" icon={<PlusOutlined />} onClick={() => setCreating(true)}>
                Yeni
              </Button>
            </IfPermission>
          }
        >
          <List
            loading={roles.isLoading}
            dataSource={roles.data ?? []}
            locale={{ emptyText: <Empty description="Rol yok" /> }}
            renderItem={(role) => (
              <List.Item
                style={{
                  cursor: 'pointer',
                  background: role.id === activeId ? 'rgba(22, 119, 255, 0.08)' : undefined,
                  borderRadius: 8,
                  paddingInline: 8,
                }}
                onClick={() => selectRole(role)}
              >
                <Space direction="vertical" size={0}>
                  <Space>
                    <Typography.Text strong>{role.name}</Typography.Text>
                    {role.isSystem && <Tag>Sistem</Tag>}
                  </Space>
                  <Typography.Text type="secondary">{role.userCount} kullanıcı</Typography.Text>
                </Space>
              </List.Item>
            )}
          />
        </Card>
      </Col>
      <Col xs={24} lg={16}>
        <Card
          title={shown ? shown.name : 'Yetkiler'}
          extra={
            shown && !shown.isSystem && canManage ? (
              <Space>
                <IfPermission code="Settings.ManageUsers">
                  <Button icon={<UserAddOutlined />} onClick={() => setAddingStaff(true)}>Personel ekle</Button>
                </IfPermission>
                <Popconfirm title="Bu rol silinsin mi?" okText="Sil" cancelText="Vazgeç" onConfirm={() => deleteMutation.mutate(shown.id)}>
                  <Button danger loading={deleteMutation.isPending}>Sil</Button>
                </Popconfirm>
                <Button type="primary" loading={saveMutation.isPending} onClick={() => saveMutation.mutate()}>Kaydet</Button>
              </Space>
            ) : shown?.isSystem ? <Tag>Kilitli</Tag> : null
          }
        >
          {!shown && <Empty description="Soldan bir rol seçin" />}
          {shown && (
            <Space direction="vertical" size={16} style={{ width: '100%' }}>
              {!shown.isSystem && canManage && (
                <Input
                  value={selectedId === shown.id ? draftName : shown.name}
                  maxLength={100}
                  onChange={(event) => {
                    setSelectedId(shown.id)
                    setDraftName(event.target.value)
                    setDraftPermissions(shown.permissions)
                  }}
                  placeholder="Rol adı"
                />
              )}
              <Collapse
                defaultActiveKey={groups.map((group) => group.group)}
                items={groups.map((group) => {
                  const codes = group.items.map((item) => item.code)
                  return {
                    key: group.group,
                    label: `${group.group} (${currentPermissions.filter((code) => codes.includes(code)).length}/${codes.length})`,
                    children: (
                      <Checkbox.Group
                        disabled={locked}
                        value={currentPermissions.filter((code) => codes.includes(code))}
                        onChange={(checked) => {
                          const next = [
                            ...currentPermissions.filter((code) => !codes.includes(code)),
                            ...(checked as string[]),
                          ]
                          setSelectedId(shown.id)
                          setDraftName(selectedId === shown.id ? draftName || shown.name : shown.name)
                          setDraftPermissions(next)
                        }}
                        style={{ display: 'flex', flexDirection: 'column', gap: 8 }}
                      >
                        {group.items.map((item) => (
                          <Checkbox key={item.code} value={item.code}>{panelLabels[item.code] ?? item.label}</Checkbox>
                        ))}
                      </Checkbox.Group>
                    ),
                  }
                })}
              />
            </Space>
          )}
        </Card>
      </Col>

      <AppModal
        title="Yeni rol"
        open={creating}
        onCancel={() => setCreating(false)}
        onOk={() => createForm.submit()}
        confirmLoading={createMutation.isPending}
        okText="Oluştur"
        cancelText="Vazgeç"
        destroyOnClose
      >
        <Form form={createForm} layout="vertical" onFinish={(values) => createMutation.mutate(values)}>
          <Form.Item name="name" label="Rol adı" rules={[{ required: true, message: 'Rol adı zorunludur.' }, { max: 100 }]}>
            <Input placeholder="Kapıcı" />
          </Form.Item>
          <Typography.Text type="secondary">Rol açılınca yetkileri sağdaki panellerden seçip kaydedin.</Typography.Text>
        </Form>
      </AppModal>

      <AppModal
        title="Personel ekle"
        open={addingStaff}
        onCancel={() => setAddingStaff(false)}
        onOk={() => staffForm.submit()}
        confirmLoading={staffMutation.isPending}
        okText="Ekle"
        cancelText="Vazgeç"
        destroyOnClose
      >
        <Form form={staffForm} layout="vertical" onFinish={(values) => staffMutation.mutate(values)}>
          <Form.Item name="fullName" label="Ad soyad" rules={[{ required: true, message: 'Ad soyad zorunludur.' }]}>
            <Input />
          </Form.Item>
          <Form.Item name="email" label="E-posta" rules={[{ required: true, message: 'E-posta zorunludur.' }, { type: 'email', message: 'Geçerli bir e-posta girin.' }]}>
            <Input />
          </Form.Item>
          <Form.Item name="password" label="Şifre" rules={passwordRules}>
            <Input.Password />
          </Form.Item>
        </Form>
      </AppModal>
    </Row>
  )
}
