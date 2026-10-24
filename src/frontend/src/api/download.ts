import axios from 'axios'
import { apiClient } from './client'
import { notifyError } from './notifier'
import type { ProblemDetails } from '../types/api'

/**
 * Blob indirme yardımcısı: PDF/Excel endpoint'lerinden dosyayı tarayıcıya indirir.
 * Content-Disposition header'ından dosya adını çıkarır (yoksa fallback ad kullanılır).
 * Hata durumunda blob içindeki ProblemDetails JSON'u parse edilip notification gösterilir.
 */
export async function downloadFile(url: string, fallbackName: string): Promise<void> {
  try {
    const response = await apiClient.get<Blob>(url, { responseType: 'blob' })

    const disposition = response.headers['content-disposition'] as string | undefined
    let fileName = fallbackName
    if (disposition) {
      const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition)
      if (match) {
        fileName = decodeURIComponent(match[1].replace(/"/g, ''))
      }
    }

    const blobUrl = URL.createObjectURL(response.data)
    const link = document.createElement('a')
    link.href = blobUrl
    link.download = fileName
    document.body.appendChild(link)
    link.click()
    link.remove()
    URL.revokeObjectURL(blobUrl)
  } catch (error) {
    if (axios.isAxiosError(error) && error.response?.data instanceof Blob) {
      // Hata yanıtı da blob olarak geldi — ProblemDetails'e dönüştürüp göster.
      try {
        const text = await error.response.data.text()
        const problem = JSON.parse(text) as ProblemDetails
        notifyError(problem.title ?? 'Rapor hatası', problem.detail, problem.traceId)
        return
      } catch {
        /* JSON değilse genel mesaja düş */
      }
    }

    notifyError('İndirme hatası', 'Rapor dosyası indirilemedi. API sunucusunu kontrol edin.')
  }
}
