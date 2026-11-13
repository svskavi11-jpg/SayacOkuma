import { useEffect } from 'react'
import axios from 'axios'
import { useMutation } from '@tanstack/react-query'
import { Form, Input, Typography, message } from 'antd'
import { AppModal } from '../../components/AppModal'
import { readToken } from './authStorage'

interface PasswordForm {
  currentPassword: string
  newPassword: string
  confirm: string
}

const passwordRules = [
  { required: true, message: 'Şifre zorunludur.' },
  { min: 8, message: 'Şifre en az 8 karakter olmalıdır.' },
  { pattern: /[A-Z]/, message: 'Şifre en az bir büyük harf içermelidir.' },
  { pattern: /[a-z]/, message: 'Şifre en az bir küçük harf içermelidir.' },
  { pattern: /[0-9]/, message: 'Şifre en az bir rakam içermelidir.' },
  { pattern: /[^a-zA-Z0-9]/, message: 'Şifre en az bir özel karakter içermelidir.' },
]

export function ChangePasswordModal({ open, onClose }: { open: boolean; onClose: () => void }) {
  const [form] = Form.useForm<PasswordForm>()
  const mutation = useMutation({
    mutationFn: async (values: PasswordForm) => {
      await axios.post(
        '/api/auth/change-password',
        { currentPassword: values.currentPassword, newPassword: values.newPassword },
        { headers: { Authorization: `Bearer ${readToken()}` } },
      )
    },
    onSuccess: () => {
      message.success('Şifreniz değiştirildi.')
      form.resetFields()
      onClose()
    },
    onError: (error: unknown) => {
      const detail = (error as { response?: { data?: { detail?: string } } }).response?.data?.detail
      message.error(detail || 'Şifre değiştirilemedi.')
    },
  })

  useEffect(() => {
    if (open) {
      form.resetFields()
    }
  }, [open, form])

  return (
    <AppModal
      title="Profil / Şifre Değiştir"
      open={open}
      onCancel={onClose}
      okText="Kaydet"
      cancelText="Vazgeç"
      confirmLoading={mutation.isPending}
      onOk={() => form.submit()}
      destroyOnClose
    >
      <Typography.Paragraph type="secondary">
        Mevcut şifrenizi ve yeni şifrenizi girin. Oturumunuz açık kalır.
      </Typography.Paragraph>
      <Form form={form} layout="vertical" onFinish={(values) => mutation.mutate(values)}>
        <Form.Item name="currentPassword" label="Mevcut şifre" rules={[{ required: true, message: 'Mevcut şifre zorunludur.' }]}>
          <Input.Password />
        </Form.Item>
        <Form.Item name="newPassword" label="Yeni şifre" extra="En az 8 karakter, büyük harf, küçük harf, rakam ve özel karakter." rules={passwordRules}>
          <Input.Password />
        </Form.Item>
        <Form.Item
          name="confirm"
          label="Yeni şifre (tekrar)"
          dependencies={['newPassword']}
          rules={[
            { required: true, message: 'Şifre tekrarı zorunludur.' },
            ({ getFieldValue }) => ({
              validator(_, value) {
                if (!value || getFieldValue('newPassword') === value) {
                  return Promise.resolve()
                }
                return Promise.reject(new Error('Şifreler aynı değil.'))
              },
            }),
          ]}
        >
          <Input.Password />
        </Form.Item>
      </Form>
    </AppModal>
  )
}
