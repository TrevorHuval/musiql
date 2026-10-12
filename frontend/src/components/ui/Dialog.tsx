import { Fragment, useEffect, useId, useRef, useState, type ReactNode } from 'react'
import { useDialogMotion } from './useDialogMotion'
import styles from './Dialog.module.css'

interface DialogProps {
  open: boolean
  onClose: () => void
  title: string
  description?: string
  children: ReactNode
}

export function Dialog({ open, onClose, title, description, children }: DialogProps) {
  const ref = useRef<HTMLDialogElement>(null)
  const titleId = useId()
  useDialogMotion(ref, open, onClose)

  useEffect(() => {
    if (!open) return
    const previous = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      document.body.style.overflow = previous
    }
  }, [open])

  const pointerDownTarget = useRef<EventTarget | null>(null)
  const [wasOpen, setWasOpen] = useState(open)
  const [openCount, setOpenCount] = useState(open ? 1 : 0)
  if (open !== wasOpen) {
    setWasOpen(open)
    if (open) setOpenCount(openCount + 1)
  }

  return (
    <dialog
      ref={ref}
      aria-labelledby={titleId}
      className="dialog dialog-motion"
      onPointerDown={(event) => {
        pointerDownTarget.current = event.target
      }}
      onClick={(event) => {
        const downOnBackdrop = pointerDownTarget.current === ref.current
        pointerDownTarget.current = null
        if (event.target === ref.current && downOnBackdrop) onClose()
      }}
      onKeyDown={(event) => {
        if (event.key === 'Escape') {
          event.preventDefault()
          onClose()
        }
      }}
    >
      {openCount > 0 && (
        <div className="dialog-stage">
          <div className={`dialog-sheet ${styles.sheet}`}>
            <h2 id={titleId} className={styles.title}>
              {title}
            </h2>
            {description && <p className={styles.description}>{description}</p>}
            <Fragment key={openCount}>{children}</Fragment>
          </div>
        </div>
      )}
    </dialog>
  )
}
