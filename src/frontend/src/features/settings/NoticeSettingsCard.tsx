import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Button, Card, Col, Form, Input, InputNumber, Row, Slider, Space, Typography, Upload, message } from 'antd'
import { DeleteOutlined, UploadOutlined } from '@ant-design/icons'
import { useState } from 'react'
import { apiClient } from '../../api/client'
import type { NoticeSettingsDTO } from '../../types/api'

type Side = 'left' | 'right'

/** Logo önizleme + yükle/kaldır. Resim DB'de saklanır; PDF'in sol/sağ üstüne basılır. */
/** PDF'teki logo yüksekliği sınırları (mm) — sunucu tarafındaki DomainConstants ile aynı. */
const LOGO_HEIGHT_MIN_MM = 8
const LOGO_HEIGHT_MAX_MM = 40
/** Önizleme ölçeği: 1 mm ≈ bu kadar piksel (kart genişliğine göre ölçekli). */
const PREVIEW_PX_PER_MM = 2.2

/** Slider + sayı kutusu; Form.Item ile value/onChange alır. */
function HeightControl({ value, onChange }: { value?: number; onChange?: (v: number) => void }) {
  return (
    <Row gutter={8} align="middle">
      <Col flex="auto">
        <Slider min={LOGO_HEIGHT_MIN_MM} max={LOGO_HEIGHT_MAX_MM} value={value} onChange={onChange} />
      </Col>
      <Col flex="90px">
        <InputNumber
          min={LOGO_HEIGHT_MIN_MM}
          max={LOGO_HEIGHT_MAX_MM}
          precision={0}
          value={value}
          onChange={(v) => v !== null && onChange?.(v)}
          addonAfter="mm"
          style={{ width: '100%' }}
        />
      </Col>
    </Row>
  )
}

function LogoField({
  side,
  label,
  hasLogo,
  version,
  heightName,
  heightMm,
  onChanged,
}: {
  side: Side
  label: string
  hasLogo: boolean
  version: number
  heightName: 'logoLeftHeightMm' | 'logoRightHeightMm'
  heightMm: number
  onChanged: () => void
}) {
  const upload = useMutation({
    mutationFn: async (file: File) => {
      const body = new FormData()
      body.append('file', file)
      await apiClient.put(`/settings/notice/logo/${side}`, body)
    },
    onSuccess: () => {
      message.success(`${label} yüklendi.`)
      onChanged()
    },
  })

  const remove = useMutation({
    mutationFn: async () => {
      await apiClient.delete(`/settings/notice/logo/${side}`)
    },
    onSuccess: () => {
      message.success(`${label} kaldırıldı; yerine metin başlık basılacak.`)
      onChanged()
    },
  })

  return (
    <div>
      <Typography.Text strong>{label}</Typography.Text>
      <div
        style={{
          margin: '8px 0',
          minHeight: 70,
          border: '1px dashed #d9d9d9',
          borderRadius: 6,
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          background: '#fafafa',
        }}
      >
        {hasLogo ? (
          <img
            src={`/api/v1/settings/notice/logo/${side}?v=${version}`}
            alt={label}
            style={{ height: heightMm * PREVIEW_PX_PER_MM, maxWidth: '95%', objectFit: 'contain' }}
          />
        ) : (
          <Typography.Text type="secondary">Logo yok (metin basılır)</Typography.Text>
        )}
      </div>
      <Form.Item
        name={heightName}
        label="PDF'teki yükseklik"
        extra="Genişlik oranla otomatik ayarlanır. Önizleme ölçeklidir."
        style={{ marginBottom: 8 }}
      >
        <HeightControl />
      </Form.Item>
      <Space>
        <Upload
          accept="image/png,image/jpeg"
          showUploadList={false}
          beforeUpload={(file) => {
            if (file.size > 1_048_576) {
              message.error('Logo dosyası en fazla 1 MB olabilir.')
              return Upload.LIST_IGNORE
            }
            upload.mutate(file)
            return false
          }}
        >
          <Button icon={<UploadOutlined />} loading={upload.isPending}>
            Yükle (PNG/JPG)
          </Button>
        </Upload>
        {hasLogo && (
          <Button danger icon={<DeleteOutlined />} loading={remove.isPending} onClick={() => remove.mutate()}>
            Kaldır
          </Button>
        )}
      </Space>
    </div>
  )
}

