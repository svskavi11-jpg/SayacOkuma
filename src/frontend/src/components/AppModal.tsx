import { Grid, Modal } from 'antd'
import type { ModalProps } from 'antd'

/** Masaüstünde verilen genişlik, 768px altında ekranın %90'ı. */
export function AppModal({ width = 520, styles, ...props }: ModalProps) {
  const screens = Grid.useBreakpoint()
  const mobile = screens.md === false

  return (
    <Modal
      {...props}
      width={mobile ? '90%' : width}
      styles={{
        ...styles,
        body: {
          maxHeight: mobile ? '70vh' : undefined,
          overflowY: mobile ? 'auto' : undefined,
          ...styles?.body,
        },
      }}
    />
  )
}
