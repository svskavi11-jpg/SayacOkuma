import { useEffect } from 'react'
import { useMutation } from '@tanstack/react-query'
import { Form, Input, Typography, message } from 'antd'
import { AppModal } from '../../components/AppModal'
import { apiClient } from '../../api/client'
import type { ApartmentListItem } from '../../types/api'

interface PortalForm {
  fullName: string
  email: string
  password: string
}

export function PortalAccountModal({
  apartment,
  onClose,
}: {
  apartment: ApartmentListItem | null
  onClose: () => void
}) {
  const [form] = Form.useForm<PortalForm>()
  const mutation = useMutation({
    mutationFn: async (values: PortalForm) => {
      await apiClient.post(`/apartments/${apartment!.id}/resident-account`, values)
    },
    onSuccess: () => {
      message.success('Sakin hesabı kaydedildi.')
      form.resetFields()
      onClose()
    },
  })

  useEffect(() => {
    if (!apartment) return
    form.setFieldsValue({
      fullName: apartment.kiraciName || apartment.ownerName || '',
      email: apartment.kiraciEmail || apartment.evSahibiEmail || '',
      password: '',
    })
  }, [apartment, form])

  return (
    <AppModal
      title={apartment ? `Portal hesabı · Daire ${apartment.doorNumber}` : 'Portal hesabı'}
      open={apartment !== null}
      onCancel={onClose}
      okText="Kaydet"
      cancelText="Vazgeç"
      confirmLoading={mutation.isPending}
      onOk={() => form.submit()}
      destroyOnClose
    >
      <Typography.Paragraph type="secondary">
        Bu daireye giriş hesabı açılır. E-posta aynı sitede kayıtlı bir sakine aitse şifre ve daire güncellenir.
      </Typography.Paragraph>
      <Form form={form} layout="vertical" onFinish={(values) => mutation.mutate(values)}>
        <Form.Item name="fullName" label="Ad soyad" rules={[{ required: true, message: 'Ad soyad zorunludur.' }]}>
          <Input maxLength={150} />
        </Form.Item>
        <Form.Item
          name="email"
          label="E-posta"
          rules={[
            { required: true, message: 'E-posta zorunludur.' },
            { type: 'email', message: 'Geçerli bir e-posta girin.' },
          ]}
        >
          <Input />
        </Form.Item>
        <Form.Item
          name="password"
          label="Şifre"
          extra="En az 8 karakter, büyük harf, küçük harf, rakam ve özel karakter."
          rules={[
            { required: true, message: 'Şifre zorunludur.' },
            { min: 8, message: 'Şifre en az 8 karakter olmalıdır.' },
            { pattern: /[A-Z]/, message: 'Şifre en az bir büyük harf içermelidir.' },
            { pattern: /[a-z]/, message: 'Şifre en az bir küçük harf içermelidir.' },
            { pattern: /[0-9]/, message: 'Şifre en az bir rakam içermelidir.' },
            { pattern: /[^a-zA-Z0-9]/, message: 'Şifre en az bir özel karakter içermelidir.' },
          ]}
        >
          <Input.Password />
        </Form.Item>
      </Form>
    </AppModal>
  )
}
