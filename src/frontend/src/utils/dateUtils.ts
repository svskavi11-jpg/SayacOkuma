import dayjs from 'dayjs'

/**
 * Program genelinde tek tip Türkçe tarih biçimi (GG.AA.YYYY).
 * API'den gelen ISO tarihler (2026-05-23T00:00:00) asla çiğ basılmaz.
 */
export function formatDate(dateString: string | null | undefined): string {
  if (!dateString) return '—'
  return dayjs(dateString).format('DD.MM.YYYY')
}

/** Türkçe tarih-saat biçimi (GG.AA.YYYY SS:dd). */
export function formatDateTime(dateString: string | null | undefined): string {
  if (!dateString) return '—'
  return dayjs(dateString).format('DD.MM.YYYY HH:mm')
}
