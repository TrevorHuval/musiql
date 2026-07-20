import {
  Dialog as HuiDialog,
  DialogBackdrop,
  DialogPanel,
  DialogTitle,
} from '@headlessui/react'
import type { ReactNode } from 'react'
import styles from './Dialog.module.css'

interface DialogProps {
  open: boolean
  onClose: () => void
  title: string
  description?: string
  children: ReactNode
}

export function Dialog({ open, onClose, title, description, children }: DialogProps) {
  return (
    <HuiDialog open={open} onClose={onClose} className={styles.root}>
      <DialogBackdrop transition className={styles.backdrop} />
      <div className={styles.positioner}>
        <DialogPanel transition className={styles.panel}>
          <DialogTitle className={styles.title}>{title}</DialogTitle>
          {description && <p className={styles.description}>{description}</p>}
          {children}
        </DialogPanel>
      </div>
    </HuiDialog>
  )
}
