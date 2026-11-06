import { Button, Col, DatePicker, Row, Select, Table, Tag, Upload } from 'antd'
import { UploadOutlined } from '@ant-design/icons'
import type { ColumnsType } from 'antd/es/table'
import type { UploadFile } from 'antd/es/upload/interface'
import { Form } from 'antd'
import dayjs from 'dayjs'
import { apiClient } from '../../api/client'
import { downloadFile } from '../../api/download'
import { formatDate } from '../../utils/dateUtils'
import type { ApartmentOwnerHistoryItem, ResidentDocumentItem, ResidentDocumentType } from '../../types/api'

const documentOptions = [
  { value: 'KiraSozlesmesi', label: 'Kira sözleşmesi' },
  { value: 'Tapu', label: 'Tapu' },
  { value: 'Diger', label: 'Diğer' },
]

function roleTag(residentType: ApartmentOwnerHistoryItem['residentType']) {
  return residentType === 'Kiraci' ? <Tag color="geekblue">Kiracı</Tag> : <Tag color="purple">Ev Sahibi</Tag>
}

export function StayFields({
  prefix,
  startRequired,
  files,
  onFiles,
  documentType,
  onDocumentType,
}: {
  prefix: 'ev' | 'ki'
  startRequired: boolean
  files: UploadFile[]
  onFiles: (files: UploadFile[]) => void
  documentType: ResidentDocumentType
  onDocumentType: (value: ResidentDocumentType) => void
}) {
  return (
    <>
      <Row gutter={12}>
        <Col span={12}>
          <Form.Item
            name={`${prefix}ValidFrom`}
            label="Geçerlilik Başlangıcı"
            rules={
              startRequired
                ? [{ required: true, message: 'Başlangıç tarihi zorunludur.' }]
                : [
                    ({ getFieldValue }) => ({
                      validator(_: unknown, value: unknown) {
                        if (prefix === 'ki' && getFieldValue('kiName') && !value) {
                          return Promise.reject(new Error('Başlangıç tarihi zorunludur.'))
                        }
                        return Promise.resolve()
                      },
                    }),
                  ]
            }
          >
            <DatePicker format="DD.MM.YYYY" style={{ width: '100%' }} />
          </Form.Item>
        </Col>
        <Col span={12}>
          <Form.Item name={`${prefix}ValidTo`} label="Geçerlilik Bitişi">
            <DatePicker format="DD.MM.YYYY" style={{ width: '100%' }} />
          </Form.Item>
        </Col>
      </Row>
      <Row gutter={12}>
        <Col span={12}>
          <Form.Item name={`${prefix}LeaseStart`} label="Sözleşme Başlangıcı">
            <DatePicker format="DD.MM.YYYY" style={{ width: '100%' }} />
          </Form.Item>
        </Col>
        <Col span={12}>
          <Form.Item name={`${prefix}LeaseEnd`} label="Sözleşme Bitişi">
            <DatePicker format="DD.MM.YYYY" style={{ width: '100%' }} />
          </Form.Item>
        </Col>
      </Row>
      <Row gutter={12}>
        <Col span={10}>
          <Form.Item label="Evrak türü">
            <Select value={documentType} options={documentOptions} onChange={onDocumentType} />
          </Form.Item>
        </Col>
        <Col span={14}>
          <Form.Item label="Evrak">
            <Upload
              accept=".pdf,.jpg,.jpeg,.png"
              multiple
              fileList={files}
              beforeUpload={(file) => {
                onFiles([...files, file])
                return false
              }}
              onRemove={(file) => onFiles(files.filter((item) => item.uid !== file.uid))}
            >
              <Button icon={<UploadOutlined />}>Dosya seç</Button>
            </Upload>
          </Form.Item>
        </Col>
      </Row>
    </>
  )
}

const columns: ColumnsType<ApartmentOwnerHistoryItem> = [
  { title: 'Kişi', dataIndex: 'ownerName' },
  { title: 'Rol', dataIndex: 'residentType', width: 110, render: roleTag },
  { title: 'Giriş', dataIndex: 'validFrom', width: 110, render: (value: string) => formatDate(value) },
  {
    title: 'Çıkış',
    dataIndex: 'validTo',
    width: 130,
    render: (value: string | null) => (value ? formatDate(value) : 'Devam Ediyor'),
  },
  {
    title: 'Sözleşme Başlangıç',
    dataIndex: 'leaseStartDate',
    width: 140,
    render: (value: string | null) => (value ? dayjs(value).format('DD.MM.YYYY') : '-'),
  },
  {
    title: 'Sözleşme Bitiş',
    dataIndex: 'leaseEndDate',
    width: 140,
    render: (value: string | null) => (value ? dayjs(value).format('DD.MM.YYYY') : '-'),
  },
  { title: 'Telefon', dataIndex: 'phoneNumber', width: 130, render: (value: string | null) => value || '—' },
]

export function ResidentHistoryBlock({
  rows,
  documents,
  loading,
}: {
  rows: ApartmentOwnerHistoryItem[]
  documents: ResidentDocumentItem[]
  loading: boolean
}) {
  const ordered = [...rows].sort((a, b) => {
    if (!a.validTo && b.validTo) return -1
    if (a.validTo && !b.validTo) return 1
    return dayjs(b.validTo ?? b.validFrom).valueOf() - dayjs(a.validTo ?? a.validFrom).valueOf()
  })

  return (
    <>
      <Table
        rowKey="id"
        size="small"
        loading={loading}
        columns={columns}
        dataSource={ordered}
        pagination={false}
        locale={{ emptyText: 'Bu dairede sakin kaydı yok.' }}
        style={{ marginTop: 8 }}
      />
      {documents.length > 0 && (
        <div style={{ marginTop: 8 }}>
          {documents.map((document) => (
            <Button
              key={document.id}
              type="link"
              size="small"
              onClick={() => void downloadFile(`/residents/documents/${document.id}/file`, document.fileName)}
            >
              {document.ownerName} — {document.fileName}
            </Button>
          ))}
        </div>
      )}
    </>
  )
}

export async function uploadResidentFiles(ownerId: string, files: UploadFile[], documentType: ResidentDocumentType) {
  for (const file of files) {
    const payload = new FormData()
    payload.append('file', file.originFileObj ?? (file as unknown as Blob), file.name)
    payload.append('documentType', documentType)
    await apiClient.post(`/residents/${ownerId}/documents`, payload)
  }
}