/** Bireysel Gider Bildirimi (PDF) form ayarları — tüm alanlar veritabanından yönetilir. */
export function NoticeSettingsCard() {
  const queryClient = useQueryClient()
  const [form] = Form.useForm<NoticeSettingsDTO>()
  // Logo değişince <img> önbelleğini kırmak için sürüm sayacı.
  const [logoVersion, setLogoVersion] = useState(0)
  // Slider hareket ettikçe önizleme canlı büyüsün/küçalsın.
  const leftHeight = Form.useWatch('logoLeftHeightMm', form)
  const rightHeight = Form.useWatch('logoRightHeightMm', form)

  const query = useQuery({
    queryKey: ['notice-settings'],
    queryFn: async () => (await apiClient.get<NoticeSettingsDTO>('/settings/notice')).data,
  })

  const save = useMutation({
    mutationFn: async (values: NoticeSettingsDTO) =>
      (await apiClient.put<NoticeSettingsDTO>('/settings/notice', values)).data,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['notice-settings'] })
      message.success('Bildirim formu ayarları kaydedildi. Bir sonraki PDF yeni değerlerle üretilir.')
    },
  })

  const data = query.data
  if (query.isLoading || !data) {
    return <Card loading style={{ maxWidth: 800, marginTop: 16 }} />
  }

  return (
    <Card title="Bireysel Gider Bildirimi Formu" style={{ maxWidth: 800, marginTop: 16 }}>
      <Typography.Paragraph type="secondary">
        PDF formundaki sol/sağ üst logolar ve değişebilen tanımlar buradan girilir. Hesaplama
        sonuçları (tutarlar, okumalar, m²) otomatik gelir.
      </Typography.Paragraph>

      <Form
        form={form}
        layout="vertical"
        initialValues={data}
        key={`${data.dueDateDays}-${data.buildingAddress}-${data.leftTitle}-${data.rightTitle}`}
        onFinish={(values) => save.mutate(values)}
      >
        <Row gutter={16} style={{ marginBottom: 16 }}>
          <Col span={12}>
            <LogoField
              side="left"
              label="Sol Üst Logo"
              hasLogo={data.hasLeftLogo}
              version={logoVersion}
              heightName="logoLeftHeightMm"
              heightMm={leftHeight ?? data.logoLeftHeightMm}
              onChanged={() => {
                setLogoVersion((v) => v + 1)
                void queryClient.invalidateQueries({ queryKey: ['notice-settings'] })
              }}
            />
          </Col>
          <Col span={12}>
            <LogoField
              side="right"
              label="Sağ Üst Logo"
              hasLogo={data.hasRightLogo}
              version={logoVersion}
              heightName="logoRightHeightMm"
              heightMm={rightHeight ?? data.logoRightHeightMm}
              onChanged={() => {
                setLogoVersion((v) => v + 1)
                void queryClient.invalidateQueries({ queryKey: ['notice-settings'] })
              }}
            />
          </Col>
        </Row>
        <Row gutter={16}>
          <Col span={12}>
            <Form.Item name="leftTitle" label="Sol Üst Metin (logo yoksa)" extra="Boşsa Şirket Adı kullanılır.">
              <Input maxLength={100} placeholder="Çınar Teknik" />
            </Form.Item>
          </Col>
          <Col span={12}>
            <Form.Item name="rightTitle" label="Sağ Üst Metin (logo yoksa)">
              <Input maxLength={100} placeholder="Danfoss" />
            </Form.Item>
          </Col>
        </Row>

        <Form.Item
          name="buildingAddress"
          label="Bina Adresi"
          rules={[{ required: true, message: 'Bina adresi zorunludur.' }]}
        >
          <Input maxLength={500} />
        </Form.Item>

        <Row gutter={16}>
          <Col span={8}>
            <Form.Item name="dueDateDays" label="Son Ödeme (oluşturmadan sonra gün)">
              <InputNumber style={{ width: '100%' }} min={0} max={120} precision={0} addonAfter="gün" />
            </Form.Item>
          </Col>
          <Col span={8}>
            <Form.Item name="readingServiceFee" label="Okuma Hizmet Bedeli">
              <InputNumber
                style={{ width: '100%' }}
                min={0}
                step={0.5}
                precision={2}
                decimalSeparator=","
                addonAfter="TL"
              />
            </Form.Item>
          </Col>
          <Col span={8}>
            <Form.Item
              name="defaultSubscriberNumber"
              label="Varsayılan Abone No"
              extra="Faturada abone no yoksa."
            >
              <Input maxLength={20} />
            </Form.Item>
          </Col>
        </Row>

        <Form.Item
          name="defaultNote"
          label="Ek Açıklamalar (varsayılan)"
          extra="Faturaya özel not girilmediyse formun 'Ek Açıklamalar' bölümüne basılır."
        >
          <Input.TextArea rows={3} maxLength={1000} showCount />
        </Form.Item>

        <Button type="primary" htmlType="submit" loading={save.isPending}>
          Kaydet
        </Button>
      </Form>
    </Card>
  )
}
