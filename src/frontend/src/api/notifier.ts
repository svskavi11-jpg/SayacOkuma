import type { ArgsProps } from 'antd/es/notification/interface'

// Global notifier: AppLayout, AntD App.useApp ile buraya kayit yapar.
// Boylece axios interceptor'i React context'ine bagimli olmadan hata gosterir.
let notifier: ((args: ArgsProps) => void) | null = null

export function registerNotifier(fn: (args: ArgsProps) => void): void {
  notifier = fn
}

export function notifyError(title: string, description?: string, traceId?: string): void {
  const descParts: string[] = []
  if (description) descParts.push(description)
  if (traceId) descParts.push(`Hata kodu: ${traceId}`)

  notifier?.({
    message: title,
    description: descParts.length > 0 ? descParts.join('\n') : undefined,
    placement: 'topRight',
    duration: 8,
  })
}