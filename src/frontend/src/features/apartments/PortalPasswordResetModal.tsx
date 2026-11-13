import { useEffect } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Form, Input, Typography, message } from 'antd'
import { AppModal } from '../../components/AppModal'
import { apiClient } from '../../api/client'
import type { ApartmentListItem } from '../../types/api'

interface ResetForm {
  password: string
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

export function PortalPasswordResetModal({
  apartment,
  onClose,
}: {
  apartment: ApartmentListItem | null
  onClose: () => void
}) {
  const [form] = Form.useForm<ResetForm>()
  const mutation = useMutation({
    mutationFn: async (values: ResetForm) => {
      await apiClient.post(`/apartments/${apartment!.id}/resident-account/reset-password`, {
        newPassword: values.password,
      })
    },
    onSuccess: () => {
      message.success('Portal şifresi sıfırlandı.')
      form.resetFields()
      onClose()
    },
  })

  useEffect(() => {
    if (apartment) {
      form.resetFields()
    }
  }, [apartment, form])

  return (
    <AppModal
      title={apartment ? `Portal şifresi · Daire ${apartment.doorNumber}` : 'Portal şifresi'}
      open={apartment !== null}
      onCancel={onClose}
      okText="Sıfırla"
      cancelText="Vazgeç"
      confirmLoading={mutation.isPending}
      onOk={() => form.submit()}
      destroyOnClose
    >
      <Typography.Paragraph type="secondary">
        Yeni şifreyi sakine siz iletin. E-posta gönderilmez. Portal hesabı yoksa işlem reddedilir.
      </Typography.Paragraph>
      <Form form={form} layout="vertical" onFinish={(values) => mutation.mutate(values)}>
        <Form.Item name="password" label="Yeni şifre" rules={passwordRules}>
          <Input.Password />
        </Form.Item>
        <Form.Item
          name="confirm"
          label="Yeni şifre (tekrar)"
          dependencies={['password']}
          rules={[
            { required: true, message: 'Şifre tekrarı zorunludur.' },
            ({ getFieldValue }) => ({
              validator(_, value) {
                if (!value || getFieldValue('password') === value) {
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
